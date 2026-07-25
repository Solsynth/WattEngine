using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace WattEngine.Ideask.Migrations
{
    /// <inheritdoc />
    public partial class AddGitHubInstallationGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "git_hub_installation_grants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    broad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    installation_id = table.Column<long>(type: "bigint", nullable: true),
                    expires_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_git_hub_installation_grants", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_git_hub_installation_grants_broad_id_account_id",
                table: "git_hub_installation_grants",
                columns: new[] { "broad_id", "account_id" });

            migrationBuilder.CreateIndex(
                name: "ix_git_hub_installation_grants_state",
                table: "git_hub_installation_grants",
                column: "state",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "git_hub_installation_grants");
        }
    }
}
