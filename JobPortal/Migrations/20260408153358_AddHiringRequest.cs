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
            migrationBuilder.AddColumn<bool>(
                name: "IsApprovingExecutive",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "HiringRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RequestedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    RoleTitle = table.Column<string>(type: "TEXT", nullable: false),
                    Department = table.Column<string>(type: "TEXT", nullable: false),
                    LevelBand = table.Column<string>(type: "TEXT", nullable: false),
                    Location = table.Column<string>(type: "TEXT", nullable: false),
                    IsReplacement = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReplacementReason = table.Column<string>(type: "TEXT", nullable: true),
                    Headcount = table.Column<int>(type: "INTEGER", nullable: false),
                    Justification = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    TalentLeadReviewedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    TalentLeadReviewedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TalentLeadNotes = table.Column<string>(type: "TEXT", nullable: true),
                    ExecutiveApprovedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    ExecutiveApprovedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExecutiveNotes = table.Column<string>(type: "TEXT", nullable: true),
                    RejectedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    RejectedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RejectionReason = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HiringRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HiringRequests_AspNetUsers_ExecutiveApprovedByUserId",
                        column: x => x.ExecutiveApprovedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HiringRequests_AspNetUsers_RejectedByUserId",
                        column: x => x.RejectedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HiringRequests_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HiringRequests_AspNetUsers_TalentLeadReviewedByUserId",
                        column: x => x.TalentLeadReviewedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HiringRequests_ExecutiveApprovedByUserId",
                table: "HiringRequests",
                column: "ExecutiveApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HiringRequests_RejectedByUserId",
                table: "HiringRequests",
                column: "RejectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HiringRequests_RequestedByUserId",
                table: "HiringRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HiringRequests_TalentLeadReviewedByUserId",
                table: "HiringRequests",
                column: "TalentLeadReviewedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HiringRequests");

            migrationBuilder.DropColumn(
                name: "IsApprovingExecutive",
                table: "AspNetUsers");
        }
    }
}
