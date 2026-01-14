using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MindSetCoach.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Coaches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Coaches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Coaches_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Athletes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    CoachId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Athletes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Athletes_Coaches_CoachId",
                        column: x => x.CoachId,
                        principalTable: "Coaches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Athletes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JournalEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AthleteId = table.Column<int>(type: "integer", nullable: false),
                    EntryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EmotionalState = table.Column<string>(type: "text", nullable: false),
                    SessionReflection = table.Column<string>(type: "text", nullable: false),
                    MentalBarriers = table.Column<string>(type: "text", nullable: false),
                    IsFlagged = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalEntries_Athletes_AthleteId",
                        column: x => x.AthleteId,
                        principalTable: "Athletes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "Email", "PasswordHash", "Role" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "coach@test.com", "$2a$11$jWRAhGYBJJw4JlX8QWpY5uBQxRBOg3N93ktjv8m9u4XTnXg/2hrw6", 1 },
                    { 2, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "athlete1@test.com", "$2a$11$jWRAhGYBJJw4JlX8QWpY5uBQxRBOg3N93ktjv8m9u4XTnXg/2hrw6", 0 },
                    { 3, new DateTime(2025, 10, 1, 0, 0, 0, 0, DateTimeKind.Utc), "athlete2@test.com", "$2a$11$jWRAhGYBJJw4JlX8QWpY5uBQxRBOg3N93ktjv8m9u4XTnXg/2hrw6", 0 }
                });

            migrationBuilder.InsertData(
                table: "Coaches",
                columns: new[] { "Id", "Email", "Name", "UserId" },
                values: new object[] { 1, "coach@test.com", "Coach Mike", 1 });

            migrationBuilder.InsertData(
                table: "Athletes",
                columns: new[] { "Id", "CoachId", "Email", "Name", "UserId" },
                values: new object[,]
                {
                    { 1, 1, "athlete1@test.com", "Athlete One", 2 },
                    { 2, 1, "athlete2@test.com", "Athlete Two", 3 }
                });

            migrationBuilder.InsertData(
                table: "JournalEntries",
                columns: new[] { "Id", "AthleteId", "CreatedAt", "EmotionalState", "EntryDate", "IsFlagged", "MentalBarriers", "SessionReflection" },
                values: new object[,]
                {
                    { 1, 1, new DateTime(2025, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), "Feeling nervous about the upcoming competition. My anxiety is high but I'm trying to stay positive.", new DateTime(2025, 10, 18, 0, 0, 0, 0, DateTimeKind.Utc), true, "Self-doubt is creeping in. I keep comparing myself to other athletes and feeling like I'm not good enough.", "Training went okay today. I struggled with consistency in my technique. Need to focus more on breathing." },
                    { 2, 1, new DateTime(2025, 10, 19, 0, 0, 0, 0, DateTimeKind.Utc), "More confident today after talking with my coach. Feeling motivated.", new DateTime(2025, 10, 19, 0, 0, 0, 0, DateTimeKind.Utc), false, "Still worried about letting my team down, but working through it with visualization techniques.", "Had a great session! My form was much better and I hit all my targets. Coach gave positive feedback." },
                    { 3, 1, new DateTime(2025, 10, 20, 0, 0, 0, 0, DateTimeKind.Utc), "Feeling strong and focused. Ready to push myself today.", new DateTime(2025, 10, 20, 0, 0, 0, 0, DateTimeKind.Utc), false, "Had a moment of doubt during the hardest set, but used my breathing techniques to refocus. It worked!", "Pushed through a tough workout. My mental game was on point - stayed present and didn't let frustration take over." },
                    { 4, 1, new DateTime(2025, 10, 21, 0, 0, 0, 0, DateTimeKind.Utc), "Tired and a bit overwhelmed. Feeling the pressure of balancing training with life.", new DateTime(2025, 10, 21, 0, 0, 0, 0, DateTimeKind.Utc), true, "Feeling burnt out. The voice in my head keeps saying I should take a break, but I'm afraid of losing momentum.", "Not my best session. I was distracted and couldn't focus. Made several mistakes that I don't usually make." },
                    { 5, 1, new DateTime(2025, 10, 22, 0, 0, 0, 0, DateTimeKind.Utc), "Recharged after taking yesterday afternoon off. Feeling balanced and ready.", new DateTime(2025, 10, 22, 0, 0, 0, 0, DateTimeKind.Utc), false, "Learning to trust the process and not feel guilty about taking care of myself. This is progress.", "Amazing session today! Everything clicked. I realized that rest was exactly what I needed." }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Athletes_CoachId",
                table: "Athletes",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_Athletes_UserId",
                table: "Athletes",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Coaches_UserId",
                table: "Coaches",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_AthleteId",
                table: "JournalEntries",
                column: "AthleteId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JournalEntries");

            migrationBuilder.DropTable(
                name: "Athletes");

            migrationBuilder.DropTable(
                name: "Coaches");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
