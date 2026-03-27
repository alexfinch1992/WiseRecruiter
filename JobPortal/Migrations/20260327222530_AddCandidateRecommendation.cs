using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPortal.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateRecommendation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CandidateRecommendations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ApplicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    Stage = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SubmittedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    SubmittedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReviewedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastUpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BypassedApproval = table.Column<bool>(type: "INTEGER", nullable: false),
                    BypassReason = table.Column<string>(type: "TEXT", nullable: true),
                    BypassedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    BypassedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateRecommendations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateRecommendations_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateRecommendations_ApplicationId",
                table: "CandidateRecommendations",
                column: "ApplicationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CandidateRecommendations");
        }
    }
}
