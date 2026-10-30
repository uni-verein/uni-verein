using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniVerein.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddIncomeTwoFactorAndEurExport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "session_version",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "totp_enabled",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "totp_last_used_step",
                table: "Users",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "totp_secret",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "payment_date",
                table: "Receipts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_area",
                table: "ReceiptCategories",
                type: "text",
                nullable: false,
                defaultValue: "NON_PROFIT");

            migrationBuilder.AddColumn<string>(
                name: "tax_number",
                table: "CreditorConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "payment_date",
                table: "Contributions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BookingNumbers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    contribution_id = table.Column<Guid>(type: "uuid", nullable: true),
                    income_id = table.Column<Guid>(type: "uuid", nullable: true),
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingNumbers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "IncomeCategories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    tax_area = table.Column<string>(type: "text", nullable: false, defaultValue: "NON_PROFIT"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomeCategories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentAcknowledgements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    contribution_id = table.Column<Guid>(type: "uuid", nullable: true),
                    income_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payer_name = table.Column<string>(type: "text", nullable: false),
                    payer_address = table.Column<string>(type: "text", nullable: true),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    purpose = table.Column<string>(type: "text", nullable: false),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    issuer_name = table.Column<string>(type: "text", nullable: false),
                    issuer_address = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAcknowledgements", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "UserRecoveryCodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code_hash = table.Column<string>(type: "text", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRecoveryCodes", x => x.id);
                    table.ForeignKey(
                        name: "FK_UserRecoveryCodes_Users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Incomes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payer = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    income_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    paid = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    payment_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incomes", x => x.id);
                    table.ForeignKey(
                        name: "FK_Incomes_IncomeCategories_category_id",
                        column: x => x.category_id,
                        principalTable: "IncomeCategories",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingNumbers_contribution_id",
                table: "BookingNumbers",
                column: "contribution_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingNumbers_income_id",
                table: "BookingNumbers",
                column: "income_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingNumbers_receipt_id",
                table: "BookingNumbers",
                column: "receipt_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingNumbers_year_kind_sequence",
                table: "BookingNumbers",
                columns: new[] { "year", "kind", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Incomes_category_id",
                table: "Incomes",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAcknowledgements_contribution_id",
                table: "PaymentAcknowledgements",
                column: "contribution_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAcknowledgements_income_id",
                table: "PaymentAcknowledgements",
                column: "income_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAcknowledgements_year_sequence",
                table: "PaymentAcknowledgements",
                columns: new[] { "year", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRecoveryCodes_user_id_code_hash",
                table: "UserRecoveryCodes",
                columns: new[] { "user_id", "code_hash" });

            migrationBuilder.Sql("UPDATE \"Contributions\" SET deleted_at = NULL WHERE paid IS NOT NULL AND deleted_at IS NOT NULL;");

            migrationBuilder.Sql("""
                UPDATE "Contributions" SET payment_date = due_date WHERE paid IS NOT NULL;
                UPDATE "Incomes" SET payment_date = income_date WHERE paid IS NOT NULL;
                UPDATE "Receipts" SET payment_date = receipt_date WHERE paid IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                INSERT INTO "BookingNumbers" (id, year, kind, sequence, contribution_id, income_id, receipt_id, created_at)
                SELECT gen_random_uuid(), s.yr, 'INCOME',
                       ROW_NUMBER() OVER (PARTITION BY s.yr ORDER BY s.payment_date, s.created_at, s.id),
                       s.contribution_id, s.income_id, NULL, now()
                FROM (
                    SELECT id, payment_date, created_at,
                           CAST(EXTRACT(YEAR FROM payment_date AT TIME ZONE 'UTC') AS integer) AS yr,
                           id AS contribution_id, NULL::uuid AS income_id
                    FROM "Contributions" WHERE paid IS NOT NULL
                    UNION ALL
                    SELECT id, payment_date, created_at,
                           CAST(EXTRACT(YEAR FROM payment_date AT TIME ZONE 'UTC') AS integer) AS yr,
                           NULL::uuid AS contribution_id, id AS income_id
                    FROM "Incomes" WHERE paid IS NOT NULL
                ) s;

                INSERT INTO "BookingNumbers" (id, year, kind, sequence, contribution_id, income_id, receipt_id, created_at)
                SELECT gen_random_uuid(), s.yr, 'EXPENSE',
                       ROW_NUMBER() OVER (PARTITION BY s.yr ORDER BY s.payment_date, s.created_at, s.id),
                       NULL, NULL, s.id, now()
                FROM (
                    SELECT id, payment_date, created_at,
                           CAST(EXTRACT(YEAR FROM payment_date AT TIME ZONE 'UTC') AS integer) AS yr
                    FROM "Receipts" WHERE paid IS NOT NULL
                ) s;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingNumbers");

            migrationBuilder.DropTable(
                name: "Incomes");

            migrationBuilder.DropTable(
                name: "PaymentAcknowledgements");

            migrationBuilder.DropTable(
                name: "UserRecoveryCodes");

            migrationBuilder.DropTable(
                name: "IncomeCategories");

            migrationBuilder.DropColumn(
                name: "session_version",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "totp_enabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "totp_last_used_step",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "totp_secret",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "payment_date",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "tax_area",
                table: "ReceiptCategories");

            migrationBuilder.DropColumn(
                name: "tax_number",
                table: "CreditorConfigs");

            migrationBuilder.DropColumn(
                name: "payment_date",
                table: "Contributions");
        }
    }
}
