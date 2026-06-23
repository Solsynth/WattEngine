using System;
using DysonNetwork.Shared.Models;
using Microsoft.EntityFrameworkCore.Migrations;
using NodaTime;

#nullable disable

namespace WattEngine.Valve.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workspaces",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    name = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    description = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    type = table.Column<int>(type: "integer", nullable: false),
                    owner_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    picture = table.Column<SnCloudFileReferenceObject>(type: "jsonb", nullable: true),
                    background = table.Column<SnCloudFileReferenceObject>(type: "jsonb", nullable: true),
                    plan = table.Column<int>(type: "integer", nullable: false),
                    plan_expires_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workspaces", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workspace_billings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_method_id = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    next_billing_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    monthly_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    currency = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workspace_billings", x => x.id);
                    table.ForeignKey(
                        name: "fk_workspace_billings_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workspace_invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    currency = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    paid_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workspace_invoices", x => x.id);
                    table.ForeignKey(
                        name: "fk_workspace_invoices_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workspace_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    joined_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    leave_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workspace_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_workspace_members_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workspace_role_permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_level = table.Column<int>(type: "integer", nullable: false),
                    can_manage_workspace = table.Column<bool>(type: "boolean", nullable: false),
                    can_manage_members = table.Column<bool>(type: "boolean", nullable: false),
                    can_manage_billing = table.Column<bool>(type: "boolean", nullable: false),
                    can_create_projects = table.Column<bool>(type: "boolean", nullable: false),
                    can_manage_projects = table.Column<bool>(type: "boolean", nullable: false),
                    can_use_ideask = table.Column<bool>(type: "boolean", nullable: false),
                    can_use_drive = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workspace_role_permissions", x => x.id);
                    table.ForeignKey(
                        name: "fk_workspace_role_permissions_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workspace_user_permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    can_manage_workspace = table.Column<bool>(type: "boolean", nullable: true),
                    can_manage_members = table.Column<bool>(type: "boolean", nullable: true),
                    can_manage_billing = table.Column<bool>(type: "boolean", nullable: true),
                    can_create_projects = table.Column<bool>(type: "boolean", nullable: true),
                    can_manage_projects = table.Column<bool>(type: "boolean", nullable: true),
                    can_use_ideask = table.Column<bool>(type: "boolean", nullable: true),
                    can_use_drive = table.Column<bool>(type: "boolean", nullable: true),
                    created_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<Instant>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<Instant>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workspace_user_permissions", x => x.id);
                    table.ForeignKey(
                        name: "fk_workspace_user_permissions_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_workspace_billings_workspace_id",
                table: "workspace_billings",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "ix_workspace_invoices_workspace_id",
                table: "workspace_invoices",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "ix_workspace_members_workspace_id_account_id",
                table: "workspace_members",
                columns: new[] { "workspace_id", "account_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workspace_role_permissions_workspace_id_role_level",
                table: "workspace_role_permissions",
                columns: new[] { "workspace_id", "role_level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workspace_user_permissions_workspace_id_account_id",
                table: "workspace_user_permissions",
                columns: new[] { "workspace_id", "account_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workspaces_slug_deleted_at",
                table: "workspaces",
                columns: new[] { "slug", "deleted_at" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workspace_billings");

            migrationBuilder.DropTable(
                name: "workspace_invoices");

            migrationBuilder.DropTable(
                name: "workspace_members");

            migrationBuilder.DropTable(
                name: "workspace_role_permissions");

            migrationBuilder.DropTable(
                name: "workspace_user_permissions");

            migrationBuilder.DropTable(
                name: "workspaces");
        }
    }
}
