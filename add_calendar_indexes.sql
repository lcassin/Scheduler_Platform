-- Index to optimize calendar queries with date range filtering
-- Run this script against your SchedulerPlatform database

-- Index for calendar view filtering by date range
-- Supports: WHERE IsDeleted=0, optional ClientId filter, NextRunTime BETWEEN start AND end
-- Includes commonly displayed columns to avoid key lookups
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Schedules_Calendar' AND object_id = OBJECT_ID('dbo.Schedules'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Schedules_Calendar 
    ON dbo.Schedules (IsDeleted, ClientId, NextRunTime) 
    INCLUDE (Id, Name, TimeZone, IsEnabled);
    PRINT 'Created index IX_Schedules_Calendar on Schedules table';
END
ELSE
BEGIN
    PRINT 'Index IX_Schedules_Calendar already exists on Schedules table';
END
GO

PRINT 'Calendar indexes created successfully!';
