using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniCds.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReportEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GeneratedByUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    Format = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reports_users_GeneratedByUserId",
                        column: x => x.GeneratedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reports_GeneratedAtUtc",
                table: "reports",
                column: "GeneratedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_reports_GeneratedByUserId",
                table: "reports",
                column: "GeneratedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reports");
        }
    }
}
