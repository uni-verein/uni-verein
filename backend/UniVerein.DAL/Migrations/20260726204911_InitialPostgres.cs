using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniVerein.DAL.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContributionPlans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    interval = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContributionPlans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "CreditorConfigs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    iban_encrypted = table.Column<string>(type: "text", nullable: false),
                    bic_encrypted = table.Column<string>(type: "text", nullable: false),
                    creditor_id = table.Column<string>(type: "text", nullable: false),
                    street_name_and_number = table.Column<string>(type: "text", nullable: true),
                    post_code = table.Column<string>(type: "text", nullable: true),
                    city_name = table.Column<string>(type: "text", nullable: false),
                    country_code = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditorConfigs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "FirmwareVersions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "text", nullable: false),
                    tag_name = table.Column<string>(type: "text", nullable: false),
                    release_notes = table.Column<string>(type: "text", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmwareVersions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "LinkSettings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    link = table.Column<string>(type: "text", nullable: false),
                    icon = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkSettings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "MailSettings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    smtp_server = table.Column<string>(type: "text", nullable: false),
                    port = table.Column<int>(type: "integer", nullable: false),
                    username = table.Column<string>(type: "text", nullable: false),
                    password = table.Column<string>(type: "text", nullable: false),
                    from_mail = table.Column<string>(type: "text", nullable: false),
                    enable_ssl = table.Column<bool>(type: "boolean", nullable: false),
                    imap_server = table.Column<string>(type: "text", nullable: false),
                    imap_port = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailSettings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "MemberCategories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberCategories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "SepaExport",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    exportedCases = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SepaExport", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    email = table.Column<string>(type: "text", nullable: true),
                    password_hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    failed_attempts = table.Column<int>(type: "integer", nullable: false),
                    blocking_login_timeout = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "web_page_config",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    page_name = table.Column<string>(type: "text", nullable: false),
                    logo = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_web_page_config", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mandate_id = table.Column<string>(type: "text", nullable: false),
                    member_number = table.Column<int>(type: "integer", nullable: false),
                    gender = table.Column<string>(type: "text", nullable: false),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    middle_name = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    birthday = table.Column<string>(type: "text", nullable: false),
                    street = table.Column<string>(type: "text", nullable: false),
                    postal_code = table.Column<string>(type: "text", nullable: false),
                    city = table.Column<string>(type: "text", nullable: false),
                    country_code = table.Column<string>(type: "text", nullable: true),
                    emailEncrypted = table.Column<string>(type: "text", nullable: false),
                    emailHash = table.Column<string>(type: "text", nullable: false),
                    phone = table.Column<string>(type: "text", nullable: false),
                    bulk_mail = table.Column<string>(type: "text", nullable: false),
                    start_of_studies = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_of_studies = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    academic_degree = table.Column<string>(type: "text", nullable: true),
                    course_of_study = table.Column<string>(type: "text", nullable: false),
                    task_within_the_club = table.Column<string>(type: "text", nullable: false),
                    iban_encrypted = table.Column<string>(type: "text", nullable: true),
                    iban_hash = table.Column<string>(type: "text", nullable: true),
                    bic_encrypted = table.Column<string>(type: "text", nullable: true),
                    sepa_consent = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    entry_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    exit_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    member_category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    contribution_plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Members", x => x.id);
                    table.ForeignKey(
                        name: "FK_Members_ContributionPlans_contribution_plan_id",
                        column: x => x.contribution_plan_id,
                        principalTable: "ContributionPlans",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_Members_MemberCategories_member_category_id",
                        column: x => x.member_category_id,
                        principalTable: "MemberCategories",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    entity = table.Column<string>(type: "text", nullable: false),
                    data = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FirmwareVersionNotifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    firmware_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmwareVersionNotifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_FirmwareVersionNotifications_FirmwareVersions_firmware_vers~",
                        column: x => x.firmware_version_id,
                        principalTable: "FirmwareVersions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FirmwareVersionNotifications_Users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Contributions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    paid = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    exportId = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contributions", x => x.id);
                    table.ForeignKey(
                        name: "FK_Contributions_Members_member_id",
                        column: x => x.member_id,
                        principalTable: "Members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_user_id",
                table: "AuditLogs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_member_id",
                table: "Contributions",
                column: "member_id");

            migrationBuilder.CreateIndex(
                name: "IX_FirmwareVersionNotifications_firmware_version_id_user_id",
                table: "FirmwareVersionNotifications",
                columns: new[] { "firmware_version_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FirmwareVersionNotifications_user_id",
                table: "FirmwareVersionNotifications",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_FirmwareVersions_version",
                table: "FirmwareVersions",
                column: "version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Members_contribution_plan_id",
                table: "Members",
                column: "contribution_plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_Members_member_category_id",
                table: "Members",
                column: "member_category_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Contributions");

            migrationBuilder.DropTable(
                name: "CreditorConfigs");

            migrationBuilder.DropTable(
                name: "FirmwareVersionNotifications");

            migrationBuilder.DropTable(
                name: "LinkSettings");

            migrationBuilder.DropTable(
                name: "MailSettings");

            migrationBuilder.DropTable(
                name: "SepaExport");

            migrationBuilder.DropTable(
                name: "web_page_config");

            migrationBuilder.DropTable(
                name: "Members");

            migrationBuilder.DropTable(
                name: "FirmwareVersions");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "ContributionPlans");

            migrationBuilder.DropTable(
                name: "MemberCategories");
        }
    }
}
