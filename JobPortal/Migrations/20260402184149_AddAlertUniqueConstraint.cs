using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPortal.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Alerts_UserId_Type_RelatedEntityId_RelatedEntityType",
                table: "Alerts",
                columns: new[] { "UserId", "Type", "RelatedEntityId", "RelatedEntityType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Alerts_UserId_Type_RelatedEntityId_RelatedEntityType",
                table: "Alerts");
        }
    }
}
