using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPortal.Migrations
{
    /// <inheritdoc />
    public partial class MoveFacetFieldsToScorecardFacet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "ScorecardFacets",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ScorecardFacets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotesPlaceholder",
                table: "ScorecardFacets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScorecardFacets_CategoryId",
                table: "ScorecardFacets",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScorecardFacets_Categories_CategoryId",
                table: "ScorecardFacets",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Copy existing data from ScorecardTemplateFacets → ScorecardFacets.
            // For each facet, take the first non-null value found across all template assignments.
            migrationBuilder.Sql(@"
                UPDATE ScorecardFacets
                SET
                    Description = (
                        SELECT stf.Description
                        FROM ScorecardTemplateFacets stf
                        WHERE stf.ScorecardFacetId = ScorecardFacets.Id
                          AND stf.Description IS NOT NULL
                        LIMIT 1
                    ),
                    NotesPlaceholder = (
                        SELECT stf.NotesPlaceholder
                        FROM ScorecardTemplateFacets stf
                        WHERE stf.ScorecardFacetId = ScorecardFacets.Id
                          AND stf.NotesPlaceholder IS NOT NULL
                        LIMIT 1
                    ),
                    CategoryId = (
                        SELECT stf.CategoryId
                        FROM ScorecardTemplateFacets stf
                        WHERE stf.ScorecardFacetId = ScorecardFacets.Id
                          AND stf.CategoryId IS NOT NULL
                        LIMIT 1
                    )
                WHERE EXISTS (
                    SELECT 1 FROM ScorecardTemplateFacets stf
                    WHERE stf.ScorecardFacetId = ScorecardFacets.Id
                      AND (stf.Description IS NOT NULL
                           OR stf.NotesPlaceholder IS NOT NULL
                           OR stf.CategoryId IS NOT NULL)
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScorecardFacets_Categories_CategoryId",
                table: "ScorecardFacets");

            migrationBuilder.DropIndex(
                name: "IX_ScorecardFacets_CategoryId",
                table: "ScorecardFacets");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "ScorecardFacets");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ScorecardFacets");

            migrationBuilder.DropColumn(
                name: "NotesPlaceholder",
                table: "ScorecardFacets");
        }
    }
}
