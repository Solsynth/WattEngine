using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WattEngine.Flywheel.Migrations;

[DbContext(typeof(AppDatabase))]
[Migration("20260727010000_ReplaceMlsOperationsWithBlobs")]
public partial class ReplaceMlsOperationsWithBlobs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "operations");
        migrationBuilder.DropTable(name: "devices");
        migrationBuilder.DropTable(name: "stream_members");
        migrationBuilder.DropTable(name: "streams");
        migrationBuilder.CreateTable(name: "app_settings", columns: table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false), workspace_id = table.Column<Guid>(type: "uuid", nullable: false), app_id = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false), retained_revision_count = table.Column<int>(type: "integer", nullable: false), event_cursor = table.Column<long>(type: "bigint", nullable: false), created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false), updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: table => table.PrimaryKey("pk_app_settings", x => x.id));
        migrationBuilder.CreateTable(name: "blobs", columns: table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false), workspace_id = table.Column<Guid>(type: "uuid", nullable: false), app_id = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false), blob_id = table.Column<Guid>(type: "uuid", nullable: false), current_revision = table.Column<long>(type: "bigint", nullable: false), last_event_cursor = table.Column<long>(type: "bigint", nullable: false), created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false), updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: table => table.PrimaryKey("pk_blobs", x => x.id));
        migrationBuilder.CreateTable(name: "blob_revisions", columns: table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false), blob_id = table.Column<Guid>(type: "uuid", nullable: false), revision = table.Column<long>(type: "bigint", nullable: false), scheme_version = table.Column<int>(type: "integer", nullable: false), storage_key = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false), size = table.Column<long>(type: "bigint", nullable: false), sha256 = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false), uploaded_by_account_id = table.Column<Guid>(type: "uuid", nullable: false), created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
        }, constraints: table => table.PrimaryKey("pk_blob_revisions", x => x.id));
        migrationBuilder.CreateIndex(name: "ix_app_settings_workspace_id_app_id", table: "app_settings", columns: new[] { "workspace_id", "app_id" }, unique: true);
        migrationBuilder.CreateIndex(name: "ix_blobs_workspace_id_app_id_blob_id", table: "blobs", columns: new[] { "workspace_id", "app_id", "blob_id" }, unique: true);
        migrationBuilder.CreateIndex(name: "ix_blob_revisions_blob_id_revision", table: "blob_revisions", columns: new[] { "blob_id", "revision" }, unique: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder) => throw new NotSupportedException("The pre-production Flywheel MLS schema is intentionally not restorable.");
}
