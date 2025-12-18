--

USE SchedulerPlatform;
GO

SET NOCOUNT ON;
GO
SET DATEFORMAT ymd;
GO

        PRINT '========================================';
        PRINT 'Starting Permissions/Schedules Tables Cleanup';
        PRINT '========================================';
        PRINT '';

    ALTER TABLE [dbo].UserPermissions DROP CONSTRAINT FK_UserPermissions_Users_UserId;
    GO
    ALTER TABLE [dbo].PasswordHistories DROP CONSTRAINT FK_PasswordHistories_Users_UserId;
    GO
    ALTER TABLE [dbo].Schedules DROP CONSTRAINT [FK_Schedules_Clients_ClientId];
    GO
    ALTER TABLE [dbo].ScheduleSyncSources DROP CONSTRAINT [FK_ScheduleSyncSources_Clients_ClientId];
    GO
    ALTER TABLE [dbo].Users DROP CONSTRAINT [FK_Users_Clients_ClientId];
    GO
    ALTER TABLE [dbo].NotificationSettings DROP CONSTRAINT [FK_NotificationSettings_Schedules_ScheduleId];
    GO
    ALTER TABLE [dbo].JobParameters DROP CONSTRAINT [FK_JobParameters_Schedules_ScheduleId];
    GO
    ALTER TABLE [dbo].JobExecutions DROP CONSTRAINT [FK_JobExecutions_Schedules_ScheduleId];
    GO
   
    TRUNCATE TABLE NotificationSettings
    GO
    TRUNCATE TABLE JobExecutions
    GO
    TRUNCATE TABLE JobParameters
    GO
    TRUNCATE TABLE Schedules
    GO

    TRUNCATE TABLE ScheduleSyncSources
    GO
    
    TRUNCATE TABLE [dbo].[Clients];
    GO

    TRUNCATE TABLE Users
    GO

    TRUNCATE TABLE PasswordHistories
    GO

    INSERT INTO [Schedules] (Name, Description, ClientId, JobType, Frequency, CronExpression, NextRunTime, LastRunTime, IsEnabled, MaxRetries, RetryDelayMinutes, TimeZone, JobConfiguration, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted, TimeoutMinutes) VALUES
    (N'Daily Log Cleanup', N'Automatically deletes log files older than 7 days from API and IdentityServer directories', 1, 1, 1, N'0 0 2 * * ?', CONVERT(DATETIME2, '2025-11-21 08:00:00.0000000', 121), NULL, CONVERT(bit, 'True'), 3, 5, N'Central Standard Time', N'{"ExecutablePath":"C:\\Users\\LCassin\\source\\repos\\Scheduler_Platform\\src\\SchedulerPlatform.LogCleanup\\bin\\Release\\net10.0\\SchedulerPlatform.LogCleanup.exe","Arguments":"1","WorkingDirectory":"C:\\Users\\LCassin\\source\\repos\\Scheduler_Platform\\src\\SchedulerPlatform.LogCleanup\\bin\\Release\\net10.0","TimeoutSeconds":300}', CONVERT(DATETIME2, '2025-10-24 23:03:13.4966667', 121), CONVERT(DATETIME2, '2025-11-20 20:32:11.0045378', 121), N'System', N'Default Admin', CONVERT(bit, 'False'), NULL)

    DECLARE @NewSchedule BIGINT = SCOPE_IDENTITY()

    
    INSERT INTO [Clients] (ClientName, ClientCode, IsActive, ContactEmail, ContactPhone, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted, LastSyncedAt, ExternalClientId) VALUES
    (N'Cass Information Systems', N'INTERNAL', CONVERT(bit, 'True'), N'lcassin@cassinfo.com', N'864-201-2796', CONVERT(DATETIME2, '2025-10-22 11:41:16.2500000', 121), CONVERT(DATETIME2, '2025-11-18 18:45:18.6169732', 121), N'Lee Cassin', N'ApiSync', CONVERT(bit, 'True'), NULL, 0)

    INSERT INTO [JobExecutions] (ScheduleId, StartTime, EndTime, Status, Output, ErrorMessage, StackTrace, RetryCount, DurationSeconds, TriggeredBy, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted, CancelledBy) VALUES
    (@NewSchedule, CONVERT(DATETIME2, '2025-11-14 19:32:56.7142008', 121), CONVERT(DATETIME2, '2025-11-14 19:32:57.0431620', 121), 3, N'[2025-11-14 14:32:56] Starting log cleanup process...
    [2025-11-14 14:32:57] Deleting log files older than 2025-11-13
    [2025-11-14 14:32:57] Scanning directory: C:\Users\LCassin\source\repos\Scheduler_Platform\src\SchedulerPlatform.API\logs
    [2025-11-14 14:32:57] Deleted: scheduler-api-20251113_003.txt (3,770 bytes)
    [2025-11-14 14:32:57] Deleted: scheduler-api-20251113_004.txt (3,770 bytes)
    [2025-11-14 14:32:57] Scanning directory: C:\Users\LCassin\source\repos\Scheduler_Platform\src\SchedulerPlatform.IdentityServer\Logs
    [2025-11-14 14:32:57] Deleted: identity-server-20251113_003.txt (58,993 bytes)
    [2025-11-14 14:32:57] Deleted: identity-server-20251113_004.txt (55,148 bytes)
    [2025-11-14 14:32:57] Log cleanup completed successfully
    [2025-11-14 14:32:57] Total files deleted: 4
    [2025-11-14 14:32:57] Total space freed: 0.12 MB
    ', NULL, NULL, 0, 0, N'Default Admin', CONVERT(DATETIME2, '0001-01-01 00:00:00.0000000', 121), NULL, NULL, NULL, CONVERT(bit, 'False'), NULL)


INSERT INTO [NotificationSettings] (ScheduleId, EnableSuccessNotifications, EnableFailureNotifications, SuccessEmailRecipients, FailureEmailRecipients, SuccessEmailSubject, FailureEmailSubject, IncludeExecutionDetails, IncludeOutput, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted) VALUES
(@NewSchedule, CONVERT(bit, 'False'), CONVERT(bit, 'True'), NULL, N'lcassin@cassinfo.com', NULL, N'Log Cleanup Job Failed', CONVERT(bit, 'True'), CONVERT(bit, 'False'), CONVERT(DATETIME2, '2025-10-24 23:03:13.5033333', 121), CONVERT(DATETIME2, '2025-11-14 16:36:34.0409745', 121), N'System', N'Default Admin', CONVERT(bit, 'False'))


INSERT INTO [Users] (Username, Email, FirstName, LastName, ClientId, IsActive, ExternalUserId, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted, ExternalIssuer, PasswordHash, IsSystemAdmin, LastLoginAt) VALUES
(N'superadmin', N'superadmin@cassinfo.com', N'Super', N'Admin', 1, CONVERT(bit, 'True'), NULL, CONVERT(DATETIME2, '2025-11-13 18:23:44.1700000', 121), NULL, N'System', NULL, CONVERT(bit, 'False'), NULL, NULL, CONVERT(bit, 'True'), NULL)

INSERT INTO [Users] (Username, Email, FirstName, LastName, ClientId, IsActive, ExternalUserId, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted, ExternalIssuer, PasswordHash, IsSystemAdmin, LastLoginAt) VALUES
(N'admin', N'admin@cassinfo.com', N'Default', N'Admin', 1, CONVERT(bit, 'True'), NULL, CONVERT(DATETIME2, '2025-11-13 18:23:44.1800000', 121), CONVERT(DATETIME2, '2025-11-20 20:53:20.3204627', 121), N'System', N'PasswordReset', CONVERT(bit, 'False'), NULL, N'AQAAAAEAACcQAAAAECNH0oZr0igG+UTX7F2NIU3JC0DSysyPWQX+KQoq/fr0XMNeqxNlxbiWtGd2LFMamA==', CONVERT(bit, 'True'), CONVERT(DATETIME2, '2025-11-20 20:53:20.3197674', 121))

INSERT INTO [Users] (Username, Email, FirstName, LastName, ClientId, IsActive, ExternalUserId, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted, ExternalIssuer, PasswordHash, IsSystemAdmin, LastLoginAt) VALUES
(N'viewer', N'viewer@cassinfo.com', N'View', N'Only', 1, CONVERT(bit, 'True'), NULL, CONVERT(DATETIME2, '2025-11-13 18:23:44.1900000', 121), CONVERT(DATETIME2, '2025-11-14 17:18:09.3215592', 121), N'System', N'PasswordReset', CONVERT(bit, 'False'), NULL, N'AQAAAAEAACcQAAAAEMJ24UU6gnEXVgabJEJuJdqqWFhhlVn3thwWSga2ugpNzsALvI8oKw+8YLzelgfqTA==', CONVERT(bit, 'False'), CONVERT(DATETIME2, '2025-11-14 17:18:09.3205089', 121))

INSERT INTO [Users] (Username, Email, FirstName, LastName, ClientId, IsActive, ExternalUserId, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted, ExternalIssuer, PasswordHash, IsSystemAdmin, LastLoginAt) VALUES
(N'editor', N'editor@cassinfo.com', N'Schedule', N'Editor', 1, CONVERT(bit, 'True'), NULL, CONVERT(DATETIME2, '2025-11-13 18:23:44.2033333', 121), CONVERT(DATETIME2, '2025-11-13 19:54:57.6800000', 121), N'System', N'PasswordReset', CONVERT(bit, 'False'), NULL, N'AQAAAAEAACcQAAAAEKluqq0CX8KwQmTEFtggSJWW9TXUHHg4q88ROzqeXivcBStj5DcvXZrpizNLxf00tA==', CONVERT(bit, 'False'), NULL)


INSERT INTO [UserPermissions] (UserId, PermissionName, ResourceType, ResourceId, CanCreate, CanRead, CanUpdate, CanDelete, CanExecute, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted) VALUES
(1, N'scheduler', NULL, NULL, CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(DATETIME2, '2025-11-13 18:23:44.1700000', 121), NULL, N'System', NULL, CONVERT(bit, 'False'))

INSERT INTO [UserPermissions] (UserId, PermissionName, ResourceType, ResourceId, CanCreate, CanRead, CanUpdate, CanDelete, CanExecute, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted) VALUES
(1, N'schedules', NULL, NULL, CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(DATETIME2, '2025-11-13 18:23:44.1700000', 121), NULL, N'System', NULL, CONVERT(bit, 'False'))

INSERT INTO [UserPermissions] (UserId, PermissionName, ResourceType, ResourceId, CanCreate, CanRead, CanUpdate, CanDelete, CanExecute, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted) VALUES
(1, N'jobs', NULL, NULL, CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(DATETIME2, '2025-11-13 18:23:44.1700000', 121), NULL, N'System', NULL, CONVERT(bit, 'False'))

INSERT INTO [UserPermissions] (UserId, PermissionName, ResourceType, ResourceId, CanCreate, CanRead, CanUpdate, CanDelete, CanExecute, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted) VALUES
(2, N'scheduler', NULL, NULL, CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(DATETIME2, '2025-11-13 18:23:44.1800000', 121), NULL, N'System', NULL, CONVERT(bit, 'False'))

INSERT INTO [UserPermissions] (UserId, PermissionName, ResourceType, ResourceId, CanCreate, CanRead, CanUpdate, CanDelete, CanExecute, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted) VALUES
(2, N'schedules', NULL, NULL, CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(DATETIME2, '2025-11-13 18:23:44.1800000', 121), NULL, N'System', NULL, CONVERT(bit, 'False'))

INSERT INTO [UserPermissions] (UserId, PermissionName, ResourceType, ResourceId, CanCreate, CanRead, CanUpdate, CanDelete, CanExecute, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted) VALUES
(2, N'jobs', NULL, NULL, CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(DATETIME2, '2025-11-13 18:23:44.1800000', 121), NULL, N'System', NULL, CONVERT(bit, 'False'))

INSERT INTO [UserPermissions] (UserId, PermissionName, ResourceType, ResourceId, CanCreate, CanRead, CanUpdate, CanDelete, CanExecute, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted) VALUES
(3, N'scheduler', NULL, NULL, CONVERT(bit, 'False'), CONVERT(bit, 'True'), CONVERT(bit, 'False'), CONVERT(bit, 'False'), CONVERT(bit, 'False'), CONVERT(DATETIME2, '2025-11-13 18:23:44.1900000', 121), NULL, N'System', NULL, CONVERT(bit, 'False'))

INSERT INTO [UserPermissions] (UserId, PermissionName, ResourceType, ResourceId, CanCreate, CanRead, CanUpdate, CanDelete, CanExecute, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted) VALUES
(3, N'schedules', NULL, NULL, CONVERT(bit, 'False'), CONVERT(bit, 'True'), CONVERT(bit, 'False'), CONVERT(bit, 'False'), CONVERT(bit, 'False'), CONVERT(DATETIME2, '2025-11-13 18:23:44.1900000', 121), NULL, N'System', NULL, CONVERT(bit, 'False'))

INSERT INTO [UserPermissions] (UserId, PermissionName, ResourceType, ResourceId, CanCreate, CanRead, CanUpdate, CanDelete, CanExecute, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted) VALUES
(3, N'jobs', NULL, NULL, CONVERT(bit, 'False'), CONVERT(bit, 'True'), CONVERT(bit, 'False'), CONVERT(bit, 'False'), CONVERT(bit, 'False'), CONVERT(DATETIME2, '2025-11-13 18:23:44.1900000', 121), NULL, N'System', NULL, CONVERT(bit, 'False'))

INSERT INTO [UserPermissions] (UserId, PermissionName, ResourceType, ResourceId, CanCreate, CanRead, CanUpdate, CanDelete, CanExecute, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted) VALUES
(4, N'scheduler', NULL, NULL, CONVERT(bit, 'False'), CONVERT(bit, 'True'), CONVERT(bit, 'False'), CONVERT(bit, 'False'), CONVERT(bit, 'False'), CONVERT(DATETIME2, '2025-11-13 18:23:44.2066667', 121), NULL, N'System', NULL, CONVERT(bit, 'False'))

INSERT INTO [UserPermissions] (UserId, PermissionName, ResourceType, ResourceId, CanCreate, CanRead, CanUpdate, CanDelete, CanExecute, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted) VALUES
(4, N'schedules', NULL, NULL, CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(bit, 'True'), CONVERT(DATETIME2, '2025-11-13 18:23:44.2066667', 121), NULL, N'System', NULL, CONVERT(bit, 'False'))

INSERT INTO [UserPermissions] (UserId, PermissionName, ResourceType, ResourceId, CanCreate, CanRead, CanUpdate, CanDelete, CanExecute, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsDeleted) VALUES
(4, N'jobs', NULL, NULL, CONVERT(bit, 'False'), CONVERT(bit, 'True'), CONVERT(bit, 'False'), CONVERT(bit, 'False'), CONVERT(bit, 'False'), CONVERT(DATETIME2, '2025-11-13 18:23:44.2066667', 121), NULL, N'System', NULL, CONVERT(bit, 'False'))


-- 3. Recreate the foreign key constraint
    ALTER TABLE [dbo].NotificationSettings WITH CHECK ADD CONSTRAINT [FK_NotificationSettings_Schedules_ScheduleId]
    FOREIGN KEY ([ScheduleId]) REFERENCES [dbo].[Schedules] ([Id]);
    GO

    ALTER TABLE [dbo].JobParameters WITH CHECK ADD CONSTRAINT [FK_JobParameters_Schedules_ScheduleId]
    FOREIGN KEY ([ScheduleId]) REFERENCES [dbo].[Schedules] ([Id]);
    GO

    ALTER TABLE [dbo].JobExecutions WITH CHECK ADD CONSTRAINT [FK_JobExecutions_Schedules_ScheduleId]
    FOREIGN KEY ([ScheduleId]) REFERENCES [dbo].[Schedules] ([Id]);
    GO

    ALTER TABLE [dbo].Schedules WITH CHECK ADD CONSTRAINT [FK_Schedules_Clients_ClientId] 
    FOREIGN KEY ([ClientID]) REFERENCES [dbo].[Clients] ([Id]);
    GO

    ALTER TABLE [dbo].ScheduleSyncSources WITH CHECK ADD CONSTRAINT [FK_ScheduleSyncSources_Clients_ClientId]
    FOREIGN KEY ([ClientID]) REFERENCES [dbo].[Clients] ([Id]);
    GO

    ALTER TABLE [dbo].UserPermissions WITH CHECK ADD CONSTRAINT FK_UserPermissions_Users_UserId
    FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);
    GO
    ALTER TABLE [dbo].PasswordHistories WITH CHECK ADD CONSTRAINT FK_PasswordHistories_Users_UserId
    FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]);
    GO

    ALTER TABLE [dbo].Users WITH CHECK ADD CONSTRAINT [FK_Users_Clients_ClientId]
    FOREIGN KEY ([ClientID]) REFERENCES [dbo].[Clients] ([Id]);
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

		ALTER TABLE USERS
		ADD MustChangePassword BIT DEFAULT(0) NOT NULL
		GO
		
		ALTER TABLE USERS
		ADD PasswordChangedAt DATETIME2
		GO

		-- Fix NULL values in Users table
		UPDATE Users 
		SET IsActive = 1 
		WHERE IsActive IS NULL;

		UPDATE Users 
		SET MustChangePassword = 0 
		WHERE MustChangePassword IS NULL;
