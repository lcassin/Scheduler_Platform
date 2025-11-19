using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchedulerPlatform.Infrastructure.Migrations
{
    public partial class AddSchedulesPaginationIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX IX_Schedules_Browse 
                ON dbo.Schedules (IsDeleted, ClientId, Name, Id) 
                INCLUDE (IsEnabled, NextRunTime, LastRunTime, CronExpression, JobType, CreatedAt, UpdatedAt);
            ");

            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX IX_JobExecutions_ScheduleId_StartTime 
                ON dbo.JobExecutions (ScheduleId, StartTime DESC) 
                INCLUDE (Status, EndTime);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_Schedules_Browse ON dbo.Schedules;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_JobExecutions_ScheduleId_StartTime ON dbo.JobExecutions;");
        }
    }
}
