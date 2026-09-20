using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniVerein.DAL.Migrations
{
    /// <inheritdoc />
    [ExcludeFromCodeCoverage]
    public partial class AddPendingSelfEnrollment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingSelfEnrollments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    confirmation_token_hash = table.Column<string>(type: "text", nullable: false),
                    confirmation_token_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    submitted_ip = table.Column<string>(type: "text", nullable: false),
                    gender = table.Column<string>(type: "text", nullable: false),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    middle_name = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    birthday = table.Column<string>(type: "text", nullable: false),
                    street = table.Column<string>(type: "text", nullable: false),
                    postal_code = table.Column<string>(type: "text", nullable: false),
                    city = table.Column<string>(type: "text", nullable: false),
                    country_code = table.Column<string>(type: "text", nullable: true),
                    email_encrypted = table.Column<string>(type: "text", nullable: false),
                    email_hash = table.Column<string>(type: "text", nullable: false),
                    phone = table.Column<string>(type: "text", nullable: false),
                    bulk_mail = table.Column<string>(type: "text", nullable: false),
                    start_of_studies = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_of_studies = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    academic_degree = table.Column<string>(type: "text", nullable: true),
                    course_of_study = table.Column<string>(type: "text", nullable: false),
                    iban_encrypted = table.Column<string>(type: "text", nullable: true),
                    iban_hash = table.Column<string>(type: "text", nullable: true),
                    bic_encrypted = table.Column<string>(type: "text", nullable: true),
                    sepa_consent = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    member_category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contribution_plan_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingSelfEnrollments", x => x.id);
                    table.ForeignKey(
                        name: "FK_PendingSelfEnrollments_ContributionPlans_contribution_plan_~",
                        column: x => x.contribution_plan_id,
                        principalTable: "ContributionPlans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PendingSelfEnrollments_MemberCategories_member_category_id",
                        column: x => x.member_category_id,
                        principalTable: "MemberCategories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingSelfEnrollments_contribution_plan_id",
                table: "PendingSelfEnrollments",
                column: "contribution_plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_PendingSelfEnrollments_member_category_id",
                table: "PendingSelfEnrollments",
                column: "member_category_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingSelfEnrollments");
        }
    }
}
