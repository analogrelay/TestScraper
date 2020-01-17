using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Microsoft.AspNetCore.Internal.TestScraper.Db
{
    public class TestResultsDbContext : DbContext
    {
        public static readonly int StringMaxLength = 1024;

        public TestResultsDbContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<Pipeline> Pipelines { get; set; }
        public DbSet<PipelineBuild> Builds { get; set; }
        public DbSet<PipelineTestResult> TestResults { get; set; }
        public DbSet<PipelineTestAssembly> TestAssemblies { get; set; }
        public DbSet<PipelineTestCollection> TestCollections { get; set; }
        public DbSet<PipelineTestMethod> TestMethods { get; set; }
        public DbSet<PipelineTestCase> TestCases { get; set; }
        public DbSet<PipelineTestRun> TestRuns { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Pipeline>(x =>
            {
                x.HasIndex(p => new { p.Project, p.Name })
                    .IsUnique();
                x.HasIndex(p => new { p.Project, p.AzDoId })
                    .IsUnique();
            });

            modelBuilder.Entity<PipelineBuild>(x =>
            {
                x.HasIndex(b => new { b.PipelineId, b.AzDoId })
                    .IsUnique();
                x.Property(b => b.Result)
                    .HasConversion(new EnumToStringConverter<PipelineBuildResult>());
                x.Property(b => b.SyncStatus)
                    .HasConversion(new EnumToStringConverter<SyncStatus>());
            });

            modelBuilder.Entity<PipelineTestResult>(x =>
            {
                x.Property(r => r.Result)
                    .HasConversion(new EnumToStringConverter<TestResultKind>());
            });

            modelBuilder.Entity<PipelineTestAssembly>(x =>
            {
                x.Property(m => m.Name).HasMaxLength(450);
                x.HasIndex(a => new { a.Name }).IsUnique();
            });

            modelBuilder.Entity<PipelineTestCollection>(x =>
            {
                x.Property(m => m.Name).HasMaxLength(StringMaxLength);
                x.HasIndex(c => new { c.AssemblyId, c.Name }).IsUnique();
            });

            modelBuilder.Entity<PipelineTestMethod>(x =>
            {
                x.Property(m => m.Type).HasMaxLength(StringMaxLength);
                x.Property(m => m.Name).HasMaxLength(StringMaxLength);
                x.HasIndex(m => new { m.CollectionId, m.Type, m.Name }).IsUnique();
            });

            modelBuilder.Entity<PipelineTestCase>(x =>
            {
                x.Property(c => c.Name).HasMaxLength(StringMaxLength);
                x.HasIndex(c => new { c.MethodId, c.Name }).IsUnique();
            });

            modelBuilder.Entity<PipelineTestRun>(x =>
            {
                x.Property(c => c.Name).HasMaxLength(StringMaxLength);
                x.HasIndex(r => r.Name)
                    .IsUnique();
            });
        }
    }
}
