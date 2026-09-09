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
            migrationBuilder.Sql("""
            CREATE TRIGGER IF NOT EXISTS audit_entries_no_update
            BEFORE UPDATE ON audit_entries
            BEGIN
                SELECT RAISE(ABORT, 'audit_entries is append-only: UPDATE is forbidden');
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER IF NOT EXISTS audit_entries_no_delete
            BEFORE DELETE ON audit_entries
            BEGIN
                SELECT RAISE(ABORT, 'audit_entries is append-only: DELETE is forbidden');
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER IF NOT EXISTS electronic_signatures_no_update
            BEFORE UPDATE ON electronic_signatures
            BEGIN
                SELECT RAISE(ABORT, 'electronic_signatures is append-only: UPDATE is forbidden');
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER IF NOT EXISTS electronic_signatures_no_delete
            BEFORE DELETE ON electronic_signatures
            BEGIN
                SELECT RAISE(ABORT, 'electronic_signatures is append-only: DELETE is forbidden');
            END;
            """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS audit_entries_no_update;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS audit_entries_no_delete;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS electronic_signatures_no_update;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS electronic_signatures_no_delete;");

        }
    }
}
