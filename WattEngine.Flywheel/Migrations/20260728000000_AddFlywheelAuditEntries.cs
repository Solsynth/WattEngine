using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WattEngine.Flywheel.Migrations;

[DbContext(typeof(AppDatabase))]
[Migration("20260728000000_AddFlywheelAuditEntries")]
public partial class AddFlywheelAuditEntries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "audit_entries", columns: table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false), workspace_id = table.Column<Guid>(type: "uuid", nullable: false), app_id = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false), blob_id = table.Column<Guid>(type: "uuid", nullable: true), revision = table.Column<long>(type: "bigint", nullable: true), action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false), actor_account_id = table.Column<Guid>(type: "uuid", nullable: false), created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: table => table.PrimaryKey("pk_audit_entries", x => x.id));
        migrationBuilder.CreateIndex(name: "ix_audit_entries_workspace_id_app_id_created_at", table: "audit_entries", columns: new[] { "workspace_id", "app_id", "created_at" });
        migrationBuilder.CreateIndex(name: "ix_audit_entries_workspace_id_blob_id_created_at", table: "audit_entries", columns: new[] { "workspace_id", "blob_id", "created_at" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "audit_entries");
}
