using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WattEngine.Ideask.Migrations
{
    /// <inheritdoc />
    public partial class MakeGitHubBoardRelationshipManyToOne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_git_hub_integrations_broad_id",
                table: "git_hub_integrations");

            migrationBuilder.CreateIndex(
                name: "ix_git_hub_integrations_broad_id",
                table: "git_hub_integrations",
                column: "broad_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_git_hub_integrations_broad_id",
                table: "git_hub_integrations");

            migrationBuilder.CreateIndex(
                name: "ix_git_hub_integrations_broad_id",
                table: "git_hub_integrations",
                column: "broad_id",
                unique: true);
        }
    }
}
