using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniVerein.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddMembershipStatusToMembers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "membership_status",
                table: "Members",
                type: "text",
                nullable: false,
                defaultValue: "MEMBER");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "membership_status",
                table: "Members");
        }
    }
}
