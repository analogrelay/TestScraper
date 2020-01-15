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
                            await ProcessBuildAsync(config, build, buildClient, artifactRegexes, stoppingToken);
                        }
                    }
                }
            }
        }

        private async Task ProcessBuildAsync(PipelineConfig config, Build build, BuildHttpClient buildClient, IEnumerable<Regex> artifactRegexes, CancellationToken stoppingToken)
        {
            var artifacts = await buildClient.GetArtifactsAsync(config.Project, build.Id, cancellationToken: stoppingToken);
            foreach (var artifact in artifacts)
            {
                if (artifactRegexes.Any(r => r.IsMatch(artifact.Name)))
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
                                using(_logger.BeginScope("File: {TestResultsFile}", file.FullName))
                                {
                                    using (var stream = file.Open())
                                    {
                                        var doc = await XDocument.LoadAsync(stream, LoadOptions.None, stoppingToken);
                                        await ProcessTestResultsAsync(doc, stoppingToken);
                                    }
                                }
                            }
                            else
                            {
                            }
                        }
                    }
                    finally
                    {
                    }
                }
                else
                {
                    _logger.LogTrace("Skipping unmatched artifact {ArtifactName}", artifact.Name);
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
