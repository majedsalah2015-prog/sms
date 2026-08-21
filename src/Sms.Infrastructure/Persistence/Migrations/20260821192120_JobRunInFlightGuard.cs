using Microsoft.EntityFrameworkCore.Migrations;

namespace Sms.Infrastructure.Persistence.Migrations
{
    public partial class JobRunInFlightGuard : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_JobRun_InFlight",
                schema: "ops",
                table: "JobRun",
                column: "JobDefinitionId",
                unique: true,
                filter: "[Status] = 1");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_JobRun_InFlight",
                schema: "ops",
                table: "JobRun");
        }
    }
}
