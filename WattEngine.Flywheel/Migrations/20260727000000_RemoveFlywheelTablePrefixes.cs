using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WattEngine.Flywheel.Migrations;

[DbContext(typeof(AppDatabase))]
[Migration("20260727000000_RemoveFlywheelTablePrefixes")]
public partial class RemoveFlywheelTablePrefixes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameTable(name: "flywheel_streams", newName: "streams");
        migrationBuilder.RenameTable(name: "flywheel_devices", newName: "devices");
        migrationBuilder.RenameTable(name: "flywheel_operations", newName: "operations");
        migrationBuilder.RenameTable(name: "flywheel_stream_members", newName: "stream_members");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameTable(name: "streams", newName: "flywheel_streams");
        migrationBuilder.RenameTable(name: "devices", newName: "flywheel_devices");
        migrationBuilder.RenameTable(name: "operations", newName: "flywheel_operations");
        migrationBuilder.RenameTable(name: "stream_members", newName: "flywheel_stream_members");
    }
}
