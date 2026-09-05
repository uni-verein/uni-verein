using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniVerein.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddSelfEnrollmentEnabledToWebPageConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "self_enrollment_enabled",
                table: "web_page_config",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "self_enrollment_enabled",
                table: "web_page_config");
        }
    }
}
