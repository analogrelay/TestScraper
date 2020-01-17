using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Microsoft.AspNetCore.Internal.TestScraper.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pipelines",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AzDoId = table.Column<int>(nullable: false),
                    WebUrl = table.Column<string>(nullable: true),
                    Project = table.Column<string>(nullable: true),
                    Name = table.Column<string>(nullable: true),
                    RepositoryId = table.Column<string>(nullable: true),
                    RepositoryType = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pipelines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestAssemblies",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestAssemblies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestRuns",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Builds",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AzDoId = table.Column<int>(nullable: false),
                    WebUrl = table.Column<string>(nullable: true),
                    PipelineId = table.Column<int>(nullable: false),
                    BuildNumber = table.Column<string>(nullable: true),
                    SourceBranch = table.Column<string>(nullable: true),
                    SourceVersion = table.Column<string>(nullable: true),
                    SyncStatus = table.Column<string>(nullable: false),
                    SyncAttempts = table.Column<int>(nullable: false),
                    StartTimeUtc = table.Column<DateTime>(nullable: true),
                    CompletedTimeUtc = table.Column<DateTime>(nullable: true),
                    SyncStartedUtc = table.Column<DateTime>(nullable: true),
                    SyncCompleteUtc = table.Column<DateTime>(nullable: true),
                    Result = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Builds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Builds_Pipelines_PipelineId",
                        column: x => x.PipelineId,
                        principalTable: "Pipelines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestCollections",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssemblyId = table.Column<int>(nullable: false),
                    Name = table.Column<string>(maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCollections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestCollections_TestAssemblies_AssemblyId",
                        column: x => x.AssemblyId,
                        principalTable: "TestAssemblies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestMethods",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollectionId = table.Column<int>(nullable: false),
                    Type = table.Column<string>(maxLength: 1024, nullable: true),
                    Name = table.Column<string>(maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestMethods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestMethods_TestCollections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "TestCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestCases",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MethodId = table.Column<int>(nullable: false),
                    Name = table.Column<string>(maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestCases_TestMethods_MethodId",
                        column: x => x.MethodId,
                        principalTable: "TestMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestResults",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BuildId = table.Column<int>(nullable: false),
                    RunId = table.Column<int>(nullable: false),
                    CaseId = table.Column<int>(nullable: false),
                    Result = table.Column<string>(nullable: false),
                    Traits = table.Column<string>(nullable: true),
                    Quarantined = table.Column<bool>(nullable: false),
                    QuarantinedOn = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestResults_Builds_BuildId",
                        column: x => x.BuildId,
                        principalTable: "Builds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TestResults_TestCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "TestCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TestResults_TestRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "TestRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Builds_PipelineId_AzDoId",
                table: "Builds",
                columns: new[] { "PipelineId", "AzDoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pipelines_Project_AzDoId",
                table: "Pipelines",
                columns: new[] { "Project", "AzDoId" },
                unique: true,
                filter: "[Project] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Pipelines_Project_Name",
                table: "Pipelines",
                columns: new[] { "Project", "Name" },
                unique: true,
                filter: "[Project] IS NOT NULL AND [Name] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TestAssemblies_Name",
                table: "TestAssemblies",
                column: "Name",
                unique: true,
                filter: "[Name] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_MethodId_Name",
                table: "TestCases",
                columns: new[] { "MethodId", "Name" },
                unique: true,
                filter: "[Name] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TestCollections_AssemblyId_Name",
                table: "TestCollections",
                columns: new[] { "AssemblyId", "Name" },
                unique: true,
                filter: "[Name] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TestMethods_CollectionId_Type_Name",
                table: "TestMethods",
                columns: new[] { "CollectionId", "Type", "Name" },
                unique: true,
                filter: "[Type] IS NOT NULL AND [Name] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TestResults_BuildId",
                table: "TestResults",
                column: "BuildId");

            migrationBuilder.CreateIndex(
                name: "IX_TestResults_CaseId",
                table: "TestResults",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_TestResults_RunId",
                table: "TestResults",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_TestRuns_Name",
                table: "TestRuns",
                column: "Name",
                unique: true,
                filter: "[Name] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TestResults");

            migrationBuilder.DropTable(
                name: "Builds");

            migrationBuilder.DropTable(
                name: "TestCases");

            migrationBuilder.DropTable(
                name: "TestRuns");

            migrationBuilder.DropTable(
                name: "Pipelines");

            migrationBuilder.DropTable(
                name: "TestMethods");

            migrationBuilder.DropTable(
                name: "TestCollections");

            migrationBuilder.DropTable(
                name: "TestAssemblies");
        }
    }
}
