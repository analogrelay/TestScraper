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
                    Name = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pipelines", x => x.Id);
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
                    Status = table.Column<string>(nullable: false),
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
                name: "TestResults",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BuildId = table.Column<int>(nullable: false),
                    Run = table.Column<string>(nullable: true),
                    Assembly = table.Column<string>(nullable: true),
                    Collection = table.Column<string>(nullable: true),
                    Type = table.Column<string>(nullable: true),
                    Method = table.Column<string>(nullable: true),
                    FullName = table.Column<string>(nullable: true),
                    Result = table.Column<string>(nullable: false),
                    SkipReason = table.Column<string>(nullable: true),
                    FailureMessage = table.Column<string>(nullable: true),
                    FailureStackTrace = table.Column<string>(nullable: true),
                    Traits = table.Column<string>(nullable: true),
                    Flaky = table.Column<bool>(nullable: false),
                    FlakyOn = table.Column<string>(nullable: true)
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
                name: "IX_TestResults_BuildId",
                table: "TestResults",
                column: "BuildId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TestResults");

            migrationBuilder.DropTable(
                name: "Builds");

            migrationBuilder.DropTable(
                name: "Pipelines");
        }
    }
}
