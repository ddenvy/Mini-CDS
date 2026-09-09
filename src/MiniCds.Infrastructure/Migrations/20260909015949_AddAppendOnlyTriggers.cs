using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniCds.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppendOnlyTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
             const string sql = @"
CREATE TRIGGER trg_audit_entries_no_update BEFORE UPDATE ON audit_entries
BEGIN SELECT RAISE(ABORT, 'audit_entries is append-only'); END;
CREATE TRIGGER trg_audit_entries_no_delete BEFORE DELETE ON audit_entries
BEGIN SELECT RAISE(ABORT, 'audit_entries is append-only'); END;
CREATE TRIGGER trg_electronic_signatures_no_update BEFORE UPDATE ON electronic_signatures
BEGIN SELECT RAISE(ABORT, 'electronic_signatures is append-only'); END;
CREATE TRIGGER trg_electronic_signatures_no_delete BEFORE DELETE ON electronic_signatures
BEGIN SELECT RAISE(ABORT, 'electronic_signatures is append-only'); END;";
            migrationBuilder.Sql(sql);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
                        migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_audit_entries_no_update;
DROP TRIGGER IF EXISTS trg_audit_entries_no_delete;
DROP TRIGGER IF EXISTS trg_electronic_signatures_no_update;
DROP TRIGGER IF EXISTS trg_electronic_signatures_no_delete;");


        }
    }
}
