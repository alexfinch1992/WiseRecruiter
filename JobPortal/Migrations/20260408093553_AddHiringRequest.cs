using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobPortal.Migrations
{
    /// <inheritdoc />
    public partial class AddHiringRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HiringRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobTitle = table.Column<string>(type: "TEXT", nullable: false),
                    Department = table.Column<string>(type: "TEXT", nullable: false),
                    Headcount = table.Column<int>(type: "INTEGER", nullable: false),
                    Justification = table.Column<string>(type: "TEXT", nullable: true),
                    SalaryBand = table.Column<string>(type: "TEXT", nullable: true),
                    TargetStartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EmploymentType = table.Column<int>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    Stage = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SubmittedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    SubmittedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Stage1ReviewedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    Stage1ReviewedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Stage1Feedback = table.Column<string>(type: "TEXT", nullable: true),
                    Stage2ReviewedByUserId = table.Column<int>(type: "INTEGER", nullable: true),
                    Stage2ReviewedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Stage2Feedback = table.Column<string>(type: "TEXT", nullable: true),
                    RejectionReason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HiringRequests", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HiringRequests");
        }
    }
}
