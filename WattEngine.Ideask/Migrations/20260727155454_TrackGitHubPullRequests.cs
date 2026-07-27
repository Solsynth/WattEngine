using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WattEngine.Ideask.Migrations
{
    /// <inheritdoc />
    public partial class TrackGitHubPullRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_pull_request",
                table: "git_hub_issue_links",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_pull_request",
                table: "git_hub_issue_links");
        }
    }
}
