using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MindSetCoach.Api.Migrations.Experiments
{
    /// <inheritdoc />
    public partial class AddExperimentPresets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExperimentPresets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Config = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExperimentPresets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExperimentPresets_CreatedAt",
                table: "ExperimentPresets",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ExperimentPresets_IsDefault",
                table: "ExperimentPresets",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_ExperimentPresets_Name",
                table: "ExperimentPresets",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExperimentPresets");
        }
    }
}
