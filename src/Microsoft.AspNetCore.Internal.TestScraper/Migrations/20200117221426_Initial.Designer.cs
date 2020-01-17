﻿// <auto-generated />
using System;
using Microsoft.AspNetCore.Internal.TestScraper.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Microsoft.AspNetCore.Internal.TestScraper.Migrations
{
    [DbContext(typeof(TestResultsDbContext))]
    [Migration("20200117221426_Initial")]
    partial class Initial
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "3.1.1")
                .HasAnnotation("Relational:MaxIdentifierLength", 128)
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("Microsoft.AspNetCore.Internal.TestScraper.Db.Pipeline", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>("AzDoId")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("Project")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("RepositoryId")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("RepositoryType")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("WebUrl")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.HasIndex("Project", "AzDoId")
                        .IsUnique()
                        .HasFilter("[Project] IS NOT NULL");

                    b.HasIndex("Project", "Name")
                        .IsUnique()
                        .HasFilter("[Project] IS NOT NULL AND [Name] IS NOT NULL");

                    b.ToTable("Pipelines");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Internal.TestScraper.Db.PipelineBuild", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>("AzDoId")
                        .HasColumnType("int");

                    b.Property<string>("BuildNumber")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime?>("CompletedTimeUtc")
                        .HasColumnType("datetime2");

                    b.Property<int>("PipelineId")
                        .HasColumnType("int");

                    b.Property<string>("Result")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("SourceBranch")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("SourceVersion")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime?>("StartTimeUtc")
                        .HasColumnType("datetime2");

                    b.Property<int>("SyncAttempts")
                        .HasColumnType("int");

                    b.Property<DateTime?>("SyncCompleteUtc")
                        .HasColumnType("datetime2");

                    b.Property<DateTime?>("SyncStartedUtc")
                        .HasColumnType("datetime2");

                    b.Property<string>("SyncStatus")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("WebUrl")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.HasIndex("PipelineId", "AzDoId")
                        .IsUnique();

                    b.ToTable("Builds");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Internal.TestScraper.Db.PipelineTestAssembly", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(450)")
                        .HasMaxLength(450);

                    b.HasKey("Id");

                    b.HasIndex("Name")
                        .IsUnique()
                        .HasFilter("[Name] IS NOT NULL");

                    b.ToTable("TestAssemblies");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Internal.TestScraper.Db.PipelineTestCase", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>("MethodId")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(1024)")
                        .HasMaxLength(1024);

                    b.HasKey("Id");

                    b.HasIndex("MethodId", "Name")
                        .IsUnique()
                        .HasFilter("[Name] IS NOT NULL");

                    b.ToTable("TestCases");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Internal.TestScraper.Db.PipelineTestCollection", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>("AssemblyId")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(1024)")
                        .HasMaxLength(1024);

                    b.HasKey("Id");

                    b.HasIndex("AssemblyId", "Name")
                        .IsUnique()
                        .HasFilter("[Name] IS NOT NULL");

                    b.ToTable("TestCollections");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Internal.TestScraper.Db.PipelineTestMethod", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>("CollectionId")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(1024)")
                        .HasMaxLength(1024);

                    b.Property<string>("Type")
                        .HasColumnType("nvarchar(1024)")
                        .HasMaxLength(1024);

                    b.HasKey("Id");

                    b.HasIndex("CollectionId", "Type", "Name")
                        .IsUnique()
                        .HasFilter("[Type] IS NOT NULL AND [Name] IS NOT NULL");

                    b.ToTable("TestMethods");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Internal.TestScraper.Db.PipelineTestResult", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>("BuildId")
                        .HasColumnType("int");

                    b.Property<int>("CaseId")
                        .HasColumnType("int");

                    b.Property<bool>("Quarantined")
                        .HasColumnType("bit");

                    b.Property<string>("QuarantinedOn")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Result")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("RunId")
                        .HasColumnType("int");

                    b.Property<string>("Traits")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.HasIndex("BuildId");

                    b.HasIndex("CaseId");

                    b.HasIndex("RunId");

                    b.ToTable("TestResults");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Internal.TestScraper.Db.PipelineTestRun", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(1024)")
                        .HasMaxLength(1024);

                    b.HasKey("Id");

                    b.HasIndex("Name")
                        .IsUnique()
                        .HasFilter("[Name] IS NOT NULL");

                    b.ToTable("TestRuns");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Internal.TestScraper.Db.PipelineBuild", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Internal.TestScraper.Db.Pipeline", "Pipeline")
                        .WithMany("Builds")
                        .HasForeignKey("PipelineId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Internal.TestScraper.Db.PipelineTestCase", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Internal.TestScraper.Db.PipelineTestMethod", "Method")
                        .WithMany("Cases")
                        .HasForeignKey("MethodId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Internal.TestScraper.Db.PipelineTestCollection", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Internal.TestScraper.Db.PipelineTestAssembly", "Assembly")
                        .WithMany("Collections")
                        .HasForeignKey("AssemblyId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Internal.TestScraper.Db.PipelineTestMethod", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Internal.TestScraper.Db.PipelineTestCollection", "Collection")
                        .WithMany("Methods")
                        .HasForeignKey("CollectionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Internal.TestScraper.Db.PipelineTestResult", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Internal.TestScraper.Db.PipelineBuild", "Build")
                        .WithMany("TestResults")
                        .HasForeignKey("BuildId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Microsoft.AspNetCore.Internal.TestScraper.Db.PipelineTestCase", "Case")
                        .WithMany()
                        .HasForeignKey("CaseId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Microsoft.AspNetCore.Internal.TestScraper.Db.PipelineTestRun", "Run")
                        .WithMany()
                        .HasForeignKey("RunId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}
