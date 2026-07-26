using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WattEngine.Ideask.Migrations
{
    /// <inheritdoc />
    public partial class SupportMultipleGitHubRepositoriesPerBoard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_git_hub_integrations_broad_id",
                table: "git_hub_integrations");

            migrationBuilder.DropIndex(
                name: "ix_git_hub_issue_links_task_id",
                table: "git_hub_issue_links");

            migrationBuilder.DropIndex(
                name: "ix_git_hub_comment_links_comment_id",
                table: "git_hub_comment_links");

            migrationBuilder.AddColumn<Guid>(
                name: "integration_id",
                table: "git_hub_comment_links",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_git_hub_integrations_broad_id",
                table: "git_hub_integrations",
                column: "broad_id");

            migrationBuilder.CreateIndex(
                name: "ix_git_hub_issue_links_integration_id_task_id",
                table: "git_hub_issue_links",
                columns: new[] { "integration_id", "task_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_git_hub_issue_links_task_id",
                table: "git_hub_issue_links",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "ix_git_hub_comment_links_comment_id",
                table: "git_hub_comment_links",
                column: "comment_id");

            migrationBuilder.CreateIndex(
                name: "ix_git_hub_comment_links_integration_id_comment_id",
                table: "git_hub_comment_links",
                columns: new[] { "integration_id", "comment_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_git_hub_integrations_broad_id",
                table: "git_hub_integrations");

            migrationBuilder.DropIndex(
                name: "ix_git_hub_issue_links_integration_id_task_id",
                table: "git_hub_issue_links");

            migrationBuilder.DropIndex(
                name: "ix_git_hub_issue_links_task_id",
                table: "git_hub_issue_links");

            migrationBuilder.DropIndex(
                name: "ix_git_hub_comment_links_comment_id",
                table: "git_hub_comment_links");

            migrationBuilder.DropIndex(
                name: "ix_git_hub_comment_links_integration_id_comment_id",
                table: "git_hub_comment_links");

            migrationBuilder.DropColumn(
                name: "integration_id",
                table: "git_hub_comment_links");

            migrationBuilder.CreateIndex(
                name: "ix_git_hub_integrations_broad_id",
                table: "git_hub_integrations",
                column: "broad_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_git_hub_issue_links_task_id",
                table: "git_hub_issue_links",
                column: "task_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_git_hub_comment_links_comment_id",
                table: "git_hub_comment_links",
                column: "comment_id",
                unique: true);
        }
    }
}
