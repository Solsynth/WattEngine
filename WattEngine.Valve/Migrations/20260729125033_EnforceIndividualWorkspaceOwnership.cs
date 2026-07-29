using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WattEngine.Valve.Migrations
{
    /// <inheritdoc />
    public partial class EnforceIndividualWorkspaceOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_workspaces_owner_individual",
                table: "workspaces",
                column: "owner_account_id",
                unique: true,
                filter: "type = 0 AND deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_workspaces_owner_individual",
                table: "workspaces");
        }
    }
}
