using Microsoft.AspNetCore.Internal.TestScraper.Db;
using Microsoft.AspNetCore.Internal.TestScraper.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Internal.TestScraper.Worker
{
    /// <summary>
    /// Long-lived context storing cached database results for a test results sync. NOT THREAD SAFE.
    /// </summary>
    internal class TestResultsScrapeContext
    {
        private Dictionary<(string, string), Pipeline> _pipelineCache = new Dictionary<(string, string), Pipeline>();

        public TestResultsDbContext Db { get; }
        public BuildHttpClient BuildClient { get; }
        public PipelineScannerOptions Options { get; }
        public ILogger Logger { get; }
        public DateTime MinFinishTime { get; }

        public TestResultsScrapeContext(TestResultsDbContext db, BuildHttpClient buildClient, PipelineScannerOptions options, ILogger logger)
        {
            Db = db;
            BuildClient = buildClient;

            Options = options;
            Logger = logger;
            MinFinishTime = DateTime.UtcNow.Subtract(TimeSpan.FromDays(options.MinFinishTimeInDaysAgo));
        }

        public async Task<PipelineScrapeContext> CreatePipelineSyncContextAsync(PipelineConfig config, CancellationToken cancellationToken = default)
        {
            if (!_pipelineCache.TryGetValue((config.Project, config.Name), out var dbPipeline))
            {
                dbPipeline = await Db.Pipelines.FirstOrDefaultAsync(p => p.Project == config.Project && p.Name == config.Name);
                if (dbPipeline == null)
                {
                    var definitionRef = (await BuildClient.GetDefinitionsAsync(project: config.Project, name: config.Name, cancellationToken: cancellationToken)).FirstOrDefault();
                    if (definitionRef == null)
                    {
                        // We'll store 'null' in the cache to cache the negative ack.
                        Logger.LogWarning(new EventId(0, "CouldNotFindPipeline"), "Could not find pipeline: {PipelineProject}/{PipelineName}", config.Project, config.Name);
                    }
                    else
                    {
                        var definition = await BuildClient.GetDefinitionAsync(config.Project, definitionRef.Id, definitionRef.Revision, cancellationToken: cancellationToken);

                        // Ensure the Pipeline exists in the database
                        dbPipeline = await Db.Pipelines.FirstOrDefaultAsync(p => p.Project == config.Project && p.AzDoId == definition.Id);
                        if (dbPipeline == null)
                        {
                            dbPipeline = new Pipeline()
                            {
                                AzDoId = definition.Id,
                                Name = definition.Name,
                                Project = definition.Project.Name,
                                RepositoryId = definition.Repository.Id,
                                RepositoryType = definition.Repository.Type,
                                WebUrl = definition.Links.Links.TryGetValue("web", out var link) ? ((ReferenceLink)link).Href : null,
                            };
                            Db.Pipelines.Add(dbPipeline);
                            await Db.SaveChangesAsync();
                            Logger.LogDebug(new EventId(0, "CreatedDbPipeline"), "Created Pipeline {PipelineProject}/{PipelineName} in Database", config.Project, config.Name);
                        }
                    }
                }
                _pipelineCache[(config.Project, config.Name)] = dbPipeline;
            }
            var artifactRegexes = config.ArtifactPatterns.Select(p => new Regex(p));
            return new PipelineScrapeContext(this, dbPipeline, artifactRegexes);

        }

        private string TruncateIfNecessary(string valueType, string name)
        {
            if (name.Length > TestResultsDbContext.StringMaxLength)
            {
                Logger.LogWarning(new EventId(0, $"Truncating{valueType}"), "Truncating {ValueType} name because it was longer than {MaxLength}: {UntruncatedValue}", valueType, TestResultsDbContext.StringMaxLength, name);
                return name.Substring(0, TestResultsDbContext.StringMaxLength - 3) + "...";
            }
            return name;
        }
    }
}
