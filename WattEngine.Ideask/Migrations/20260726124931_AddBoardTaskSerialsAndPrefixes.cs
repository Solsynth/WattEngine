using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WattEngine.Ideask.Migrations
{
    /// <inheritdoc />
    public partial class AddBoardTaskSerialsAndPrefixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "serial_number",
                table: "tasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "next_task_number",
                table: "broads",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "task_prefix",
                table: "broads",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            // Preserve every existing task while assigning a stable, board-local
            // number. New tasks are allocated atomically from next_task_number.
            migrationBuilder.Sql("""
                WITH numbered_tasks AS (
                    SELECT id, ROW_NUMBER() OVER (PARTITION BY broad_id ORDER BY created_at, id)::integer AS serial_number
                    FROM tasks
                )
                UPDATE tasks AS work_task
                SET serial_number = numbered_tasks.serial_number
                FROM numbered_tasks
                WHERE work_task.id = numbered_tasks.id;
                """);

            migrationBuilder.Sql("""
                UPDATE broads AS board
                SET next_task_number = COALESCE(board_task_numbers.maximum_serial_number, 0) + 1
                FROM (
                    SELECT broad_id, MAX(serial_number) AS maximum_serial_number
                    FROM tasks
                    GROUP BY broad_id
                ) AS board_task_numbers
                WHERE board.id = board_task_numbers.broad_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "serial_number",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "next_task_number",
                table: "broads");

            migrationBuilder.DropColumn(
                name: "task_prefix",
                table: "broads");
        }
    }
}
