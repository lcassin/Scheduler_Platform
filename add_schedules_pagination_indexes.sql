-- Indexes to optimize schedules pagination with 3M+ records
-- Run this script against your SchedulerPlatform database

-- Index for browsing schedules with filtering and sorting
-- Supports: WHERE IsDeleted=0, optional ClientId filter, ORDER BY Name,Id
-- Includes commonly displayed columns to avoid key lookups
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Schedules_Browse' AND object_id = OBJECT_ID('dbo.Schedules'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Schedules_Browse 
    ON dbo.Schedules (IsDeleted, ClientId, Name, Id) 
    INCLUDE (IsEnabled, NextRunTime, LastRunTime, CronExpression, JobType, CreatedAt, UpdatedAt);
    PRINT 'Created index IX_Schedules_Browse on Schedules table';
END
ELSE
BEGIN
    PRINT 'Index IX_Schedules_Browse already exists on Schedules table';
END
GO

-- Index for fetching last execution per schedule
-- Supports: TOP 1 ORDER BY StartTime DESC per ScheduleId
-- Includes Status and StartTime to avoid key lookups
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JobExecutions_ScheduleId_StartTime' AND object_id = OBJECT_ID('dbo.JobExecutions'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_JobExecutions_ScheduleId_StartTime 
    ON dbo.JobExecutions (ScheduleId, StartTime DESC) 
    INCLUDE (Status, EndTime);
    PRINT 'Created index IX_JobExecutions_ScheduleId_StartTime on JobExecutions table';
END
ELSE
BEGIN
    PRINT 'Index IX_JobExecutions_ScheduleId_StartTime already exists on JobExecutions table';
END
GO

PRINT 'Schedules pagination indexes created successfully!';
