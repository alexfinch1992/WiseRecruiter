using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPortal.Migrations
{
    /// <inheritdoc />
    public partial class AddJobOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "Jobs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_OwnerUserId",
                table: "Jobs",
                column: "OwnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Jobs_AspNetUsers_OwnerUserId",
                table: "Jobs",
                column: "OwnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Jobs_AspNetUsers_OwnerUserId",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_OwnerUserId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Jobs");
        }
    }
}
