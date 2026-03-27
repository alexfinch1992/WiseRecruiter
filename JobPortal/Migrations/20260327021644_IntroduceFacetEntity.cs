using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPortal.Migrations
{
    /// <inheritdoc />
    public partial class IntroduceFacetEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScorecardTemplateFacets_Categories_CategoryId",
                table: "ScorecardTemplateFacets");

            migrationBuilder.DropForeignKey(
                name: "FK_ScorecardTemplateFacets_ScorecardFacets_ScorecardFacetId",
                table: "ScorecardTemplateFacets");

            migrationBuilder.DropIndex(
                name: "IX_ScorecardTemplateFacets_CategoryId",
                table: "ScorecardTemplateFacets");

            migrationBuilder.DropIndex(
                name: "IX_ScorecardTemplateFacets_ScorecardFacetId",
                table: "ScorecardTemplateFacets");

            migrationBuilder.DropIndex(
                name: "IX_ScorecardTemplateFacets_ScorecardTemplateId_ScorecardFacetId",
                table: "ScorecardTemplateFacets");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "ScorecardTemplateFacets");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ScorecardTemplateFacets");

            migrationBuilder.DropColumn(
                name: "NotesPlaceholder",
                table: "ScorecardTemplateFacets");

            migrationBuilder.AddColumn<int>(
                name: "FacetId",
                table: "ScorecardTemplateFacets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Facets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    NotesPlaceholder = table.Column<string>(type: "TEXT", nullable: true),
                    CategoryId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Facets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Facets_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            // Copy ScorecardFacets → Facets (preserving IDs for referential integrity)
            migrationBuilder.Sql(@"
                INSERT INTO Facets (Id, Name, Description, NotesPlaceholder, CategoryId)
                SELECT Id, Name, Description, NotesPlaceholder, CategoryId
                FROM ScorecardFacets;
            ");

            // Populate FacetId from the legacy ScorecardFacetId (IDs are aligned after the copy above)
            migrationBuilder.Sql(@"
                UPDATE ScorecardTemplateFacets
                SET FacetId = ScorecardFacetId
                WHERE ScorecardFacetId IS NOT NULL AND ScorecardFacetId != 0;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_ScorecardTemplateFacets_FacetId",
                table: "ScorecardTemplateFacets",
                column: "FacetId");

            migrationBuilder.CreateIndex(
                name: "IX_ScorecardTemplateFacets_ScorecardTemplateId_FacetId",
                table: "ScorecardTemplateFacets",
                columns: new[] { "ScorecardTemplateId", "FacetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Facets_CategoryId",
                table: "Facets",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Facets_Name",
                table: "Facets",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ScorecardTemplateFacets_Facets_FacetId",
                table: "ScorecardTemplateFacets",
                column: "FacetId",
                principalTable: "Facets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScorecardTemplateFacets_Facets_FacetId",
                table: "ScorecardTemplateFacets");

            migrationBuilder.DropTable(
                name: "Facets");

            migrationBuilder.DropIndex(
                name: "IX_ScorecardTemplateFacets_FacetId",
                table: "ScorecardTemplateFacets");

            migrationBuilder.DropIndex(
                name: "IX_ScorecardTemplateFacets_ScorecardTemplateId_FacetId",
                table: "ScorecardTemplateFacets");

            migrationBuilder.DropColumn(
                name: "FacetId",
                table: "ScorecardTemplateFacets");

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "ScorecardTemplateFacets",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ScorecardTemplateFacets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotesPlaceholder",
                table: "ScorecardTemplateFacets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScorecardTemplateFacets_CategoryId",
                table: "ScorecardTemplateFacets",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ScorecardTemplateFacets_ScorecardFacetId",
                table: "ScorecardTemplateFacets",
                column: "ScorecardFacetId");

            migrationBuilder.CreateIndex(
                name: "IX_ScorecardTemplateFacets_ScorecardTemplateId_ScorecardFacetId",
                table: "ScorecardTemplateFacets",
                columns: new[] { "ScorecardTemplateId", "ScorecardFacetId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ScorecardTemplateFacets_Categories_CategoryId",
                table: "ScorecardTemplateFacets",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ScorecardTemplateFacets_ScorecardFacets_ScorecardFacetId",
                table: "ScorecardTemplateFacets",
                column: "ScorecardFacetId",
                principalTable: "ScorecardFacets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
