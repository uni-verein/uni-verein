using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniVerein.DAL.Migrations
{
    /// <inheritdoc />
    [ExcludeFromCodeCoverage]
    public partial class AddCalendarRecurrenceAndReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CalendarEvents_start_at_end_at",
                table: "CalendarEvents");

            migrationBuilder.AddColumn<string>(
                name: "recurrence_exceptions",
                table: "CalendarEvents",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "recurrence_rule",
                table: "CalendarEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "reminder_minutes_before",
                table: "CalendarEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "series_end_at",
                table: "CalendarEvents",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            // Existing events are all single events, so their series ends with the event itself.
            migrationBuilder.Sql("UPDATE \"CalendarEvents\" SET series_end_at = end_at;");

            migrationBuilder.CreateTable(
                name: "CalendarReminderLogs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurrence_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarReminderLogs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_start_at_series_end_at",
                table: "CalendarEvents",
                columns: new[] { "start_at", "series_end_at" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarReminderLogs_event_id_occurrence_start",
                table: "CalendarReminderLogs",
                columns: new[] { "event_id", "occurrence_start" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarReminderLogs");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEvents_start_at_series_end_at",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "recurrence_exceptions",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "recurrence_rule",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "reminder_minutes_before",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "series_end_at",
                table: "CalendarEvents");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_start_at_end_at",
                table: "CalendarEvents",
                columns: new[] { "start_at", "end_at" });
        }
    }
}
