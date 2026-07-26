using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace WattEngine.Ideask.Migrations
{
    /// <inheritdoc />
    public partial class ScopeGitHubInstallationsToWorkspaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_git_hub_installation_grants_broad_id_account_id",
                table: "git_hub_installation_grants");

            migrationBuilder.AddColumn<Instant>(
                name: "completed_at",
                table: "git_hub_installation_grants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "workspace_id",
                table: "git_hub_installation_grants",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE git_hub_installation_grants AS grant
                SET workspace_id = broad.workspace_id,
                    completed_at = CASE WHEN grant.installation_id IS NOT NULL THEN grant.updated_at ELSE NULL END
                FROM broads AS broad
                WHERE broad.id = grant.broad_id;
                """);

            migrationBuilder.DropColumn(
                name: "broad_id",
                table: "git_hub_installation_grants");

            migrationBuilder.CreateIndex(
                name: "ix_git_hub_installation_grants_workspace_id_account_id",
                table: "git_hub_installation_grants",
                columns: new[] { "workspace_id", "account_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_git_hub_installation_grants_workspace_id_account_id",
                table: "git_hub_installation_grants");

            migrationBuilder.DropColumn(
                name: "completed_at",
                table: "git_hub_installation_grants");

            migrationBuilder.DropColumn(
                name: "workspace_id",
                table: "git_hub_installation_grants");

            migrationBuilder.AddColumn<Guid>(
                name: "broad_id",
                table: "git_hub_installation_grants",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_git_hub_installation_grants_broad_id_account_id",
                table: "git_hub_installation_grants",
                columns: new[] { "broad_id", "account_id" });
        }
    }
}
