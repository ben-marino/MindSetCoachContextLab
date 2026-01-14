using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MindSetCoach.Api.Migrations.Experiments
{
    /// <inheritdoc />
    public partial class AddExperimentTypeAndSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExperimentType",
                table: "ExperimentRuns",
                type: "TEXT",
                nullable: false,
                defaultValue: "Persona");

            migrationBuilder.AddColumn<int>(
                name: "EntriesUsed",
                table: "ExperimentRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EntryOrder",
                table: "ExperimentRuns",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "reverse");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ExperimentRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ExperimentRuns_IsDeleted",
                table: "ExperimentRuns",
                column: "IsDeleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExperimentRuns_IsDeleted",
                table: "ExperimentRuns");

            migrationBuilder.DropColumn(
                name: "ExperimentType",
                table: "ExperimentRuns");

            migrationBuilder.DropColumn(
                name: "EntriesUsed",
                table: "ExperimentRuns");

            migrationBuilder.DropColumn(
                name: "EntryOrder",
                table: "ExperimentRuns");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ExperimentRuns");
        }
    }
}
