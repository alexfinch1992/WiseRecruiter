using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace JobPortal.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryAndExtendTemplateFacet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Technical" },
                    { 2, "Soft Skills" },
                    { 3, "Leadership" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScorecardTemplateFacets_CategoryId",
                table: "ScorecardTemplateFacets",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScorecardTemplateFacets_Categories_CategoryId",
                table: "ScorecardTemplateFacets",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScorecardTemplateFacets_Categories_CategoryId",
                table: "ScorecardTemplateFacets");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_ScorecardTemplateFacets_CategoryId",
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
        }
    }
}
