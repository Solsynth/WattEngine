using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WattEngine.Flywheel.Migrations;

[DbContext(typeof(AppDatabase))]
[Migration("20260726000000_Initial")]
public partial class Initial : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "flywheel_streams",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                app_id = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                mls_group_id = table.Column<string>(type: "character varying(1400)", maxLength: 1400, nullable: false),
                current_cursor = table.Column<long>(type: "bigint", nullable: false),
                mls_epoch = table.Column<long>(type: "bigint", nullable: false),
                requires_mls_rotation = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            }, constraints: table => table.PrimaryKey("pk_flywheel_streams", x => x.id));
        migrationBuilder.CreateTable(
            name: "flywheel_devices",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                stream_id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                device_id = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                label = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                is_revoked = table.Column<bool>(type: "boolean", nullable: false),
                last_acknowledged_cursor = table.Column<long>(type: "bigint", nullable: false),
                last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            }, constraints: table => table.PrimaryKey("pk_flywheel_devices", x => x.id));
        migrationBuilder.CreateTable(
            name: "flywheel_operations",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                stream_id = table.Column<Guid>(type: "uuid", nullable: false),
                device_registration_id = table.Column<Guid>(type: "uuid", nullable: false),
                operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                scheme_version = table.Column<int>(type: "integer", nullable: false),
                cursor = table.Column<long>(type: "bigint", nullable: false),
                ciphertext = table.Column<byte[]>(type: "bytea", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                retain_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            }, constraints: table => table.PrimaryKey("pk_flywheel_operations", x => x.id));
        migrationBuilder.CreateTable(
            name: "flywheel_stream_members",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                stream_id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            }, constraints: table => table.PrimaryKey("pk_flywheel_stream_members", x => x.id));
        migrationBuilder.CreateIndex(name: "ix_flywheel_streams_workspace_id_app_id", table: "flywheel_streams", columns: new[] { "workspace_id", "app_id" }, unique: true);
        migrationBuilder.CreateIndex(name: "ix_flywheel_devices_stream_id_device_id", table: "flywheel_devices", columns: new[] { "stream_id", "device_id" }, unique: true);
        migrationBuilder.CreateIndex(name: "ix_flywheel_operations_stream_id_operation_id", table: "flywheel_operations", columns: new[] { "stream_id", "operation_id" }, unique: true);
        migrationBuilder.CreateIndex(name: "ix_flywheel_operations_stream_id_cursor", table: "flywheel_operations", columns: new[] { "stream_id", "cursor" }, unique: true);
        migrationBuilder.CreateIndex(name: "ix_flywheel_stream_members_stream_id_account_id", table: "flywheel_stream_members", columns: new[] { "stream_id", "account_id" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "flywheel_operations");
        migrationBuilder.DropTable(name: "flywheel_devices");
        migrationBuilder.DropTable(name: "flywheel_stream_members");
        migrationBuilder.DropTable(name: "flywheel_streams");
    }
}
