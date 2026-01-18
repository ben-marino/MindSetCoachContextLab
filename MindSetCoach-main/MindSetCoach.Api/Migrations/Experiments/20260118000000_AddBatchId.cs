using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MindSetCoach.Api.Migrations.Experiments
{
    /// <inheritdoc />
    public partial class AddBatchId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BatchId",
                table: "ExperimentRuns",
                type: "TEXT",
                maxLength: 36,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExperimentRuns_BatchId",
                table: "ExperimentRuns",
                column: "BatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExperimentRuns_BatchId",
                table: "ExperimentRuns");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "ExperimentRuns");
        }
    }
}
