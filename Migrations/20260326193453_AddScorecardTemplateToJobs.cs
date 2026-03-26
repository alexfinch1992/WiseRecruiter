using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPortal.Migrations
{
    /// <inheritdoc />
    public partial class AddScorecardTemplateToJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ScorecardTemplateId",
                table: "Jobs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_ScorecardTemplateId",
                table: "Jobs",
                column: "ScorecardTemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_Jobs_ScorecardTemplates_ScorecardTemplateId",
                table: "Jobs",
                column: "ScorecardTemplateId",
                principalTable: "ScorecardTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Jobs_ScorecardTemplates_ScorecardTemplateId",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_ScorecardTemplateId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "ScorecardTemplateId",
                table: "Jobs");
        }
    }
}
