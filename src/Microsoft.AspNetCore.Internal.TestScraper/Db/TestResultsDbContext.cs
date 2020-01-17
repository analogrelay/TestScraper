using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Microsoft.AspNetCore.Internal.TestScraper.Db
{
    public class TestResultsDbContext: DbContext
    {
        public TestResultsDbContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<Pipeline> Pipelines { get; set; }
        public DbSet<PipelineBuild> Builds { get; set; }
        public DbSet<PipelineTestResult> TestResults { get; set; }
    }
}
