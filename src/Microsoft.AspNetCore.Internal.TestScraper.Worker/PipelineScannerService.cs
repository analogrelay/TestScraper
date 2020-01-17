using System;
using System.Collections.Generic;
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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.AspNetCore.Internal.TestScraper.Worker
{
    public class PipelineScannerService : BackgroundService
    {
        private readonly ILogger<PipelineScannerService> _logger;
        private readonly IOptions<PipelineScannerOptions> _options;
        private readonly VssConnection _connection;
        private readonly TestResultsDbContext _db;
        private readonly HttpClient _client;

        public PipelineScannerService(ILogger<PipelineScannerService> logger, IOptions<PipelineScannerOptions> options, VssConnection connection, TestResultsDbContext dbContext)
        {
            _logger = logger;
            _options = options;
            _connection = connection;
            _db = dbContext;
            _client = new HttpClient();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Get off the calling thread immediately
            await Task.Yield();

            _logger.LogInformation(new EventId(0, "StartingScanner"), "Starting pipeline scanner loop. Interval: {ScanInterval}.", _options.Value.ScanInterval);

            while (!stoppingToken.IsCancellationRequested)
            {
                var minFinishTime = DateTime.Now.Subtract(TimeSpan.FromDays(_options.Value.MinFinishTimeInDaysAgo));

                _logger.LogInformation(new EventId(0, "RunningScanLoop"), "Running scan loop.");
                foreach (var config in _options.Value.Pipelines)
                {
                    using (_logger.BeginScope("Pipeline: {PipelineProject}/{PipelineName}", config.Project, config.Name))
                    {
                        _logger.LogInformation(new EventId(0, "ProcessingPipeline"), "Processing pipeline {PipelineProject}/{PipelineName}...", config.Project, config.Name);
                        await ProcessPipelineAsync(config, minFinishTime, stoppingToken);
                        _logger.LogInformation(new EventId(0, "ProcessedPipeline"), "Processed pipeline {PipelineProject}/{PipelineName}.", config.Project, config.Name);
                    }
                }

                _logger.LogInformation("Sleeping for {ScanInterval}...", _options.Value.ScanInterval);
                await Task.Delay(_options.Value.ScanInterval);
            }

            _logger.LogInformation(new EventId(0, "Stopped"), "Service Stopped.");
        }

        private async Task ProcessPipelineAsync(PipelineConfig config, DateTime minFinishTime, CancellationToken stoppingToken)
        {
            var buildClient = await _connection.GetClientAsync<BuildHttpClient>(stoppingToken);

            // Look up the definition ID
            // TODO: Cache
            var definitionRef = (await buildClient.GetDefinitionsAsync(project: config.Project, name: config.Name, cancellationToken: stoppingToken)).FirstOrDefault();
            if (definitionRef == null)
            {
                _logger.LogWarning(new EventId(0, "CouldNotFindPipeline"), "Could not find pipeline: {PipelineProject}/{PipelineName}", config.Project, config.Name);
                return;
            }

            var definition = await buildClient.GetDefinitionAsync(config.Project, definitionRef.Id, definitionRef.Revision, cancellationToken: stoppingToken);

            // Ensure the Pipeline exists in the database
            var dbPipeline = await _db.Pipelines.FirstOrDefaultAsync(p => p.Project == config.Project && p.AzDoId == definition.Id);
            if (dbPipeline == null)
            {
                dbPipeline = new Pipeline()
                {
                    AzDoId = definition.Id,
                    Name = definition.Name,
                    Project = definition.Project.Name,
                    WebUrl = definition.Links.Links.TryGetValue("web", out var link) ? ((ReferenceLink)link).Href : null,
                };
                _db.Pipelines.Add(dbPipeline);
                await _db.SaveChangesAsync();
                _logger.LogDebug(new EventId(0, "CreatedDbPipeline"), "Created Pipeline {PipelineProject}/{PipelineName} in Database", config.Project, config.Name);
            }

            var artifactRegexes = config.ArtifactPatterns.Select(p => new Regex(p));

            foreach (var branch in config.Branches)
            {
                using (_logger.BeginScope("Branch: {SourceBranch}", branch))
                {
                    _logger.LogInformation(new EventId(0, "ProcessingBranch"), "Processing builds in branch {BranchName}...", branch);
                    await ProcessBranchAsync(config, buildClient, definition, artifactRegexes, branch, minFinishTime, dbPipeline, stoppingToken);
                    _logger.LogInformation(new EventId(0, "ProcessedBranch"), "Processed builds in branch {BranchName}.", branch);
                }
            }
        }

        private async Task ProcessBranchAsync(PipelineConfig config, BuildHttpClient buildClient, BuildDefinition definition, IEnumerable<Regex> artifactRegexes, string branch, DateTime minFinishTime, Pipeline dbPipeline, CancellationToken stoppingToken)
        {
            // Look up the most recent build (for now)
            _logger.LogDebug(new EventId(0, "FetchingBuilds"), "Fetching builds for {PipelineProject}/{PipelineName} in {BranchName}...", config.Project, config.Name, branch);
            var builds = await buildClient.GetBuildsAsync(
                config.Project,
                new[] { definition.Id },
                branchName: $"refs/heads/{branch}",
                repositoryId: definition.Repository.Id,
                repositoryType: definition.Repository.Type,
                statusFilter: BuildStatus.Completed,
                minFinishTime: minFinishTime,
                cancellationToken: stoppingToken);
            _logger.LogInformation(new EventId(0, "FetchedBuilds"), "Fetched {BuildCount} builds.", builds.Count);

            foreach (var build in builds)
            {
                using (_logger.BeginScope("Build {BuildId} #{BuildNumber}", build.Id, build.BuildNumber, build.SourceVersion))
                {
                    // Check if this build has been synced
                    var dbBuild = await _db.Builds.FirstOrDefaultAsync(b => b.PipelineId == dbPipeline.Id && b.AzDoId == build.Id);
                    var syncAttempts = dbBuild?.SyncAttempts ?? 0;
                    if(dbBuild != null && (dbBuild.Status == SyncStatus.Cancelled || dbBuild.Status == SyncStatus.Failed) && syncAttempts < _options.Value.MaxSyncAttempts)
                    {
                        // Delete the build and try again
                        // This should cascade remove all the test results (or at least orphan their BuildIds).
                        _logger.LogInformation(new EventId(0, "AttemptingResyncBuild"), "Attempting resync of build #{BuildNumber}. Deleting existing build record...", build.BuildNumber);
                        _db.Builds.Remove(dbBuild);
                        await _db.SaveChangesAsync();

                        dbBuild = null;
                        syncAttempts += 1;
                    }

                    // TODO: Retry of unsynced builds?
                    if (dbBuild == null)
                    {
                        _logger.LogInformation(new EventId(0, "ProcessingBuild"), "Processing build #{BuildNumber}...", build.BuildNumber);
                        var sw = Stopwatch.StartNew();
                        await ProcessBuildAsync(config, build, buildClient, artifactRegexes, dbPipeline, syncAttempts, stoppingToken);
                        _logger.LogInformation(new EventId(0, "ProcessedBuild"), "Processed build #{BuildNumber} in {Elapsed}.", build.BuildNumber, sw.Elapsed);
                    }
                    else
                    {
                        _logger.LogDebug(new EventId(0, "SkippingBuild"), "Skipping already-synced build #{BuildNumber}.", build.BuildNumber);
                    }
                }
            }
        }

        private async Task ProcessBuildAsync(PipelineConfig config, Build build, BuildHttpClient buildClient, IEnumerable<Regex> artifactRegexes, Pipeline dbPipeline, int syncAttempts, CancellationToken stoppingToken)
        {
            // Create a record for the build, but marked as incomplete
            var dbBuild = new PipelineBuild()
            {
                AzDoId = build.Id,
                PipelineId = dbPipeline.Id,
                BuildNumber = build.BuildNumber,
                SourceBranch = build.SourceBranch,
                SourceVersion = build.SourceVersion,
                Status = SyncStatus.InProgress,
                SyncAttempts = syncAttempts,
                WebUrl = build.Links.Links.TryGetValue("web", out var link) ? ((ReferenceLink)link).Href : null,
                Result = build.Result switch
                {
                    BuildResult.Succeeded => PipelineBuildResult.Succeeded,
                    BuildResult.PartiallySucceeded => PipelineBuildResult.PartiallySucceeded,
                    BuildResult.Failed => PipelineBuildResult.Failed,
                    BuildResult.Canceled => PipelineBuildResult.Canceled,
                    _ => null
                },
                StartTimeUtc = build.StartTime,
                CompletedTimeUtc = build.FinishTime,
                SyncStartedUtc = DateTime.UtcNow,
                SyncCompleteUtc = null, // We'll set this when we're done
            };
            _db.Builds.Add(dbBuild);
            await _db.SaveChangesAsync();
            _logger.LogDebug(new EventId(0, "CreatedDbBuild"), "Created Build Record in Database");

            try
            {
                var artifacts = await buildClient.GetArtifactsAsync(config.Project, build.Id, cancellationToken: stoppingToken);
                foreach (var artifact in artifacts)
                {
                    if (artifactRegexes.Any(r => r.IsMatch(artifact.Name)))
                    {
                        _logger.LogDebug(new EventId(0, "ProcessingArtifact"), "Processing artifact {ArtifactName}...", artifact.Name);
                        await ProcessArtifactAsync(config, buildClient, artifact, dbBuild, stoppingToken);
                        _logger.LogDebug(new EventId(0, "ProcessedArtifact"), "Processed artifact {ArtifactName}.", artifact.Name);
                    }
                    else
                    {
                        _logger.LogTrace(new EventId(0, "SkippingArtifact"), "Skipping unmatched artifact {ArtifactName}", artifact.Name);
                    }
                }

                dbBuild.Status = SyncStatus.Complete;
                dbBuild.SyncCompleteUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                _logger.LogDebug(new EventId(0, "SavedBuild"), "Saved all results from build #{BuildNumber}", build.BuildNumber);

                if (_options.Value.TagScrapedBuilds)
                {
                    // Tag the build as well
                    await buildClient.AddBuildTagAsync(config.Project, build.Id, "aspnet-tests-scraped");
                    _logger.LogDebug(new EventId(0, "TaggedBuild"), "Tagged AzDO Build.");
                }
            }
            catch (OperationCanceledException)
            {
                dbBuild.Status = SyncStatus.Cancelled;
                await _db.SaveChangesAsync();
                throw;
            }
            catch (Exception ex)
            {
                dbBuild.Status = SyncStatus.Failed;
                await _db.SaveChangesAsync();
                _logger.LogError(new EventId(0, "ErrorProcessingBuild"), ex, "Error Processing Build #{BuildNumber}.", build.BuildNumber);
            }
        }

        private async Task ProcessArtifactAsync(PipelineConfig config, BuildHttpClient buildClient, BuildArtifact artifact, PipelineBuild dbBuild, CancellationToken stoppingToken)
        {
            // Stream the artifact down to disk
            var artifactFile = await SaveArtifactAsync(config, artifact, buildClient, stoppingToken);
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
                            await ProcessTestResultFileAsync(file, dbBuild, stoppingToken);
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

        private async Task ProcessTestResultFileAsync(ZipArchiveEntry file, PipelineBuild dbBuild, CancellationToken stoppingToken)
        {
            try
            {
                using var stream = file.Open();
                var doc = await XDocument.LoadAsync(stream, LoadOptions.None, stoppingToken);
                // Remove the extension but keep the full relative path to get the run name.
                var runName = file.FullName.Substring(0, file.FullName.Length - 4);
                _logger.LogTrace(new EventId(0, "ParsingTestResults"), "Parsing test results.");
                var assemblies = XUnitTestResultsFormat.Parse(doc);

                _logger.LogTrace(new EventId(0, "ProcessingTestResults"), "Processing test results...");
                foreach (var assembly in assemblies)
                {
                    foreach (var collection in assembly.Collections)
                    {
                        foreach (var result in collection.Results)
                        {
                            var dbResult = new PipelineTestResult()
                            {
                                BuildId = dbBuild.Id,
                                Run = runName,
                                Assembly = assembly.Name,
                                Collection = collection.Name,
                                Type = result.Type,
                                Method = result.Method,
                                FullName = result.Name,
                                Traits = string.Join(";", result.Traits.Select(t => $"{t.Name}={t.Value}")),
                                Flaky = result.Traits.Any(t => t.Name.StartsWith("Flaky:")),
                                FlakyOn = string.Join(";", result.Traits.Where(t => t.Name.StartsWith("Flaky:")).Select(t => t.Name.Substring(6))),
                            };
                            switch (result.Outcome)
                            {
                                case FailureTestOutcome f:
                                    dbResult.Result = TestResultKind.Fail;
                                    dbResult.FailureMessage = f.Message;
                                    dbResult.FailureStackTrace = f.StackTrace;
                                    break;
                                case SkippedTestOutcome s:
                                    dbResult.Result = TestResultKind.Skip;
                                    dbResult.SkipReason = s.Reason;
                                    break;
                                case SuccessfulTestOutcome _:
                                    dbResult.Result = TestResultKind.Pass;
                                    break;
                                default:
                                    dbResult.Result = TestResultKind.Unknown;
                                    break;
                            }
                            _db.TestResults.Add(dbResult);
                        }
                    }
                }

                // Save results per-file. The master "SyncCompleteUtc" flag will tell consumers if this is a fully-synced build.
                _logger.LogTrace(new EventId(0, "SavingTestResults"), "Saving test results to database...");
                await _db.SaveChangesAsync();
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

        private async Task<string> SaveArtifactAsync(PipelineConfig config, BuildArtifact artifact, BuildHttpClient buildClient, CancellationToken stoppingToken)
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
