using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchedulerPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJobExecutionDashboardIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_JobExecutions_StartTime_Status_Duration",
                table: "JobExecutions",
                columns: new[] { "StartTime", "Status" },
                descending: new[] { true, false })
                .Annotation("SqlServer:Include", new[] { "DurationSeconds", "ScheduleId", "EndTime" });

            migrationBuilder.CreateIndex(
                name: "IX_JobExecutions_ScheduleId_StartTime",
                table: "JobExecutions",
                columns: new[] { "ScheduleId", "StartTime" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JobExecutions_StartTime_Status_Duration",
                table: "JobExecutions");

            migrationBuilder.DropIndex(
                name: "IX_JobExecutions_ScheduleId_StartTime",
                table: "JobExecutions");
        }
    }
}
