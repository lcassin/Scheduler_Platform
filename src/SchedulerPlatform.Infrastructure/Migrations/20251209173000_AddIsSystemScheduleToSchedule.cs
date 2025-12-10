using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchedulerPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsSystemScheduleToSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSystemSchedule",
                table: "Schedule",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Mark existing seeded schedules as system schedules
            migrationBuilder.Sql(@"
                UPDATE [Schedule] 
                SET IsSystemSchedule = 1 
                WHERE Name IN ('Daily Log Cleanup', 'ADR Account Sync', 'ADR Full Cycle')
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSystemSchedule",
                table: "Schedule");
        }
    }
}
