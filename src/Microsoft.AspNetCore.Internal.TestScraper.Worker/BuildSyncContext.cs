using Microsoft.AspNetCore.Internal.TestScraper.Db;
using Microsoft.TeamFoundation.Build.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Internal.TestScraper.Worker
{
    internal class BuildSyncContext
    {
        public BuildSyncContext(PipelineScrapeContext pipelineContext, PipelineBuild dbBuild, TestResultsDbContext db)
        {
            PipelineContext = pipelineContext;
            DbBuild = dbBuild;
            Db = db;
        }

        public PipelineScrapeContext PipelineContext { get; }
        public PipelineBuild DbBuild { get; }
        public TestResultsDbContext Db { get; }

        public async Task<IEnumerable<BuildArtifact>> GetArtifactsAsync(CancellationToken cancellationToken)
        {
            var allArtifacts = await PipelineContext.ResultsContext.BuildClient.GetArtifactsAsync(PipelineContext.DbPipeline.Project, DbBuild.AzDoId, cancellationToken);
            return allArtifacts.Where(a => PipelineContext.ArtifactRegexes.Any(r => r.IsMatch(a.Name)));
        }
    }
}