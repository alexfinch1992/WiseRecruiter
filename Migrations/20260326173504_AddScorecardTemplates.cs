using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPortal.Migrations
{
    /// <inheritdoc />
    public partial class AddScorecardTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScorecardTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScorecardTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScorecardTemplateFacets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScorecardTemplateId = table.Column<int>(type: "INTEGER", nullable: false),
                    ScorecardFacetId = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScorecardTemplateFacets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScorecardTemplateFacets_ScorecardFacets_ScorecardFacetId",
                        column: x => x.ScorecardFacetId,
                        principalTable: "ScorecardFacets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScorecardTemplateFacets_ScorecardTemplates_ScorecardTemplateId",
                        column: x => x.ScorecardTemplateId,
                        principalTable: "ScorecardTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScorecardTemplateFacets_ScorecardFacetId",
                table: "ScorecardTemplateFacets",
                column: "ScorecardFacetId");

            migrationBuilder.CreateIndex(
                name: "IX_ScorecardTemplateFacets_ScorecardTemplateId_ScorecardFacetId",
                table: "ScorecardTemplateFacets",
                columns: new[] { "ScorecardTemplateId", "ScorecardFacetId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScorecardTemplateFacets");

            migrationBuilder.DropTable(
                name: "ScorecardTemplates");
        }
    }
}
