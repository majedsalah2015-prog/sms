using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// core.CalendarEvent gains IsActive so an event can be cancelled instead of deleted
    /// (BR-GLB-005, doc/Modules/04 §8.2).
    /// </summary>
    public partial class CalendarEventCancellation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue: true, not EF's generated false — every event already on a school's
            // calendar is a live one, and shipping this with the default would have opened the
            // board with every holiday of the year marked cancelled.
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "core",
                table: "CalendarEvent",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "core",
                table: "CalendarEvent");
        }
    }
}
