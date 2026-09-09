using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniCds.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSampleMethodRawSignalPeak : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "methods",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    Parameters = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_methods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_methods_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "samples",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    MethodId = table.Column<long>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    VoidedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    VoidedReason = table.Column<string>(type: "TEXT", nullable: true),
                    VoidedByUserId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_samples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_samples_methods_MethodId",
                        column: x => x.MethodId,
                        principalTable: "methods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_samples_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_samples_users_VoidedByUserId",
                        column: x => x.VoidedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "peaks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SampleId = table.Column<long>(type: "INTEGER", nullable: false),
                    ApexIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    StartIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    EndIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    Metrics = table.Column<string>(type: "TEXT", nullable: false),
                    DetectedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsManual = table.Column<bool>(type: "INTEGER", nullable: false),
                    VoidedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    VoidedReason = table.Column<string>(type: "TEXT", nullable: true),
                    VoidedByUserId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_peaks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_peaks_samples_SampleId",
                        column: x => x.SampleId,
                        principalTable: "samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_peaks_users_VoidedByUserId",
                        column: x => x.VoidedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "raw_signals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SampleId = table.Column<long>(type: "INTEGER", nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SampleRateHz = table.Column<int>(type: "INTEGER", nullable: false),
                    Points = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_raw_signals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_raw_signals_samples_SampleId",
                        column: x => x.SampleId,
                        principalTable: "samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_methods_CreatedByUserId",
                table: "methods",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_methods_Name_Version",
                table: "methods",
                columns: new[] { "Name", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_peaks_SampleId",
                table: "peaks",
                column: "SampleId");

            migrationBuilder.CreateIndex(
                name: "IX_peaks_VoidedByUserId",
                table: "peaks",
                column: "VoidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_raw_signals_SampleId",
                table: "raw_signals",
                column: "SampleId");

            migrationBuilder.CreateIndex(
                name: "IX_samples_CreatedByUserId",
                table: "samples",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_samples_MethodId",
                table: "samples",
                column: "MethodId");

            migrationBuilder.CreateIndex(
                name: "IX_samples_Status",
                table: "samples",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_samples_VoidedByUserId",
                table: "samples",
                column: "VoidedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "peaks");

            migrationBuilder.DropTable(
                name: "raw_signals");

            migrationBuilder.DropTable(
                name: "samples");

            migrationBuilder.DropTable(
                name: "methods");
        }
    }
}
