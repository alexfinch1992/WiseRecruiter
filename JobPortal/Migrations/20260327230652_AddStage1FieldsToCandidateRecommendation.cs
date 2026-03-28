using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPortal.Migrations
{
    /// <inheritdoc />
    public partial class AddStage1FieldsToCandidateRecommendation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CareerTrajectory",
                table: "CandidateRecommendations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CognitiveNotes",
                table: "CandidateRecommendations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Concerns",
                table: "CandidateRecommendations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExperienceFit",
                table: "CandidateRecommendations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonalityNotes",
                table: "CandidateRecommendations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposedInterviewersNotes",
                table: "CandidateRecommendations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "CandidateRecommendations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TechnicalNotes",
                table: "CandidateRecommendations",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CareerTrajectory",
                table: "CandidateRecommendations");

            migrationBuilder.DropColumn(
                name: "CognitiveNotes",
                table: "CandidateRecommendations");

            migrationBuilder.DropColumn(
                name: "Concerns",
                table: "CandidateRecommendations");

            migrationBuilder.DropColumn(
                name: "ExperienceFit",
                table: "CandidateRecommendations");

            migrationBuilder.DropColumn(
                name: "PersonalityNotes",
                table: "CandidateRecommendations");

            migrationBuilder.DropColumn(
                name: "ProposedInterviewersNotes",
                table: "CandidateRecommendations");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "CandidateRecommendations");

            migrationBuilder.DropColumn(
                name: "TechnicalNotes",
                table: "CandidateRecommendations");
        }
    }
}
