using Microsoft.AspNetCore.Internal.TestScraper.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Internal.TestScraper.Worker
{
    internal class PipelineScrapeContext
    {
        public PipelineScrapeContext(TestResultsScrapeContext resultsContext, Pipeline dbPipeline, IEnumerable<Regex> artifactRegexes)
        {
            ResultsContext = resultsContext;
            DbPipeline = dbPipeline;
            ArtifactRegexes = artifactRegexes;
        }

        public TestResultsScrapeContext ResultsContext { get; }
        public Pipeline DbPipeline { get; }
        public IEnumerable<Regex> ArtifactRegexes { get; }

        public async Task<IReadOnlyList<PipelineBuild>> GetBuildsAsync(string branch, CancellationToken stoppingToken)
        {
            var builds = await ResultsContext.BuildClient.GetBuildsAsync(
                DbPipeline.Project,
                new[] { DbPipeline.AzDoId },
                branchName: $"refs/heads/{branch}",
                repositoryId: DbPipeline.RepositoryId,
                repositoryType: DbPipeline.RepositoryType,
                statusFilter: BuildStatus.Completed,
                minFinishTime: ResultsContext.MinFinishTime,
                cancellationToken: stoppingToken);

            var dbBuilds = new List<PipelineBuild>();
            foreach(var build in builds)
            {
                var dbBuild = await ResultsContext.Db.Builds.FirstOrDefaultAsync(b => b.PipelineId == DbPipeline.Id && b.AzDoId == build.Id);
                if(dbBuild == null)
                {
                    dbBuild = new PipelineBuild()
                    {
                        AzDoId = build.Id,
                        PipelineId = DbPipeline.Id,
                        BuildNumber = build.BuildNumber,
                        SourceBranch = build.SourceBranch,
                        SourceVersion = build.SourceVersion,
                        SyncStatus = SyncStatus.NotStarted,
                        SyncAttempts = 0,
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
                    };
                    ResultsContext.Db.Builds.Add(dbBuild);
                }
                else if(dbBuild.SyncStatus != SyncStatus.NotStarted)
                {
                    // Ignore entries that are in-progress or complete.
                    ResultsContext.Logger.LogDebug(new EventId(0, "SkippingBuild"), "Skipping Build {BuildNumber}, it's status is {SyncStatus}", dbBuild.BuildNumber, dbBuild.SyncStatus);
                    continue;
                }
                dbBuilds.Add(dbBuild);
            }
            await ResultsContext.Db.SaveChangesAsync();
            return dbBuilds;
        }
    }
}