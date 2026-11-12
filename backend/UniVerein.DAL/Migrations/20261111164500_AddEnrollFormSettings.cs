using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniVerein.DAL.Migrations
{
    /// <inheritdoc />
    [ExcludeFromCodeCoverage]
    public partial class AddEnrollFormSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "legal_accepted_at",
                table: "PendingSelfEnrollments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "enroll_form_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    self_enrollment_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    legal_text = table.Column<string>(type: "text", nullable: true),
                    embed_allowed_origins = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_enroll_form_settings", x => x.id);
                    table.ForeignKey(
                        name: "FK_enroll_form_settings_Tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "Tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_enroll_form_settings_tenant_id",
                table: "enroll_form_settings",
                column: "tenant_id",
                unique: true);

            migrationBuilder.Sql(@"
INSERT INTO enroll_form_settings (id, created_at, tenant_id, self_enrollment_enabled)
SELECT gen_random_uuid(), now(), t.tenant_id, TRUE
FROM (
    SELECT DISTINCT w.tenant_id
    FROM web_page_config w
    WHERE w.deleted_at IS NULL AND w.self_enrollment_enabled
) t;");

            migrationBuilder.DropColumn(
                name: "self_enrollment_enabled",
                table: "web_page_config");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "self_enrollment_enabled",
                table: "web_page_config",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(@"
UPDATE web_page_config w
SET self_enrollment_enabled = TRUE
FROM enroll_form_settings e
WHERE e.tenant_id = w.tenant_id AND e.self_enrollment_enabled;");

            migrationBuilder.DropTable(
                name: "enroll_form_settings");

            migrationBuilder.DropColumn(
                name: "legal_accepted_at",
                table: "PendingSelfEnrollments");
        }
    }
}
