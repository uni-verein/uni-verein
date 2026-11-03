using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniVerein.DAL.Migrations
{
    /// <inheritdoc />
    [ExcludeFromCodeCoverage]
    public partial class AddTenantsAndUserDevices : Migration
    {
        private static readonly Guid DefaultTenantId = new("0f1c2d3e-4a5b-4c6d-8e7f-9a0b1c2d3e4f");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PaymentAcknowledgements_year_sequence",
                table: "PaymentAcknowledgements");

            migrationBuilder.DropIndex(
                name: "IX_BookingNumbers_year_kind_sequence",
                table: "BookingNumbers");

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "web_page_config",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "UserSettings",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "Users",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "UserRecoveryCodes",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "SepaExport",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "Receipts",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "ReceiptFiles",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "ReceiptCategories",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "PendingSelfEnrollments",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "PaymentAcknowledgements",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "Notifications",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "Members",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "MemberCategories",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "MailSettings",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "LinkSettings",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "Incomes",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "IncomeCategories",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "CreditorConfigs",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "Contributions",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "ContributionPlans",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "CalendarSettings",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "CalendarReminderLogs",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "CalendarFeedTokens",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "CalendarEventSubscriptions",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "CalendarEvents",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "CalendarEventParticipants",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "CalendarCategories",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "BookingNumbers",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "AuditLogs",
                type: "uuid",
                nullable: false,
                defaultValue: DefaultTenantId);

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.id);
                });

            migrationBuilder.Sql(
                $"INSERT INTO \"Tenants\" (id, name, created_at) VALUES ('{DefaultTenantId}', 'Default', now());");

            foreach (string table in new[]
            {
                "web_page_config",
                "UserSettings",
                "Users",
                "UserRecoveryCodes",
                "SepaExport",
                "Receipts",
                "ReceiptFiles",
                "ReceiptCategories",
                "PendingSelfEnrollments",
                "PaymentAcknowledgements",
                "Notifications",
                "Members",
                "MemberCategories",
                "MailSettings",
                "LinkSettings",
                "Incomes",
                "IncomeCategories",
                "CreditorConfigs",
                "Contributions",
                "ContributionPlans",
                "CalendarSettings",
                "CalendarReminderLogs",
                "CalendarFeedTokens",
                "CalendarEventSubscriptions",
                "CalendarEvents",
                "CalendarEventParticipants",
                "CalendarCategories",
                "BookingNumbers",
                "AuditLogs"
            })
            {
                migrationBuilder.Sql($"ALTER TABLE \"{table}\" ALTER COLUMN tenant_id DROP DEFAULT;");
            }

            migrationBuilder.CreateTable(
                name: "UserDevices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDevices", x => x.id);
                    table.ForeignKey(
                        name: "FK_UserDevices_Tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "Tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserDevices_Users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_web_page_config_tenant_id",
                table: "web_page_config",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_tenant_id",
                table: "UserSettings",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_Users_tenant_id",
                table: "Users",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_UserRecoveryCodes_tenant_id",
                table: "UserRecoveryCodes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_SepaExport_tenant_id",
                table: "SepaExport",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_tenant_id",
                table: "Receipts",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptFiles_tenant_id",
                table: "ReceiptFiles",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptCategories_tenant_id",
                table: "ReceiptCategories",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_PendingSelfEnrollments_tenant_id",
                table: "PendingSelfEnrollments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAcknowledgements_tenant_id",
                table: "PaymentAcknowledgements",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAcknowledgements_tenant_id_year_sequence",
                table: "PaymentAcknowledgements",
                columns: new[] { "tenant_id", "year", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_tenant_id",
                table: "Notifications",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_Members_tenant_id",
                table: "Members",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_MemberCategories_tenant_id",
                table: "MemberCategories",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_MailSettings_tenant_id",
                table: "MailSettings",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_LinkSettings_tenant_id",
                table: "LinkSettings",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_Incomes_tenant_id",
                table: "Incomes",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeCategories_tenant_id",
                table: "IncomeCategories",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_CreditorConfigs_tenant_id",
                table: "CreditorConfigs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_tenant_id",
                table: "Contributions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_ContributionPlans_tenant_id",
                table: "ContributionPlans",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSettings_tenant_id",
                table: "CalendarSettings",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarReminderLogs_tenant_id",
                table: "CalendarReminderLogs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarFeedTokens_tenant_id",
                table: "CalendarFeedTokens",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventSubscriptions_tenant_id",
                table: "CalendarEventSubscriptions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_tenant_id",
                table: "CalendarEvents",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventParticipants_tenant_id",
                table: "CalendarEventParticipants",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarCategories_tenant_id",
                table: "CalendarCategories",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_BookingNumbers_tenant_id",
                table: "BookingNumbers",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_BookingNumbers_tenant_id_year_kind_sequence",
                table: "BookingNumbers",
                columns: new[] { "tenant_id", "year", "kind", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_tenant_id",
                table: "AuditLogs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_UserDevices_tenant_id",
                table: "UserDevices",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_UserDevices_tenant_id_user_id_token_hash",
                table: "UserDevices",
                columns: new[] { "tenant_id", "user_id", "token_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserDevices_user_id",
                table: "UserDevices",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Tenants_tenant_id",
                table: "AuditLogs",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BookingNumbers_Tenants_tenant_id",
                table: "BookingNumbers",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarCategories_Tenants_tenant_id",
                table: "CalendarCategories",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarEventParticipants_Tenants_tenant_id",
                table: "CalendarEventParticipants",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarEvents_Tenants_tenant_id",
                table: "CalendarEvents",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarEventSubscriptions_Tenants_tenant_id",
                table: "CalendarEventSubscriptions",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarFeedTokens_Tenants_tenant_id",
                table: "CalendarFeedTokens",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarReminderLogs_Tenants_tenant_id",
                table: "CalendarReminderLogs",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CalendarSettings_Tenants_tenant_id",
                table: "CalendarSettings",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ContributionPlans_Tenants_tenant_id",
                table: "ContributionPlans",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contributions_Tenants_tenant_id",
                table: "Contributions",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CreditorConfigs_Tenants_tenant_id",
                table: "CreditorConfigs",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IncomeCategories_Tenants_tenant_id",
                table: "IncomeCategories",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Incomes_Tenants_tenant_id",
                table: "Incomes",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LinkSettings_Tenants_tenant_id",
                table: "LinkSettings",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MailSettings_Tenants_tenant_id",
                table: "MailSettings",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MemberCategories_Tenants_tenant_id",
                table: "MemberCategories",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Members_Tenants_tenant_id",
                table: "Members",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Tenants_tenant_id",
                table: "Notifications",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentAcknowledgements_Tenants_tenant_id",
                table: "PaymentAcknowledgements",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PendingSelfEnrollments_Tenants_tenant_id",
                table: "PendingSelfEnrollments",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptCategories_Tenants_tenant_id",
                table: "ReceiptCategories",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptFiles_Tenants_tenant_id",
                table: "ReceiptFiles",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Receipts_Tenants_tenant_id",
                table: "Receipts",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SepaExport_Tenants_tenant_id",
                table: "SepaExport",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRecoveryCodes_Tenants_tenant_id",
                table: "UserRecoveryCodes",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Tenants_tenant_id",
                table: "Users",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserSettings_Tenants_tenant_id",
                table: "UserSettings",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_web_page_config_Tenants_tenant_id",
                table: "web_page_config",
                column: "tenant_id",
                principalTable: "Tenants",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Tenants_tenant_id",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_BookingNumbers_Tenants_tenant_id",
                table: "BookingNumbers");

            migrationBuilder.DropForeignKey(
                name: "FK_CalendarCategories_Tenants_tenant_id",
                table: "CalendarCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_CalendarEventParticipants_Tenants_tenant_id",
                table: "CalendarEventParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_CalendarEvents_Tenants_tenant_id",
                table: "CalendarEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_CalendarEventSubscriptions_Tenants_tenant_id",
                table: "CalendarEventSubscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_CalendarFeedTokens_Tenants_tenant_id",
                table: "CalendarFeedTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_CalendarReminderLogs_Tenants_tenant_id",
                table: "CalendarReminderLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_CalendarSettings_Tenants_tenant_id",
                table: "CalendarSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_ContributionPlans_Tenants_tenant_id",
                table: "ContributionPlans");

            migrationBuilder.DropForeignKey(
                name: "FK_Contributions_Tenants_tenant_id",
                table: "Contributions");

            migrationBuilder.DropForeignKey(
                name: "FK_CreditorConfigs_Tenants_tenant_id",
                table: "CreditorConfigs");

            migrationBuilder.DropForeignKey(
                name: "FK_IncomeCategories_Tenants_tenant_id",
                table: "IncomeCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_Incomes_Tenants_tenant_id",
                table: "Incomes");

            migrationBuilder.DropForeignKey(
                name: "FK_LinkSettings_Tenants_tenant_id",
                table: "LinkSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_MailSettings_Tenants_tenant_id",
                table: "MailSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_MemberCategories_Tenants_tenant_id",
                table: "MemberCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_Members_Tenants_tenant_id",
                table: "Members");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Tenants_tenant_id",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentAcknowledgements_Tenants_tenant_id",
                table: "PaymentAcknowledgements");

            migrationBuilder.DropForeignKey(
                name: "FK_PendingSelfEnrollments_Tenants_tenant_id",
                table: "PendingSelfEnrollments");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptCategories_Tenants_tenant_id",
                table: "ReceiptCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptFiles_Tenants_tenant_id",
                table: "ReceiptFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_Receipts_Tenants_tenant_id",
                table: "Receipts");

            migrationBuilder.DropForeignKey(
                name: "FK_SepaExport_Tenants_tenant_id",
                table: "SepaExport");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRecoveryCodes_Tenants_tenant_id",
                table: "UserRecoveryCodes");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Tenants_tenant_id",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_UserSettings_Tenants_tenant_id",
                table: "UserSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_web_page_config_Tenants_tenant_id",
                table: "web_page_config");

            migrationBuilder.DropTable(
                name: "UserDevices");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_web_page_config_tenant_id",
                table: "web_page_config");

            migrationBuilder.DropIndex(
                name: "IX_UserSettings_tenant_id",
                table: "UserSettings");

            migrationBuilder.DropIndex(
                name: "IX_Users_tenant_id",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_UserRecoveryCodes_tenant_id",
                table: "UserRecoveryCodes");

            migrationBuilder.DropIndex(
                name: "IX_SepaExport_tenant_id",
                table: "SepaExport");

            migrationBuilder.DropIndex(
                name: "IX_Receipts_tenant_id",
                table: "Receipts");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptFiles_tenant_id",
                table: "ReceiptFiles");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptCategories_tenant_id",
                table: "ReceiptCategories");

            migrationBuilder.DropIndex(
                name: "IX_PendingSelfEnrollments_tenant_id",
                table: "PendingSelfEnrollments");

            migrationBuilder.DropIndex(
                name: "IX_PaymentAcknowledgements_tenant_id",
                table: "PaymentAcknowledgements");

            migrationBuilder.DropIndex(
                name: "IX_PaymentAcknowledgements_tenant_id_year_sequence",
                table: "PaymentAcknowledgements");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_tenant_id",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Members_tenant_id",
                table: "Members");

            migrationBuilder.DropIndex(
                name: "IX_MemberCategories_tenant_id",
                table: "MemberCategories");

            migrationBuilder.DropIndex(
                name: "IX_MailSettings_tenant_id",
                table: "MailSettings");

            migrationBuilder.DropIndex(
                name: "IX_LinkSettings_tenant_id",
                table: "LinkSettings");

            migrationBuilder.DropIndex(
                name: "IX_Incomes_tenant_id",
                table: "Incomes");

            migrationBuilder.DropIndex(
                name: "IX_IncomeCategories_tenant_id",
                table: "IncomeCategories");

            migrationBuilder.DropIndex(
                name: "IX_CreditorConfigs_tenant_id",
                table: "CreditorConfigs");

            migrationBuilder.DropIndex(
                name: "IX_Contributions_tenant_id",
                table: "Contributions");

            migrationBuilder.DropIndex(
                name: "IX_ContributionPlans_tenant_id",
                table: "ContributionPlans");

            migrationBuilder.DropIndex(
                name: "IX_CalendarSettings_tenant_id",
                table: "CalendarSettings");

            migrationBuilder.DropIndex(
                name: "IX_CalendarReminderLogs_tenant_id",
                table: "CalendarReminderLogs");

            migrationBuilder.DropIndex(
                name: "IX_CalendarFeedTokens_tenant_id",
                table: "CalendarFeedTokens");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEventSubscriptions_tenant_id",
                table: "CalendarEventSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEvents_tenant_id",
                table: "CalendarEvents");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEventParticipants_tenant_id",
                table: "CalendarEventParticipants");

            migrationBuilder.DropIndex(
                name: "IX_CalendarCategories_tenant_id",
                table: "CalendarCategories");

            migrationBuilder.DropIndex(
                name: "IX_BookingNumbers_tenant_id",
                table: "BookingNumbers");

            migrationBuilder.DropIndex(
                name: "IX_BookingNumbers_tenant_id_year_kind_sequence",
                table: "BookingNumbers");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_tenant_id",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "web_page_config");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "UserSettings");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "UserRecoveryCodes");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "SepaExport");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "ReceiptFiles");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "ReceiptCategories");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "PendingSelfEnrollments");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "PaymentAcknowledgements");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "MemberCategories");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "MailSettings");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "LinkSettings");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "Incomes");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "IncomeCategories");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "CreditorConfigs");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "ContributionPlans");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "CalendarSettings");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "CalendarReminderLogs");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "CalendarFeedTokens");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "CalendarEventSubscriptions");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "CalendarEventParticipants");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "CalendarCategories");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "BookingNumbers");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "AuditLogs");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAcknowledgements_year_sequence",
                table: "PaymentAcknowledgements",
                columns: new[] { "year", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingNumbers_year_kind_sequence",
                table: "BookingNumbers",
                columns: new[] { "year", "kind", "sequence" },
                unique: true);
        }
    }
}
