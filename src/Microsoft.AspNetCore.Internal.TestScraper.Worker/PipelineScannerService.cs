using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Internal.TestScraper.Formats;
using Microsoft.AspNetCore.Internal.TestScraper.Model;
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
        private readonly HttpClient _client;

        public PipelineScannerService(ILogger<PipelineScannerService> logger, IOptions<PipelineScannerOptions> options, VssConnection connection)
        {
            _logger = logger;
            _options = options;
            _connection = connection;
            _client = new HttpClient();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Get off the calling thread immediately
            await Task.Yield();

            var timerAwaitable = new TimerAwaitable(TimeSpan.Zero, _options.Value.ScanInterval);
            using var registration = stoppingToken.Register((state) => { ((TimerAwaitable)state).Stop(); }, timerAwaitable);
            _logger.LogInformation("Starting pipeline scanner loop. Interval: {ScanInterval}", _options.Value.ScanInterval);

            timerAwaitable.Start();

            while (await timerAwaitable)
            {
                _logger.LogInformation("Running scan loop");
                foreach (var config in _options.Value.Pipelines)
                {
                    using (_logger.BeginScope("Pipeline: {PipelineProject}/{PipelineName}", config.Project, config.Name))
                    {
                        await ProcessPipelineAsync(config, stoppingToken);
                    }
                }
            }
        }

        private async Task ProcessPipelineAsync(PipelineConfig config, CancellationToken stoppingToken)
        {
            var buildClient = await _connection.GetClientAsync<BuildHttpClient>(stoppingToken);

            // Look up the definition ID
            // TODO: Cache
            var definitionRef = (await buildClient.GetDefinitionsAsync(project: config.Project, name: config.Name, cancellationToken: stoppingToken)).FirstOrDefault();
            if (definitionRef == null)
            {
                _logger.LogWarning("Could not find pipeline: {PipelineProject}/{PipelineName}", config.Project, config.Name);
                return;
            }

            var definition = await buildClient.GetDefinitionAsync(config.Project, definitionRef.Id, definitionRef.Revision, cancellationToken: stoppingToken);

            var artifactRegexes = config.ArtifactPatterns.Select(p => new Regex(p));

            foreach (var branch in config.Branches)
            {
                await ProcessBranchAsync(config, buildClient, definition, artifactRegexes, branch, stoppingToken);
            }
        }

        private async Task ProcessBranchAsync(PipelineConfig config, BuildHttpClient buildClient, BuildDefinition definition, IEnumerable<Regex> artifactRegexes, string branch, CancellationToken stoppingToken)
        {
            using (_logger.BeginScope("Branch: {SourceBranch}", branch))
            {
                // Look up the most recent build (for now)
                _logger.LogDebug("Fetching builds...");
                var builds = await buildClient.GetBuildsAsync(
                    config.Project,
                    new[] { definition.Id },
                    branchName: $"refs/heads/{branch}",
                    repositoryId: definition.Repository.Id,
                    repositoryType: definition.Repository.Type,
                    top: 5,
                    cancellationToken: stoppingToken);
                _logger.LogDebug("Retrieved {BuildCount} builds.", builds.Count);

                foreach (var build in builds)
                {
                    using (_logger.BeginScope("Build {BuildId} #{BuildNumber}", build.Id, build.BuildNumber, build.SourceVersion))
                    {
                        try
                        {
                            await ProcessBuildAsync(config, build, buildClient, artifactRegexes, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing build: {BuildNumber}", build.BuildNumber);
                            // DO suppress the error. We want to continue processing other builds.
                        }
                    }
                }
            }
        }

        private async Task ProcessBuildAsync(PipelineConfig config, Build build, BuildHttpClient buildClient, IEnumerable<Regex> artifactRegexes, CancellationToken stoppingToken)
        {
            var artifacts = await buildClient.GetArtifactsAsync(config.Project, build.Id, cancellationToken: stoppingToken);
            var runs = new List<TestRun>();
            foreach (var artifact in artifacts)
            {
                if (artifactRegexes.Any(r => r.IsMatch(artifact.Name)))
                {
                    await ProcessArtifactAsync(config, buildClient, runs, artifact, stoppingToken);
                }
                else
                {
                    _logger.LogTrace("Skipping unmatched artifact {ArtifactName}", artifact.Name);
                }
            }
        }

        private async Task ProcessArtifactAsync(PipelineConfig config, BuildHttpClient buildClient, List<TestRun> runs, BuildArtifact artifact, CancellationToken stoppingToken)
        {
            // Stream the artifact down to disk
            var artifactFile = await SaveArtifactAsync(config, artifact, buildClient, stoppingToken);
            try
            {
                using var archive = ZipFile.OpenRead(artifactFile);
                foreach (var file in archive.Entries)
                {
                    if (file.Name.EndsWith(".xml"))
                    {
                        await ProcessTestResultFileAsync(runs, file, stoppingToken);
                    }
                    else
                    {
                        _logger.LogTrace("Skipping artifact file: {ArtifactFile}", artifact.Name, file);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing artifact: {ArtifactName}", artifact.Name);

                // We don't want to continue processing this build if an artifact failed. That way we can try again later.
                throw;
            }
            finally
            {
                try
                {
                    _logger.LogTrace("Deleting temporary file {TempFile} for Artifact {ArtifactName}", artifactFile, artifact.Name);
                    File.Delete(artifactFile);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error deleting temporary file: {TempFile}", artifactFile);
                }
            }
        }

        private async Task ProcessTestResultFileAsync(List<TestRun> runs, ZipArchiveEntry file, CancellationToken stoppingToken)
        {
            using (_logger.BeginScope("File: {TestResultsFile}", file.FullName))
            {
                try
                {
                    using var stream = file.Open();
                    var doc = await XDocument.LoadAsync(stream, LoadOptions.None, stoppingToken);
                    // Remove the extension but keep the full relative path to get the run name.
                    var runName = file.Name.Substring(file.Name.Length - 4);
                    _logger.LogTrace("Processing test run {TestRunName}", runName);
                    var assemblies = XUnitTestResultsFormat.Parse(doc);
                    runs.Add(new TestRun(runName, assemblies));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing test result file: {TestResultsFile}", file.FullName);
                }
            }
        }

        private async Task<string> SaveArtifactAsync(PipelineConfig config, BuildArtifact artifact, BuildHttpClient buildClient, CancellationToken stoppingToken)
        {
            var tempFile = Path.GetTempFileName();
            // Save the file to disk
            using (var tempFileStream = new FileStream(tempFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                _logger.LogDebug("Fetching artifact {ArtifactName} from {ArtifactUrl}.", artifact.Name, artifact.Resource.DownloadUrl);
                using var response = await _client.GetAsync(artifact.Resource.DownloadUrl, stoppingToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Unable to download artifact: {ArtifactName}. Response: {StatusCode}.", artifact.Name, response.StatusCode);
                }
                var stream = await response.Content.ReadAsStreamAsync();
                await stream.CopyToAsync(tempFileStream, stoppingToken);
                _logger.LogDebug("Downloaded artifact {ArtifactName} to {TempPath}", artifact.Name, tempFile);
            }
            return tempFile;
        }
    }
}
