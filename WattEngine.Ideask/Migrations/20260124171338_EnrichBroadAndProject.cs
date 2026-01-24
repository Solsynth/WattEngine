using DysonNetwork.Shared.Models;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WattEngine.Ideask.Migrations
{
    /// <inheritdoc />
    public partial class EnrichBroadAndProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "creator_account_id",
                table: "projects",
                newName: "account_id");

            migrationBuilder.AddColumn<SnCloudFileReferenceObject>(
                name: "background_image",
                table: "broads",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "content",
                table: "broads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "broads",
                type: "character varying(8192)",
                maxLength: 8192,
                nullable: true);

            migrationBuilder.AddColumn<SnCloudFileReferenceObject>(
                name: "icon_image",
                table: "broads",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "visibility",
                table: "broads",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "background_image",
                table: "broads");

            migrationBuilder.DropColumn(
                name: "content",
                table: "broads");

            migrationBuilder.DropColumn(
                name: "description",
                table: "broads");

            migrationBuilder.DropColumn(
                name: "icon_image",
                table: "broads");

            migrationBuilder.DropColumn(
                name: "visibility",
                table: "broads");

            migrationBuilder.RenameColumn(
                name: "account_id",
                table: "projects",
                newName: "creator_account_id");
        }
    }
}
