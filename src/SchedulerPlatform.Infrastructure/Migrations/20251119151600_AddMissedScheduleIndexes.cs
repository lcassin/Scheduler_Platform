using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchedulerPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMissedScheduleIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes 
                               WHERE name = N'IX_Schedules_MissedScheduleQuery'
                                 AND object_id = OBJECT_ID(N'[dbo].[Schedules]'))
                BEGIN
                    CREATE NONCLUSTERED INDEX [IX_Schedules_MissedScheduleQuery] 
                    ON [dbo].[Schedules] ([IsDeleted], [IsEnabled], [NextRunTime]) 
                    INCLUDE ([ClientId], [Name], [CronExpression], [TimeZone]);
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes 
                           WHERE name = N'IX_Schedules_MissedScheduleQuery'
                             AND object_id = OBJECT_ID(N'[dbo].[Schedules]'))
                BEGIN
                    DROP INDEX [IX_Schedules_MissedScheduleQuery] ON [dbo].[Schedules];
                END
            ");
        }
    }
}
