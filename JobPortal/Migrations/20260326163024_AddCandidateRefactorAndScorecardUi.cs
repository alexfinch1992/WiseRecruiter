using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPortal.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateRefactorAndScorecardUi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CandidateId",
                table: "Applications",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Candidates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirstName = table.Column<string>(type: "TEXT", nullable: false),
                    LastName = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Candidates", x => x.Id);
                });

            // Legacy backfill: create one candidate per existing application
            // and map Applications.CandidateId before FK is introduced.
            migrationBuilder.Sql(@"
                INSERT INTO Candidates (FirstName, LastName, Email, CreatedAt)
                SELECT
                    CASE
                        WHEN instr(COALESCE(Name, ''), ' ') > 0 THEN substr(Name, 1, instr(Name, ' ') - 1)
                        WHEN COALESCE(Name, '') <> '' THEN Name
                        ELSE 'Unknown'
                    END,
                    CASE
                        WHEN instr(COALESCE(Name, ''), ' ') > 0 THEN substr(Name, instr(Name, ' ') + 1)
                        ELSE 'Candidate'
                    END,
                    'legacy-app-' || Id || '@local.invalid',
                    COALESCE(AppliedDate, CURRENT_TIMESTAMP)
                FROM Applications;
            ");

            migrationBuilder.Sql(@"
                UPDATE Applications
                SET CandidateId = (
                    SELECT c.Id
                    FROM Candidates c
                    WHERE c.Email = 'legacy-app-' || Applications.Id || '@local.invalid'
                    LIMIT 1
                );
            ");

            migrationBuilder.CreateTable(
                name: "Scorecards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CandidateId = table.Column<int>(type: "INTEGER", nullable: false),
                    SubmittedBy = table.Column<string>(type: "TEXT", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scorecards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Scorecards_Candidates_CandidateId",
                        column: x => x.CandidateId,
                        principalTable: "Candidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScorecardResponses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScorecardId = table.Column<int>(type: "INTEGER", nullable: false),
                    FacetName = table.Column<string>(type: "TEXT", nullable: false),
                    Score = table.Column<decimal>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScorecardResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScorecardResponses_Scorecards_ScorecardId",
                        column: x => x.ScorecardId,
                        principalTable: "Scorecards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_CandidateId",
                table: "Applications",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_ScorecardResponses_ScorecardId",
                table: "ScorecardResponses",
                column: "ScorecardId");

            migrationBuilder.CreateIndex(
                name: "IX_Scorecards_CandidateId",
                table: "Scorecards",
                column: "CandidateId");

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_Candidates_CandidateId",
                table: "Applications",
                column: "CandidateId",
                principalTable: "Candidates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applications_Candidates_CandidateId",
                table: "Applications");

            migrationBuilder.DropTable(
                name: "ScorecardResponses");

            migrationBuilder.DropTable(
                name: "Scorecards");

            migrationBuilder.DropTable(
                name: "Candidates");

            migrationBuilder.DropIndex(
                name: "IX_Applications_CandidateId",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "CandidateId",
                table: "Applications");
        }
    }
}
