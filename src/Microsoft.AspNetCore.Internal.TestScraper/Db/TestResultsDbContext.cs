using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Pipeline>(pipeline =>
            {
                pipeline.HasIndex(p => new { p.Project, p.AzDoId })
                    .IsUnique();
            });

            modelBuilder.Entity<PipelineBuild>(pipelineBuild =>
            {
                pipelineBuild.HasIndex(b => new { b.PipelineId, b.AzDoId })
                    .IsUnique();
                pipelineBuild.Property(b => b.Result)
                    .HasConversion(new EnumToStringConverter<PipelineBuildResult>());
                pipelineBuild.Property(b => b.Status)
                    .HasConversion(new EnumToStringConverter<SyncStatus>());
            });

            modelBuilder.Entity<PipelineTestResult>(pipelineTestResult =>
            {
                pipelineTestResult.Property(r => r.Result)
                    .HasConversion(new EnumToStringConverter<TestResultKind>());
            });
        }
    }
}
