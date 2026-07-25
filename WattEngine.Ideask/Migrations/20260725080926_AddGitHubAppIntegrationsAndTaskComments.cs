using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace WattEngine.Ideask.Migrations;

public partial class AddGitHubAppIntegrationsAndTaskComments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "git_hub_integrations", columns: table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            broad_id = table.Column<Guid>(type: "uuid", nullable: false),
            installation_id = table.Column<long>(type: "bigint", nullable: false),
            git_hub_repository_id = table.Column<long>(type: "bigint", nullable: false),
            owner = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
            repository = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
            last_synced_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
            last_error = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
            created_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
            updated_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
            deleted_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("pk_git_hub_integrations", x => x.id);
            table.ForeignKey("fk_git_hub_integrations_broads_broad_id", x => x.broad_id, "broads", "id", onDelete: ReferentialAction.Cascade);
        });

        migrationBuilder.CreateTable(name: "task_comments", columns: table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false), task_id = table.Column<Guid>(type: "uuid", nullable: false),
            author_account_id = table.Column<Guid>(type: "uuid", nullable: true), external_author_login = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
            external_author_avatar_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true), content = table.Column<string>(type: "text", nullable: false),
            created_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false), updated_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false), deleted_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
        }, constraints: table => { table.PrimaryKey("pk_task_comments", x => x.id); table.ForeignKey("fk_task_comments_tasks_task_id", x => x.task_id, "tasks", "id", onDelete: ReferentialAction.Cascade); });

        migrationBuilder.CreateTable(name: "git_hub_issue_links", columns: table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false), integration_id = table.Column<Guid>(type: "uuid", nullable: false), task_id = table.Column<Guid>(type: "uuid", nullable: false),
            git_hub_issue_id = table.Column<long>(type: "bigint", nullable: false), issue_number = table.Column<int>(type: "integer", nullable: false), html_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
            last_git_hub_updated_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true), created_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false), updated_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false), deleted_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
        }, constraints: table => { table.PrimaryKey("pk_git_hub_issue_links", x => x.id); table.ForeignKey("fk_git_hub_issue_links_git_hub_integrations_integration_id", x => x.integration_id, "git_hub_integrations", "id", onDelete: ReferentialAction.Cascade); table.ForeignKey("fk_git_hub_issue_links_tasks_task_id", x => x.task_id, "tasks", "id", onDelete: ReferentialAction.Cascade); });

        migrationBuilder.CreateTable(name: "git_hub_comment_links", columns: table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false), comment_id = table.Column<Guid>(type: "uuid", nullable: false), git_hub_comment_id = table.Column<long>(type: "bigint", nullable: false),
            created_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false), updated_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false), deleted_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
        }, constraints: table => { table.PrimaryKey("pk_git_hub_comment_links", x => x.id); table.ForeignKey("fk_git_hub_comment_links_task_comments_comment_id", x => x.comment_id, "task_comments", "id", onDelete: ReferentialAction.Cascade); });

        migrationBuilder.CreateIndex("ix_git_hub_integrations_broad_id", "git_hub_integrations", "broad_id", unique: true);
        migrationBuilder.CreateIndex("ix_git_hub_integrations_git_hub_repository_id", "git_hub_integrations", "git_hub_repository_id", unique: true);
        migrationBuilder.CreateIndex("ix_task_comments_task_id_created_at", "task_comments", new[] { "task_id", "created_at" });
        migrationBuilder.CreateIndex("ix_git_hub_issue_links_integration_id_git_hub_issue_id", "git_hub_issue_links", new[] { "integration_id", "git_hub_issue_id" }, unique: true);
        migrationBuilder.CreateIndex("ix_git_hub_issue_links_task_id", "git_hub_issue_links", "task_id", unique: true);
        migrationBuilder.CreateIndex("ix_git_hub_comment_links_comment_id", "git_hub_comment_links", "comment_id", unique: true);
        migrationBuilder.CreateIndex("ix_git_hub_comment_links_git_hub_comment_id", "git_hub_comment_links", "git_hub_comment_id", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "git_hub_comment_links");
        migrationBuilder.DropTable(name: "git_hub_issue_links");
        migrationBuilder.DropTable(name: "task_comments");
        migrationBuilder.DropTable(name: "git_hub_integrations");
    }
}
