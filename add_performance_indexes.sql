
USE [SchedulerPlatform_Dev];
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes 
               WHERE name = N'IX_Schedules_ClientId_Name'
                 AND object_id = OBJECT_ID(N'[dbo].[Schedules]'))
BEGIN
    PRINT 'Creating index IX_Schedules_ClientId_Name...';
    CREATE NONCLUSTERED INDEX [IX_Schedules_ClientId_Name] 
    ON [dbo].[Schedules] ([ClientId], [Name]) 
    INCLUDE ([CronExpression], [Frequency], [NextRunTime]);
    PRINT 'Index IX_Schedules_ClientId_Name created successfully.';
END
ELSE
BEGIN
    PRINT 'Index IX_Schedules_ClientId_Name already exists.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes 
               WHERE name = N'IX_ScheduleSyncSources_NotDeleted_LastSyncedAt'
                 AND object_id = OBJECT_ID(N'[dbo].[ScheduleSyncSources]'))
BEGIN
    PRINT 'Creating index IX_ScheduleSyncSources_NotDeleted_LastSyncedAt...';
    CREATE NONCLUSTERED INDEX [IX_ScheduleSyncSources_NotDeleted_LastSyncedAt] 
    ON [dbo].[ScheduleSyncSources] ([LastSyncedAt], [ExternalClientId], [ExternalVendorId], [AccountNumber], [ScheduleFrequency]) 
    INCLUDE ([VendorName], [LastInvoiceDate]) 
    WHERE [IsDeleted] = 0;
    PRINT 'Index IX_ScheduleSyncSources_NotDeleted_LastSyncedAt created successfully.';
END
ELSE
BEGIN
    PRINT 'Index IX_ScheduleSyncSources_NotDeleted_LastSyncedAt already exists.';
END
GO

PRINT '';
PRINT '========================================';
PRINT 'Performance indexes applied successfully!';
PRINT '========================================';
PRINT '';
PRINT 'Next steps:';
PRINT '1. Run UPDATE STATISTICS or EXEC sp_updatestats to refresh statistics';
PRINT '2. Test schedule generation with --start-from=schedules';
PRINT '3. Monitor query performance and adjust if needed';
GO
