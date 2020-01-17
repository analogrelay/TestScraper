using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Internal.TestScraper.Db;
using Microsoft.AspNetCore.Internal.TestScraper.Formats;
using Microsoft.AspNetCore.Internal.TestScraper.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Scaffolding.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.AspNetCore.Internal.TestScraper.Worker
{
    public class PipelineScannerService : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IOptionsMonitor<PipelineScannerOptions> _options;
        private readonly VssConnection _connection;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly HttpClient _client;
        private BuildHttpClient _buildClient;

        public PipelineScannerService(ILoggerFactory loggerFactory, IOptionsMonitor<PipelineScannerOptions> options, VssConnection connection, IServiceScopeFactory scopeFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<PipelineScannerService>();
            _options = options;
            _connection = connection;
            _scopeFactory = scopeFactory;
            _client = new HttpClient();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Get off the calling thread immediately
            await Task.Yield();
            try
            {

                _logger.LogInformation(new EventId(0, "StartingScanner"), "Starting pipeline scanner loop. Interval: {ScanInterval}.", _options.CurrentValue.ScanInterval);

                _buildClient = await _connection.GetClientAsync<BuildHttpClient>(stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    var options = _options.CurrentValue;
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var context = new TestResultsScrapeContext(scope.ServiceProvider.GetRequiredService<TestResultsDbContext>(), _buildClient, options, _loggerFactory.CreateLogger<TestResultsScrapeContext>());
                        _logger.LogInformation(new EventId(0, "PrimingCaches"), "Priming caches...");
                        await context.PrimeCachesAsync(stoppingToken);
                        _logger.LogInformation(new EventId(0, "PrimedCaches"), "Primed caches.");

                        _logger.LogInformation(new EventId(0, "RunningScanLoop"), "Running scan loop.");
                        foreach (var config in context.Options.Pipelines)
                        {
                            using (_logger.BeginScope("Pipeline: {PipelineProject}/{PipelineName}", config.Project, config.Name))
                            {
                                _logger.LogInformation(new EventId(0, "ProcessingPipeline"), "Processing pipeline {PipelineProject}/{PipelineName}...", config.Project, config.Name);
                                await ProcessPipelineAsync(context, config, stoppingToken);
                                _logger.LogInformation(new EventId(0, "ProcessedPipeline"), "Processed pipeline {PipelineProject}/{PipelineName}.", config.Project, config.Name);
                            }
                        }
                    }

                    _logger.LogInformation("Sleeping for {ScanInterval}...", options.ScanInterval);
                    await Task.Delay(options.ScanInterval);

                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation(new EventId(0, "CancellingSync"), "Cancelling pipeline sync.");
            }

            _logger.LogInformation(new EventId(0, "Stopped"), "Service Stopped.");
        }

        private async Task ProcessPipelineAsync(TestResultsScrapeContext context, PipelineConfig config, CancellationToken stoppingToken)
        {
            try
            {
                var pipelineContext = await context.CreatePipelineSyncContextAsync(config, stoppingToken);

                foreach (var branch in config.Branches)
                {
                    using (_logger.BeginScope("Branch: {SourceBranch}", branch))
                    {
                        _logger.LogInformation(new EventId(0, "ProcessingBranch"), "Processing builds in branch {BranchName}...", branch);
                        await ProcessBranchAsync(pipelineContext, config, branch, stoppingToken);
                        _logger.LogInformation(new EventId(0, "ProcessedBranch"), "Processed builds in branch {BranchName}.", branch);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug(new EventId(0, "CancellingPipelineProcessing"), "Cancelling processing of pipeline: {PipelineProject}/{PipelineName}.", config.Project, config.Name);
                throw;
            }
        }

        private async Task ProcessBranchAsync(PipelineScrapeContext context, PipelineConfig config, string branch, CancellationToken stoppingToken)
        {
            // Look up the most recent build (for now)
            _logger.LogDebug(new EventId(0, "FetchingBuilds"), "Fetching builds for {PipelineProject}/{PipelineName} in {BranchName}...", config.Project, config.Name, branch);
            var builds = await context.GetBuildsAsync(branch, stoppingToken);
            _logger.LogInformation(new EventId(0, "FetchedBuilds"), "Fetched {BuildCount} builds.", builds.Count);

            foreach (var build in builds)
            {
                using (_logger.BeginScope("Build {BuildId} #{BuildNumber}", build.Id, build.BuildNumber, build.SourceVersion))
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<TestResultsDbContext>();
                        // Load the Build from the new db context
                        var dbBuild = await db.Builds.FirstOrDefaultAsync(b => b.Id == build.Id);
                        if (dbBuild == null)
                        {
                            _logger.LogError(new EventId(0, "CouldNotFindBuild"), "Could not find build #{BuildNumber} (ID: {BuildDbId}) in the database!", build.BuildNumber, build.Id);
                            continue;
                        }

                        var buildContext = new BuildSyncContext(context, dbBuild, db);

                        // TODO: Retry of unsynced builds?
                        _logger.LogInformation(new EventId(0, "ProcessingBuild"), "Processing build #{BuildNumber}...", build.BuildNumber);
                        var sw = Stopwatch.StartNew();
                        await ProcessBuildAsync(buildContext, stoppingToken);
                        _logger.LogInformation(new EventId(0, "ProcessedBuild"), "Processed build #{BuildNumber} in {Elapsed}.", build.BuildNumber, sw.Elapsed);
                    }
                }
            }
        }

        private async Task ProcessBuildAsync(BuildSyncContext context, CancellationToken stoppingToken)
        {
            // Mark this build as in progress.
            context.DbBuild.SyncStatus = SyncStatus.InProgress;
            context.DbBuild.SyncStartedUtc = DateTime.UtcNow;
            await context.Db.SaveChangesAsync();
            _logger.LogDebug(new EventId(0, "StartedSyncForBuild"), "Started Sync for Build #{BuildNumber}", context.DbBuild.BuildNumber);

            try
            {
                var artifacts = await context.GetArtifactsAsync(cancellationToken: stoppingToken);
                foreach (var artifact in artifacts)
                {
                    _logger.LogDebug(new EventId(0, "ProcessingArtifact"), "Processing artifact {ArtifactName}...", artifact.Name);
                    await ProcessArtifactAsync(context, artifact, stoppingToken);
                    _logger.LogDebug(new EventId(0, "ProcessedArtifact"), "Processed artifact {ArtifactName}.", artifact.Name);
                }

                context.DbBuild.SyncStatus = SyncStatus.Complete;
                context.DbBuild.SyncCompleteUtc = DateTime.UtcNow;
                await context.Db.SaveChangesAsync();
                _logger.LogDebug(new EventId(0, "SavedBuild"), "Saved all results from build #{BuildNumber}", context.DbBuild.BuildNumber);
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug(new EventId(0, "CancellingBuildProcessing"), "Cancelling processing of build: #{BuildNumber}.", context.DbBuild.BuildNumber);
                context.DbBuild.SyncStatus = SyncStatus.Cancelled;
                await context.Db.SaveChangesAsync();
                throw;
            }
            catch (Exception ex)
            {
                context.DbBuild.SyncStatus = SyncStatus.Failed;
                await context.Db.SaveChangesAsync();
                _logger.LogError(new EventId(0, "ErrorProcessingBuild"), ex, "Error Processing Build #{BuildNumber}.", context.DbBuild.BuildNumber);
            }
        }

        private async Task ProcessArtifactAsync(BuildSyncContext context, BuildArtifact artifact, CancellationToken stoppingToken)
        {
            // Stream the artifact down to disk
            var artifactFile = await SaveArtifactAsync(artifact, stoppingToken);
            try
            {
                using var archive = ZipFile.OpenRead(artifactFile);
                foreach (var file in archive.Entries)
                {
                    using (_logger.BeginScope("File: {ArtifactFile}", file.FullName))
                    {
                        if (file.Name.EndsWith(".xml"))
                        {
                            _logger.LogTrace(new EventId(0, "ProcessingFile"), "Processing file {ArtifactFile}...", file);
                            await ProcessTestResultFileAsync(context, file, stoppingToken);
                            _logger.LogTrace(new EventId(0, "ProcessedFile"), "Processed file {ArtifactFile}.", file);
                        }
                        else
                        {
                            _logger.LogTrace(new EventId(0, "SkippingFile"), "Skipping artifact file {ArtifactFile}.", file);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug(new EventId(0, "CancellingArtifactProcessing"), "Cancelling processing of artifact: {ArtifactName}.", artifact.Name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(0, "ErrorProcessingArtifact"), ex, "Error processing artifact: {ArtifactName}.", artifact.Name);

                // We don't want to continue processing this build if an artifact failed. That way we can try again later.
                throw;
            }
            finally
            {
                try
                {
                    _logger.LogTrace(new EventId(0, "DeletingTemporaryFile"), "Deleting temporary file {TempFile} for Artifact {ArtifactName}.", artifactFile, artifact.Name);
                    File.Delete(artifactFile);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(new EventId(0, "ErrorDeletingTemporaryFile"), ex, "Error deleting temporary file: {TempFile}.", artifactFile);
                }
            }
        }

        private async Task ProcessTestResultFileAsync(BuildSyncContext context, ZipArchiveEntry file, CancellationToken stoppingToken)
        {
            try
            {
                using var stream = file.Open();
                var doc = await XDocument.LoadAsync(stream, LoadOptions.None, stoppingToken);
                // Remove the extension but keep the full relative path to get the run name.
                var runName = file.FullName.Substring(0, file.FullName.Length - 4);
                var dbRunId = await context.PipelineContext.ResultsContext.GetOrCreateRunAsync(runName, stoppingToken);

                _logger.LogTrace(new EventId(0, "ParsingTestResults"), "Parsing test results.");
                var assemblies = XUnitTestResultsFormat.Parse(doc);

                _logger.LogTrace(new EventId(0, "ProcessingTestResults"), "Processing test results...");

                foreach (var assembly in assemblies)
                {
                    foreach (var collection in assembly.Collections)
                    {
                        var dbCollection = dbAssembly.Collections.FirstOrDefault(c => c.Name == collection.Name);
                        if(dbCollection == null)
                        {
                            dbCollection = new PipelineTestAssembly()
                            {
                                Name = assembly.Name
                            };
                            context.Db.TestAssemblies.Add(dbAssembly);
                        }
                        foreach (var method in collection.Methods)
                        {
                            var dbMethodId = await context.PipelineContext.ResultsContext.GetOrCreateMethodAsync(dbCollectionId, method.Type, method.Name, stoppingToken);

                            foreach (var result in method.Results)
                            {
                                // Truncate off the type name
                                var resultName = result.Name.StartsWith($"{method.Type}.") ? result.Name.Substring(method.Type.Length + 1) : result.Name;

                                var dbCaseId = await context.PipelineContext.ResultsContext.GetOrCreateTestCaseAsync(dbMethodId, resultName, stoppingToken);

                                var quarantinedOn = new List<string>();
                                foreach (var trait in result.Traits)
                                {
                                    if (trait.Name.StartsWith("Flaky:"))
                                    {
                                        quarantinedOn.Add(trait.Name.Substring(6));
                                    }
                                    else if (trait.Name.StartsWith("Quarantined:"))
                                    {
                                        quarantinedOn.Add(trait.Name.Substring(12));
                                    }
                                }

                                var dbResult = new PipelineTestResult()
                                {
                                    BuildId = context.DbBuild.Id,
                                    CaseId = dbCaseId,
                                    RunId = dbRunId,
                                    Traits = string.Join(";", result.Traits.Select(t => $"{t.Name}={t.Value}")),
                                    Quarantined = result.Traits.Any(t => t.Name.StartsWith("Flaky:") || t.Name.StartsWith("Quarantined:")),
                                    QuarantinedOn = string.Join(";", result.Traits.Where(t => t.Name.StartsWith("Flaky:")).Select(t => t.Name.Substring(6))),
                                    Result = result.Outcome switch
                                    {
                                        FailureTestOutcome f => TestResultKind.Fail,
                                        SkippedTestOutcome s => TestResultKind.Skip,
                                        SuccessfulTestOutcome _ => TestResultKind.Pass,
                                        _ => TestResultKind.Unknown,
                                    }
                                };
                                context.Db.TestResults.Add(dbResult);
                            }
                        }
                    }
                }

                // Save results per-file.
                _logger.LogTrace(new EventId(0, "SavingTestResults"), "Saving test results to database...");
                await context.Db.SaveChangesAsync();
                _logger.LogTrace(new EventId(0, "SavedTestResults"), "Saved test results to database");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(0, "ErrorProcessingTestResults"), ex, "Error processing test result file: {TestResultsFile}", file.FullName);
                throw;
            }
        }

        private async Task<string> SaveArtifactAsync(BuildArtifact artifact, CancellationToken stoppingToken)
        {
            var tempFile = Path.GetTempFileName();
            // Save the file to disk
            using (var tempFileStream = new FileStream(tempFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                _logger.LogDebug(new EventId(0, "FetchingArtifact"), "Fetching artifact {ArtifactName} from {ArtifactUrl}.", artifact.Name, artifact.Resource.DownloadUrl);
                using var response = await _client.GetAsync(artifact.Resource.DownloadUrl, stoppingToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Unable to download artifact: {ArtifactName}. Response: {StatusCode}.", artifact.Name, response.StatusCode);
                }
                var stream = await response.Content.ReadAsStreamAsync();
                await stream.CopyToAsync(tempFileStream, stoppingToken);
                _logger.LogDebug(new EventId(0, "FetchedArtifact"), "Fetched artifact {ArtifactName} to {TempPath}", artifact.Name, tempFile);
            }
            return tempFile;
        }
    }
}
