using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchedulerPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes 
                               WHERE name = N'IX_Schedules_ClientId_Name'
                                 AND object_id = OBJECT_ID(N'[dbo].[Schedules]'))
                BEGIN
                    CREATE NONCLUSTERED INDEX [IX_Schedules_ClientId_Name] 
                    ON [dbo].[Schedules] ([ClientId], [Name]) 
                    INCLUDE ([CronExpression], [Frequency], [NextRunTime]);
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes 
                               WHERE name = N'IX_ScheduleSyncSources_NotDeleted_LastSyncedAt'
                                 AND object_id = OBJECT_ID(N'[dbo].[ScheduleSyncSources]'))
                BEGIN
                    CREATE NONCLUSTERED INDEX [IX_ScheduleSyncSources_NotDeleted_LastSyncedAt] 
                    ON [dbo].[ScheduleSyncSources] ([LastSyncedAt], [ExternalClientId], [ExternalVendorId], [AccountNumber], [ScheduleFrequency]) 
                    INCLUDE ([VendorName], [LastInvoiceDate]) 
                    WHERE [IsDeleted] = 0;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes 
                           WHERE name = N'IX_Schedules_ClientId_Name'
                             AND object_id = OBJECT_ID(N'[dbo].[Schedules]'))
                BEGIN
                    DROP INDEX [IX_Schedules_ClientId_Name] ON [dbo].[Schedules];
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes 
                           WHERE name = N'IX_ScheduleSyncSources_NotDeleted_LastSyncedAt'
                             AND object_id = OBJECT_ID(N'[dbo].[ScheduleSyncSources]'))
                BEGIN
                    DROP INDEX [IX_ScheduleSyncSources_NotDeleted_LastSyncedAt] ON [dbo].[ScheduleSyncSources];
                END
            ");
        }
    }
}
