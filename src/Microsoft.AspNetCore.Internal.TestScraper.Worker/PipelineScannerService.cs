using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Test.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Internal.TestScraper.Worker
{
    public class PipelineScannerService : BackgroundService
    {
        private readonly ILogger<PipelineScannerService> _logger;
        private readonly IOptions<PipelineScannerOptions> _options;
        private readonly VssConnection _connection;

        public PipelineScannerService(ILogger<PipelineScannerService> logger, IOptions<PipelineScannerOptions> options, VssConnection connection)
        {
            _logger = logger;
            _options = options;
            _connection = connection;
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
                foreach (var config in _options.Value.Pipelines)
                {
                    await ProcessPipelineAsync(config, stoppingToken);
                }
            }
        }

        private async Task ProcessPipelineAsync(PipelineConfig config, CancellationToken stoppingToken)
        {
            _logger.LogDebug("Scanning for new builds of {PipelineProject}/{PipelineName}", config.Project, config.Name);
            var buildClient = await _connection.GetClientAsync<BuildHttpClient>(stoppingToken);
            var testClient = await _connection.GetClientAsync<TestHttpClient>(stoppingToken);

            // Look up the definition ID
            // TODO: Cache
            var definitionRef = (await buildClient.GetDefinitionsAsync(project: config.Project, name: config.Name, cancellationToken: stoppingToken)).FirstOrDefault();
            if (definitionRef == null)
            {
                _logger.LogWarning("Could not find pipeline: {PipelineProject}/{PipelineName}", config.Project, config.Name);
                return;
            }

            var definition = await buildClient.GetDefinitionAsync(config.Project, definitionRef.Id, definitionRef.Revision, cancellationToken: stoppingToken);
            var repoId = definition.Repository.Id;

            foreach (var branch in config.Branches)
            {
                // Look up the most recent build (for now)
                var builds = await buildClient.GetBuildsAsync(
                    config.Project,
                    new[] { definition.Id },
                    branchName: $"refs/heads/{branch}",
                    repositoryId: definition.Repository.Id,
                    repositoryType: definition.Repository.Type,
                    top: 5,
                    cancellationToken: stoppingToken);

                foreach (var build in builds)
                {
                    await ProcessBuildAsync(build, stoppingToken);
                }
            }
        }

        private async Task ProcessBuildAsync(Build build, CancellationToken stoppingToken)
        {
            _logger.LogDebug("Scanning build #{BuildId} {BuildNumber} {SourceVersion}", build.Id, build.BuildNumber, build.SourceVersion);
        }
    }
}
