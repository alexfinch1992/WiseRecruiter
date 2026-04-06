using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPortal.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Applications_AppliedDate",
                table: "Applications",
                column: "AppliedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_City",
                table: "Applications",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_JobId_Stage",
                table: "Applications",
                columns: new[] { "JobId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Stage",
                table: "Applications",
                column: "Stage");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Applications_AppliedDate",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_City",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_JobId_Stage",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_Stage",
                table: "Applications");
        }
    }
}
