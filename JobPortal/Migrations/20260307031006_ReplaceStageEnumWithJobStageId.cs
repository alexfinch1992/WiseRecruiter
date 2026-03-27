using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPortal.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceStageEnumWithJobStageId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Stage",
                table: "Applications");

            migrationBuilder.AddColumn<int>(
                name: "CurrentJobStageId",
                table: "Applications",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentStageId",
                table: "Applications",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_CurrentStageId",
                table: "Applications",
                column: "CurrentStageId");

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_JobStages_CurrentStageId",
                table: "Applications",
                column: "CurrentStageId",
                principalTable: "JobStages",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applications_JobStages_CurrentStageId",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_CurrentStageId",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "CurrentJobStageId",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "CurrentStageId",
                table: "Applications");

            migrationBuilder.AddColumn<int>(
                name: "Stage",
                table: "Applications",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
