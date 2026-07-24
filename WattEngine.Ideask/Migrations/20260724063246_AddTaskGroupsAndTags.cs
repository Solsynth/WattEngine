using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace WattEngine.Ideask.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskGroupsAndTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "group_id",
                table: "tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tags",
                table: "tasks",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "task_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    broad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_groups", x => x.id);
                    table.ForeignKey(
                        name: "fk_task_groups_broads_broad_id",
                        column: x => x.broad_id,
                        principalTable: "broads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tasks_group_id",
                table: "tasks",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_groups_broad_id_position",
                table: "task_groups",
                columns: new[] { "broad_id", "position" });

            migrationBuilder.AddForeignKey(
                name: "fk_tasks_task_groups_group_id",
                table: "tasks",
                column: "group_id",
                principalTable: "task_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tasks_task_groups_group_id",
                table: "tasks");

            migrationBuilder.DropTable(
                name: "task_groups");

            migrationBuilder.DropIndex(
                name: "ix_tasks_group_id",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "group_id",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "tags",
                table: "tasks");
        }
    }
}
