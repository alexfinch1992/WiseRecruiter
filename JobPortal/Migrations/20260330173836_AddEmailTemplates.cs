using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace JobPortal.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Subject = table.Column<string>(type: "TEXT", nullable: false),
                    BodyContent = table.Column<string>(type: "TEXT", nullable: false),
                    LastModified = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailTemplates", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "EmailTemplates",
                columns: new[] { "Id", "BodyContent", "LastModified", "Name", "Subject" },
                values: new object[,]
                {
                    { 1, "Hi {{FirstName}},\n\nThank you for applying. We'd like to invite you to a brief screening call to discuss your application.\n\nPlease reply with your availability and we'll get something booked in.\n\nBest regards,\nThe WiseTech Recruiting Team", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Screening Invite", "You're Invited to a Screening Call — {{FirstName}}" },
                    { 2, "Dear {{FirstName}},\n\nWe are delighted to extend this formal offer of employment at WiseTech Global. Please review the attached details and feel free to reach out if you have any questions.\n\nWe look forward to welcoming you to the team!\n\nBest regards,\nThe WiseTech Recruiting Team", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Offer Letter", "Congratulations {{FirstName}} — Your Offer from WiseTech Global" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailTemplates");
        }
    }
}
