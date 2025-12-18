--

USE SchedulerPlatform;
GO

PRINT '========================================';
PRINT 'Starting Quartz Tables Cleanup';
PRINT '========================================';
PRINT '';

PRINT 'Disabling foreign key constraints...';
ALTER TABLE QRTZ_TRIGGERS NOCHECK CONSTRAINT ALL;
ALTER TABLE QRTZ_CRON_TRIGGERS NOCHECK CONSTRAINT ALL;
ALTER TABLE QRTZ_SIMPLE_TRIGGERS NOCHECK CONSTRAINT ALL;
ALTER TABLE QRTZ_SIMPROP_TRIGGERS NOCHECK CONSTRAINT ALL;
ALTER TABLE QRTZ_BLOB_TRIGGERS NOCHECK CONSTRAINT ALL;
ALTER TABLE QRTZ_FIRED_TRIGGERS NOCHECK CONSTRAINT ALL;
ALTER TABLE QRTZ_PAUSED_TRIGGER_GRPS NOCHECK CONSTRAINT ALL;
PRINT 'Foreign key constraints disabled.';
PRINT '';

PRINT 'Clearing Quartz tables...';
PRINT '';

DECLARE @FiredTriggersCount INT;
SELECT @FiredTriggersCount = COUNT(*) FROM QRTZ_FIRED_TRIGGERS;
DELETE FROM QRTZ_FIRED_TRIGGERS;
PRINT 'Cleared QRTZ_FIRED_TRIGGERS: ' + CAST(@FiredTriggersCount AS VARCHAR(10)) + ' rows deleted';

DECLARE @CronTriggersCount INT;
SELECT @CronTriggersCount = COUNT(*) FROM QRTZ_CRON_TRIGGERS;
DELETE FROM QRTZ_CRON_TRIGGERS;
PRINT 'Cleared QRTZ_CRON_TRIGGERS: ' + CAST(@CronTriggersCount AS VARCHAR(10)) + ' rows deleted';

DECLARE @SimpleTriggersCount INT;
SELECT @SimpleTriggersCount = COUNT(*) FROM QRTZ_SIMPLE_TRIGGERS;
DELETE FROM QRTZ_SIMPLE_TRIGGERS;
PRINT 'Cleared QRTZ_SIMPLE_TRIGGERS: ' + CAST(@SimpleTriggersCount AS VARCHAR(10)) + ' rows deleted';

DECLARE @SimplePropTriggersCount INT;
SELECT @SimplePropTriggersCount = COUNT(*) FROM QRTZ_SIMPROP_TRIGGERS;
DELETE FROM QRTZ_SIMPROP_TRIGGERS;
PRINT 'Cleared QRTZ_SIMPROP_TRIGGERS: ' + CAST(@SimplePropTriggersCount AS VARCHAR(10)) + ' rows deleted';

DECLARE @BlobTriggersCount INT;
SELECT @BlobTriggersCount = COUNT(*) FROM QRTZ_BLOB_TRIGGERS;
DELETE FROM QRTZ_BLOB_TRIGGERS;
PRINT 'Cleared QRTZ_BLOB_TRIGGERS: ' + CAST(@BlobTriggersCount AS VARCHAR(10)) + ' rows deleted';

DECLARE @PausedTriggerGrpsCount INT;
SELECT @PausedTriggerGrpsCount = COUNT(*) FROM QRTZ_PAUSED_TRIGGER_GRPS;
DELETE FROM QRTZ_PAUSED_TRIGGER_GRPS;
PRINT 'Cleared QRTZ_PAUSED_TRIGGER_GRPS: ' + CAST(@PausedTriggerGrpsCount AS VARCHAR(10)) + ' rows deleted';

DECLARE @TriggersCount INT;
SELECT @TriggersCount = COUNT(*) FROM QRTZ_TRIGGERS;
DELETE FROM QRTZ_TRIGGERS;
PRINT 'Cleared QRTZ_TRIGGERS: ' + CAST(@TriggersCount AS VARCHAR(10)) + ' rows deleted';

DECLARE @JobDetailsCount INT;
SELECT @JobDetailsCount = COUNT(*) FROM QRTZ_JOB_DETAILS;
DELETE FROM QRTZ_JOB_DETAILS;
PRINT 'Cleared QRTZ_JOB_DETAILS: ' + CAST(@JobDetailsCount AS VARCHAR(10)) + ' rows deleted';

DECLARE @CalendarsCount INT;
SELECT @CalendarsCount = COUNT(*) FROM QRTZ_CALENDARS;
DELETE FROM QRTZ_CALENDARS;
PRINT 'Cleared QRTZ_CALENDARS: ' + CAST(@CalendarsCount AS VARCHAR(10)) + ' rows deleted';

DECLARE @LocksCount INT;
SELECT @LocksCount = COUNT(*) FROM QRTZ_LOCKS;
DELETE FROM QRTZ_LOCKS;
PRINT 'Cleared QRTZ_LOCKS: ' + CAST(@LocksCount AS VARCHAR(10)) + ' rows deleted';

DECLARE @SchedulerStateCount INT;
SELECT @SchedulerStateCount = COUNT(*) FROM QRTZ_SCHEDULER_STATE;
DELETE FROM QRTZ_SCHEDULER_STATE;
PRINT 'Cleared QRTZ_SCHEDULER_STATE: ' + CAST(@SchedulerStateCount AS VARCHAR(10)) + ' rows deleted';

PRINT '';

PRINT 'Re-enabling foreign key constraints...';
ALTER TABLE QRTZ_TRIGGERS CHECK CONSTRAINT ALL;
ALTER TABLE QRTZ_CRON_TRIGGERS CHECK CONSTRAINT ALL;
ALTER TABLE QRTZ_SIMPLE_TRIGGERS CHECK CONSTRAINT ALL;
ALTER TABLE QRTZ_SIMPROP_TRIGGERS CHECK CONSTRAINT ALL;
ALTER TABLE QRTZ_BLOB_TRIGGERS CHECK CONSTRAINT ALL;
ALTER TABLE QRTZ_FIRED_TRIGGERS CHECK CONSTRAINT ALL;
ALTER TABLE QRTZ_PAUSED_TRIGGER_GRPS CHECK CONSTRAINT ALL;
PRINT 'Foreign key constraints re-enabled.';
PRINT '';

PRINT '========================================';
PRINT 'Quartz Tables Cleanup Complete';
PRINT '========================================';
PRINT 'Total rows deleted:';
PRINT '  - Fired Triggers: ' + CAST(@FiredTriggersCount AS VARCHAR(10));
PRINT '  - Cron Triggers: ' + CAST(@CronTriggersCount AS VARCHAR(10));
PRINT '  - Simple Triggers: ' + CAST(@SimpleTriggersCount AS VARCHAR(10));
PRINT '  - SimpleProp Triggers: ' + CAST(@SimplePropTriggersCount AS VARCHAR(10));
PRINT '  - Blob Triggers: ' + CAST(@BlobTriggersCount AS VARCHAR(10));
PRINT '  - Paused Trigger Groups: ' + CAST(@PausedTriggerGrpsCount AS VARCHAR(10));
PRINT '  - Triggers: ' + CAST(@TriggersCount AS VARCHAR(10));
PRINT '  - Job Details: ' + CAST(@JobDetailsCount AS VARCHAR(10));
PRINT '  - Calendars: ' + CAST(@CalendarsCount AS VARCHAR(10));
PRINT '  - Locks: ' + CAST(@LocksCount AS VARCHAR(10));
PRINT '  - Scheduler State: ' + CAST(@SchedulerStateCount AS VARCHAR(10));
PRINT '';
PRINT 'All Quartz data has been cleared.';
PRINT 'Restart the API to allow Quartz to reinitialize.';
PRINT '========================================';
GO
