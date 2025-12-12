-- Seed Data Script for Scheduler Platform
-- Generated from existing database records
-- Run this after applying the InitialCreate migration to a fresh database

-- =============================================
-- 1. Client Table (1 record)
-- =============================================
SET IDENTITY_INSERT [Client] ON;

INSERT INTO [Client] (ClientId, ClientName, ClientCode, IsActive, ContactEmail, ContactPhone, CreatedDateTime, ModifiedDateTime, CreatedBy, ModifiedBy, IsDeleted, LastSyncedDateTime, ExternalClientId)
VALUES (1, 'Cass Information Systems', 'INTERNAL', 1, 'lcassin@cassinfo.com', '864-201-2796', '2025-10-22 11:41:16.2500000', '2025-11-18 18:45:18.6169732', 'Lee Cassin', 'ApiSync', 0, NULL, 0);

SET IDENTITY_INSERT [Client] OFF;

-- =============================================
-- 2. User Table (3 records - UserId 2, 3, 4)
-- =============================================
SET IDENTITY_INSERT [User] ON;

-- Admin user
INSERT INTO [User] (UserId, Username, Email, FirstName, LastName, ClientId, IsActive, ExternalUserId, CreatedDateTime, PasswordChangedDateTime, CreatedBy, ModifiedBy, IsDeleted, ExternalIssuer, PasswordHash, IsSystemAdmin, ModifiedDateTime, MustChangePassword, LastLoginDateTime)
VALUES (2, 'admin', 'lcassin@cassinfo.com', 'Default', 'Admin', 1, 1, 'entra|32a589c1-6ddf-47fe-bf57-2318dbee22a0', '2025-11-13 18:23:44.1800000', '2025-12-03 18:35:03.0579279', 'System', 'Manual', 0, 'entra', 'AQAAAAIAAYagAAAAEMayBtmV+pP33QRQwjHQHlkv0oLJpFxaA2VuMF0rL7XxngJdAYAUTevADdjlDUjrQw==', 1, '2025-12-08 22:10:43.7180563', 0, '2025-12-08 22:10:43.7172079');

-- Viewer user
INSERT INTO [User] (UserId, Username, Email, FirstName, LastName, ClientId, IsActive, ExternalUserId, CreatedDateTime, PasswordChangedDateTime, CreatedBy, ModifiedBy, IsDeleted, ExternalIssuer, PasswordHash, IsSystemAdmin, ModifiedDateTime, MustChangePassword, LastLoginDateTime)
VALUES (3, 'viewer', 'leecassin@icloud.com', 'View', 'Only', 1, 1, NULL, '2025-11-13 18:23:44.1900000', '2025-11-24 23:05:34.1008413', 'System', 'Default Admin', 0, NULL, 'AQAAAAEAACcQAAAAEMJ24UU6gnEXVgabJEJuJdqqWFhhlVn3thwWSga2ugpNzsALvI8oKw+8YLzelgfqTA==', 0, '2025-11-14 17:18:09.3205089', 0, NULL);

-- Editor user
INSERT INTO [User] (UserId, Username, Email, FirstName, LastName, ClientId, IsActive, ExternalUserId, CreatedDateTime, PasswordChangedDateTime, CreatedBy, ModifiedBy, IsDeleted, ExternalIssuer, PasswordHash, IsSystemAdmin, ModifiedDateTime, MustChangePassword, LastLoginDateTime)
VALUES (4, 'editor', 'lcassin@charter.net', 'Schedule', 'Editor', 1, 1, NULL, '2025-11-13 18:23:44.2033333', '2025-11-24 19:30:27.1196487', 'System', 'Default Admin', 0, NULL, 'AQAAAAEAACcQAAAAEKluqq0CX8KwQmTEFtggSJWW9TXUHHg4q88ROzqeXivcBStj5DcvXZrpizNLxf00tA==', 0, '2025-11-24 17:24:27.4821184', 0, NULL);

SET IDENTITY_INSERT [User] OFF;

-- =============================================
-- 3. UserPermission Table (permissions for users 2, 3, 4)
-- =============================================
SET IDENTITY_INSERT [UserPermission] ON;

-- User 2 (admin) permissions
INSERT INTO [UserPermission] (UserPermissionId, UserId, PermissionName, ResourceType, ResourceId, CanCreate, CanRead, CanUpdate, CanDelete, CanExecute, CreatedDateTime, ModifiedDateTime, CreatedBy, ModifiedBy, IsDeleted)
VALUES (4, 2, 'scheduler', NULL, NULL, 1, 1, 1, 1, 1, '2025-11-13 18:23:44.1800000', '2025-11-13 18:23:44.1800000', 'System', 'System', 0);

INSERT INTO [UserPermission] (UserPermissionId, UserId, PermissionName, ResourceType, ResourceId, CanCreate, CanRead, CanUpdate, CanDelete, CanExecute, CreatedDateTime, ModifiedDateTime, CreatedBy, ModifiedBy, IsDeleted)
VALUES (5, 2, 'schedules', NULL, NULL, 1, 1, 1, 1, 1, '2025-11-13 18:23:44.1800000', '2025-11-13 18:23:44.1800000', 'System', 'System', 0);

INSERT INTO [UserPermission] (UserPermissionId, UserId, PermissionName, ResourceType, ResourceId, CanCreate, CanRead, CanUpdate, CanDelete, CanExecute, CreatedDateTime, ModifiedDateTime, CreatedBy, ModifiedBy, IsDeleted)
VALUES (6, 2, 'jobs', NULL, NULL, 1, 1, 1, 1, 1, '2025-11-13 18:23:44.1800000', '2025-11-13 18:23:44.1800000', 'System', 'System', 0);

INSERT INTO [UserPermission] (UserPermissionId, UserId, PermissionName, ResourceType, ResourceId, CanCreate, CanRead, CanUpdate, CanDelete, CanExecute, CreatedDateTime, ModifiedDateTime, CreatedBy, ModifiedBy, IsDeleted)
VALUES (143, 2, 'users:manage', NULL, NULL, 1, 1, 1, 1, 0, '2025-11-24 18:54:12.2733333', '2025-11-24 18:54:12.2733333', 'System', 'System', 0);

-- User 3 (viewer) permissions
INSERT INTO [UserPermission] (UserPermissionId, UserId, PermissionName, ResourceType, ResourceId, CanCreate, CanRead, CanUpdate, CanDelete, CanExecute, CreatedDateTime, ModifiedDateTime, CreatedBy, ModifiedBy, IsDeleted)
VALUES (123, 3, 'scheduler', NULL, NULL, 0, 1, 0, 0, 0, '2025-11-24 17:06:04.6157306', '2025-11-24 17:06:04.6157306', 'Default Admin', 'Default Admin', 0);

INSERT INTO [UserPermission] (UserPermissionId, UserId, PermissionName, ResourceType, ResourceId, CanCreate, CanRead, CanUpdate, CanDelete, CanExecute, CreatedDateTime, ModifiedDateTime, CreatedBy, ModifiedBy, IsDeleted)
VALUES (124, 3, 'schedules', NULL, NULL, 0, 1, 0, 0, 0, '2025-11-24 17:06:04.6160085', '2025-11-24 17:06:04.6160085', 'Default Admin', 'Default Admin', 0);

INSERT INTO [UserPermission] (UserPermissionId, UserId, PermissionName, ResourceType, ResourceId, CanCreate, CanRead, CanUpdate, CanDelete, CanExecute, CreatedDateTime, ModifiedDateTime, CreatedBy, ModifiedBy, IsDeleted)
VALUES (125, 3, 'jobs', NULL, NULL, 0, 1, 0, 0, 0, '2025-11-24 17:06:04.6160605', '2025-11-24 17:06:04.6160605', 'Default Admin', 'Default Admin', 0);

-- User 4 (editor) permissions
INSERT INTO [UserPermission] (UserPermissionId, UserId, PermissionName, ResourceType, ResourceId, CanCreate, CanRead, CanUpdate, CanDelete, CanExecute, CreatedDateTime, ModifiedDateTime, CreatedBy, ModifiedBy, IsDeleted)
VALUES (156, 4, 'scheduler', NULL, NULL, 1, 1, 1, 1, 0, '2025-11-26 03:16:06.0679749', '2025-11-26 03:16:06.0679749', 'Default Admin', 'Default Admin', 0);

INSERT INTO [UserPermission] (UserPermissionId, UserId, PermissionName, ResourceType, ResourceId, CanCreate, CanRead, CanUpdate, CanDelete, CanExecute, CreatedDateTime, ModifiedDateTime, CreatedBy, ModifiedBy, IsDeleted)
VALUES (157, 4, 'schedules', NULL, NULL, 1, 1, 1, 1, 1, '2025-11-26 03:16:06.0685393', '2025-11-26 03:16:06.0685393', 'Default Admin', 'Default Admin', 0);

INSERT INTO [UserPermission] (UserPermissionId, UserId, PermissionName, ResourceType, ResourceId, CanCreate, CanRead, CanUpdate, CanDelete, CanExecute, CreatedDateTime, ModifiedDateTime, CreatedBy, ModifiedBy, IsDeleted)
VALUES (158, 4, 'jobs', NULL, NULL, 1, 1, 1, 1, 1, '2025-11-26 03:16:06.0685865', '2025-11-26 03:16:06.0685865', 'Default Admin', 'Default Admin', 0);

INSERT INTO [UserPermission] (UserPermissionId, UserId, PermissionName, ResourceType, ResourceId, CanCreate, CanRead, CanUpdate, CanDelete, CanExecute, CreatedDateTime, ModifiedDateTime, CreatedBy, ModifiedBy, IsDeleted)
VALUES (159, 4, 'users:manage', NULL, NULL, 0, 1, 1, 0, 0, '2025-11-26 03:16:06.0686020', '2025-11-26 03:16:06.0686020', 'Default Admin', 'Default Admin', 0);

SET IDENTITY_INSERT [UserPermission] OFF;

-- =============================================
-- 4. Schedule Table (3 records)
-- =============================================
SET IDENTITY_INSERT [Schedule] ON;

-- Daily Log Cleanup (Process job - JobType 1) - IsSystemSchedule = 1 (protected)
INSERT INTO [Schedule] (ScheduleId, Name, Description, ClientId, JobType, Frequency, CronExpression, ModifiedDateTime, LastRunDateTime, IsEnabled, IsSystemSchedule, MaxRetries, RetryDelayMinutes, TimeZone, JobConfiguration, CreatedDateTime, NextRunDateTime, CreatedBy, ModifiedBy, IsDeleted, TimeoutMinutes)
VALUES (1, 'Daily Log Cleanup', 'Automatically deletes log files older than 7 days from API and IdentityServer directories', 1, 1, 1, '0 0 2 * * ?', '2025-12-08 14:45:18.0915794', NULL, 1, 1, 3, 5, 'Central Standard Time', '{"ExecutablePath":"C:\\Users\\LCassin\\source\\repos\\Scheduler_Platform\\src\\SchedulerPlatform.LogCleanup\\bin\\Release\\net10.0\\SchedulerPlatform.LogCleanup.exe","Arguments":"1","WorkingDirectory":"C:\\Users\\LCassin\\source\\repos\\Scheduler_Platform"}', '2025-10-24 23:03:13.4966667', '2025-12-09 08:00:00.0000000', 'System', 'Default Admin', 0, NULL);

-- ADR Account Sync - runs daily at 1:00 AM CT (API Call job - JobType 3) - IsSystemSchedule = 1 (protected)
INSERT INTO [Schedule] (ScheduleId, Name, Description, ClientId, JobType, Frequency, CronExpression, ModifiedDateTime, LastRunDateTime, IsEnabled, IsSystemSchedule, MaxRetries, RetryDelayMinutes, TimeZone, JobConfiguration, CreatedDateTime, NextRunDateTime, CreatedBy, ModifiedBy, IsDeleted, TimeoutMinutes)
VALUES (2, 'ADR Account Sync', 'Syncs ADR accounts from VendorCredNewUAT database daily', 1, 3, 1, '0 0 1 * * ?', GETUTCDATE(), NULL, 1, 1, 3, 5, 'Central Standard Time', '{"Url":"https://localhost:7008/api/adr/sync/accounts","Method":"POST","TimeoutSeconds":600,"AuthorizationType":"ApiKey","AuthorizationValue":"{{Scheduler:InternalApiKey}}"}', GETUTCDATE(), NULL, 'System Created', 'System Created', 0, 10);

-- ADR Full Cycle - runs daily at 2:00 AM CT after sync completes (API Call job - JobType 3) - IsSystemSchedule = 1 (protected)
INSERT INTO [Schedule] (ScheduleId, Name, Description, ClientId, JobType, Frequency, CronExpression, ModifiedDateTime, LastRunDateTime, IsEnabled, IsSystemSchedule, MaxRetries, RetryDelayMinutes, TimeZone, JobConfiguration, CreatedDateTime, NextRunDateTime, CreatedBy, ModifiedBy, IsDeleted, TimeoutMinutes)
VALUES (3, 'ADR Full Cycle', 'Runs full ADR orchestration cycle: create jobs, verify credentials, process scraping, check statuses', 1, 3, 1, '0 0 2 * * ?', GETUTCDATE(), NULL, 1, 1, 3, 5, 'Central Standard Time', '{"Url":"https://localhost:7008/api/adr/orchestrate/run-full-cycle","Method":"POST","TimeoutSeconds":1800,"AuthorizationType":"ApiKey","AuthorizationValue":"{{Scheduler:InternalApiKey}}"}', GETUTCDATE(), NULL, 'System Created', 'System Created', 0, 30);

SET IDENTITY_INSERT [Schedule] OFF;

-- =============================================
-- 5. NotificationSetting Table (3 records - one per system schedule)
-- Sends failure notifications to lcassin@cassinfo.com
-- =============================================
SET IDENTITY_INSERT [NotificationSetting] ON;

-- Notification for Daily Log Cleanup (ScheduleId 1)
INSERT INTO [NotificationSetting] (NotificationSettingId, ScheduleId, EnableSuccessNotifications, EnableFailureNotifications, SuccessEmailRecipients, FailureEmailRecipients, SuccessEmailSubject, FailureEmailSubject, IncludeExecutionDetails, IncludeOutput, CreatedDateTime, ModifiedDateTime, CreatedBy, ModifiedBy, IsDeleted)
VALUES (1, 1, 0, 1, NULL, 'lcassin@cassinfo.com', NULL, 'FAILED: Daily Log Cleanup', 1, 1, GETUTCDATE(), GETUTCDATE(), 'System Seed', 'System Seed', 0);

-- Notification for ADR Account Sync (ScheduleId 2)
INSERT INTO [NotificationSetting] (NotificationSettingId, ScheduleId, EnableSuccessNotifications, EnableFailureNotifications, SuccessEmailRecipients, FailureEmailRecipients, SuccessEmailSubject, FailureEmailSubject, IncludeExecutionDetails, IncludeOutput, CreatedDateTime, ModifiedDateTime, CreatedBy, ModifiedBy, IsDeleted)
VALUES (2, 2, 0, 1, NULL, 'lcassin@cassinfo.com', NULL, 'FAILED: ADR Account Sync', 1, 1, GETUTCDATE(), GETUTCDATE(), 'System Seed', 'System Seed', 0);

-- Notification for ADR Full Cycle (ScheduleId 3)
INSERT INTO [NotificationSetting] (NotificationSettingId, ScheduleId, EnableSuccessNotifications, EnableFailureNotifications, SuccessEmailRecipients, FailureEmailRecipients, SuccessEmailSubject, FailureEmailSubject, IncludeExecutionDetails, IncludeOutput, CreatedDateTime, ModifiedDateTime, CreatedBy, ModifiedBy, IsDeleted)
VALUES (3, 3, 0, 1, NULL, 'lcassin@cassinfo.com', NULL, 'FAILED: ADR Full Cycle', 1, 1, GETUTCDATE(), GETUTCDATE(), 'System Seed', 'System Seed', 0);

SET IDENTITY_INSERT [NotificationSetting] OFF;

-- =============================================
-- End of Seed Data Script
-- =============================================
PRINT 'Seed data inserted successfully!';
