using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPortal.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalFeedback",
                table: "CandidateRecommendations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovedByUserId",
                table: "CandidateRecommendations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedUtc",
                table: "CandidateRecommendations",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovalFeedback",
                table: "CandidateRecommendations");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "CandidateRecommendations");

            migrationBuilder.DropColumn(
                name: "ApprovedUtc",
                table: "CandidateRecommendations");
        }
    }
}
