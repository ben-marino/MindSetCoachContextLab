using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MindSetCoach.Api.Migrations.Experiments
{
    /// <inheritdoc />
    public partial class InitialExperiments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExperimentRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Temperature = table.Column<double>(type: "REAL", nullable: false),
                    PromptVersion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AthleteId = table.Column<int>(type: "INTEGER", nullable: false),
                    Persona = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TokensUsed = table.Column<int>(type: "INTEGER", nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 6, nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExperimentRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExperimentClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClaimText = table.Column<string>(type: "TEXT", nullable: false),
                    IsSupported = table.Column<bool>(type: "INTEGER", nullable: false),
                    Persona = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExperimentClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExperimentClaims_ExperimentRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "ExperimentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PositionTests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<string>(type: "TEXT", nullable: false),
                    NeedleFact = table.Column<string>(type: "TEXT", nullable: false),
                    FactRetrieved = table.Column<bool>(type: "INTEGER", nullable: false),
                    ResponseSnippet = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PositionTests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PositionTests_ExperimentRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "ExperimentRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClaimReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClaimId = table.Column<int>(type: "INTEGER", nullable: false),
                    JournalEntryId = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchedSnippet = table.Column<string>(type: "TEXT", nullable: false),
                    EntryDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimReceipts_ExperimentClaims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "ExperimentClaims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimReceipts_ClaimId",
                table: "ClaimReceipts",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimReceipts_JournalEntryId",
                table: "ClaimReceipts",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ExperimentClaims_RunId",
                table: "ExperimentClaims",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_ExperimentRuns_AthleteId",
                table: "ExperimentRuns",
                column: "AthleteId");

            migrationBuilder.CreateIndex(
                name: "IX_ExperimentRuns_StartedAt",
                table: "ExperimentRuns",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ExperimentRuns_Status",
                table: "ExperimentRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PositionTests_RunId",
                table: "PositionTests",
                column: "RunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClaimReceipts");

            migrationBuilder.DropTable(
                name: "PositionTests");

            migrationBuilder.DropTable(
                name: "ExperimentClaims");

            migrationBuilder.DropTable(
                name: "ExperimentRuns");
        }
    }
}
