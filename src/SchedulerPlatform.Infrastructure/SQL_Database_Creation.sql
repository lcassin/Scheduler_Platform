IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [Clients] (
    [Id] int NOT NULL IDENTITY,
    [ClientName] nvarchar(200) NOT NULL,
    [ClientCode] nvarchar(50) NOT NULL,
    [IsActive] bit NOT NULL,
    [ContactEmail] nvarchar(255) NULL,
    [ContactPhone] nvarchar(50) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NOT NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_Clients] PRIMARY KEY ([Id])
);

CREATE TABLE [Schedules] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NOT NULL,
    [ClientId] int NOT NULL,
    [JobType] int NOT NULL,
    [Frequency] int NOT NULL,
    [CronExpression] nvarchar(100) NOT NULL,
    [NextRunTime] datetime2 NULL,
    [LastRunTime] datetime2 NULL,
    [IsEnabled] bit NOT NULL,
    [MaxRetries] int NOT NULL,
    [RetryDelayMinutes] int NOT NULL,
    [TimeZone] nvarchar(100) NULL,
    [JobConfiguration] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NOT NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_Schedules] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Schedules_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [Users] (
    [Id] int NOT NULL IDENTITY,
    [Username] nvarchar(100) NOT NULL,
    [Email] nvarchar(255) NOT NULL,
    [FirstName] nvarchar(100) NOT NULL,
    [LastName] nvarchar(100) NOT NULL,
    [ClientId] int NOT NULL,
    [IsActive] bit NOT NULL,
    [ExternalUserId] nvarchar(255) NULL,
    [ExternalIssuer] nvarchar(500) NULL,
    [PasswordHash] nvarchar(500) NULL,
    [IsSystemAdmin] bit NOT NULL DEFAULT 0,
    [LastLoginAt] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NOT NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Users_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [VendorCredentials] (
    [Id] int NOT NULL IDENTITY,
    [ClientId] int NOT NULL,
    [VendorName] nvarchar(200) NOT NULL,
    [VendorUrl] nvarchar(500) NOT NULL,
    [Username] nvarchar(200) NOT NULL,
    [EncryptedPassword] nvarchar(500) NOT NULL,
    [LastVerified] datetime2 NULL,
    [IsValid] bit NOT NULL,
    [AdditionalData] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NOT NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_VendorCredentials] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_VendorCredentials_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [JobExecutions] (
    [Id] int NOT NULL IDENTITY,
    [ScheduleId] int NOT NULL,
    [StartTime] datetime2 NOT NULL,
    [EndTime] datetime2 NULL,
    [Status] int NOT NULL,
    [Output] nvarchar(max) NULL,
    [ErrorMessage] nvarchar(max) NULL,
    [StackTrace] nvarchar(max) NULL,
    [RetryCount] int NOT NULL,
    [DurationSeconds] int NULL,
    [TriggeredBy] nvarchar(100) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NOT NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_JobExecutions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_JobExecutions_Schedules_ScheduleId] FOREIGN KEY ([ScheduleId]) REFERENCES [Schedules] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [JobParameters] (
    [Id] int NOT NULL IDENTITY,
    [ScheduleId] int NOT NULL,
    [ParameterName] nvarchar(100) NOT NULL,
    [ParameterType] nvarchar(50) NOT NULL,
    [ParameterValue] nvarchar(max) NULL,
    [SourceQuery] nvarchar(max) NULL,
    [SourceConnectionString] nvarchar(500) NULL,
    [IsDynamic] bit NOT NULL,
    [DisplayOrder] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NOT NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_JobParameters] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_JobParameters_Schedules_ScheduleId] FOREIGN KEY ([ScheduleId]) REFERENCES [Schedules] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [UserPermissions] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [PermissionName] nvarchar(100) NOT NULL,
    [ResourceType] nvarchar(50) NULL,
    [ResourceId] int NULL,
    [CanCreate] bit NOT NULL,
    [CanRead] bit NOT NULL,
    [CanUpdate] bit NOT NULL,
    [CanDelete] bit NOT NULL,
    [CanExecute] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NOT NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_UserPermissions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UserPermissions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE UNIQUE INDEX [IX_Clients_ClientCode] ON [Clients] ([ClientCode]);

CREATE INDEX [IX_JobExecutions_ScheduleId] ON [JobExecutions] ([ScheduleId]);

CREATE INDEX [IX_JobExecutions_StartTime] ON [JobExecutions] ([StartTime]);

CREATE INDEX [IX_JobExecutions_Status] ON [JobExecutions] ([Status]);

CREATE INDEX [IX_JobParameters_ScheduleId] ON [JobParameters] ([ScheduleId]);

CREATE INDEX [IX_Schedules_ClientId] ON [Schedules] ([ClientId]);

CREATE INDEX [IX_UserPermissions_UserId] ON [UserPermissions] ([UserId]);

CREATE INDEX [IX_Users_ClientId] ON [Users] ([ClientId]);

CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);

CREATE INDEX [IX_Users_ExternalIssuer_ExternalUserId] ON [Users] ([ExternalIssuer], [ExternalUserId]);

CREATE INDEX [IX_VendorCredentials_ClientId] ON [VendorCredentials] ([ClientId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251020133314_InitialCreate', N'9.0.10');

CREATE TABLE [AuditLogs] (
    [Id] int NOT NULL IDENTITY,
    [EventType] nvarchar(100) NOT NULL,
    [EntityType] nvarchar(100) NOT NULL,
    [EntityId] int NULL,
    [Action] nvarchar(50) NOT NULL,
    [OldValues] nvarchar(max) NULL,
    [NewValues] nvarchar(max) NULL,
    [UserName] nvarchar(200) NOT NULL,
    [ClientId] int NULL,
    [IpAddress] nvarchar(50) NULL,
    [UserAgent] nvarchar(500) NULL,
    [Timestamp] datetime2 NOT NULL,
    [AdditionalData] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NOT NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
);

CREATE TABLE [NotificationSettings] (
    [Id] int NOT NULL IDENTITY,
    [ScheduleId] int NOT NULL,
    [EnableSuccessNotifications] bit NOT NULL,
    [EnableFailureNotifications] bit NOT NULL,
    [SuccessEmailRecipients] nvarchar(1000) NULL,
    [FailureEmailRecipients] nvarchar(1000) NULL,
    [SuccessEmailSubject] nvarchar(500) NULL,
    [FailureEmailSubject] nvarchar(500) NULL,
    [IncludeExecutionDetails] bit NOT NULL,
    [IncludeOutput] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NOT NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_NotificationSettings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_NotificationSettings_Schedules_ScheduleId] FOREIGN KEY ([ScheduleId]) REFERENCES [Schedules] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_AuditLogs_EntityType_EntityId] ON [AuditLogs] ([EntityType], [EntityId]);

CREATE INDEX [IX_AuditLogs_Timestamp] ON [AuditLogs] ([Timestamp]);

CREATE UNIQUE INDEX [IX_NotificationSettings_ScheduleId] ON [NotificationSettings] ([ScheduleId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251020135737_AddAuditLogAndNotificationSettings', N'9.0.10');

DECLARE @var sysname;
SELECT @var = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[VendorCredentials]') AND [c].[name] = N'CreatedBy');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [VendorCredentials] DROP CONSTRAINT [' + @var + '];');
ALTER TABLE [VendorCredentials] ALTER COLUMN [CreatedBy] nvarchar(max) NULL;

DECLARE @var1 sysname;
SELECT @var1 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'CreatedBy');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT [' + @var1 + '];');
ALTER TABLE [Users] ALTER COLUMN [CreatedBy] nvarchar(max) NULL;

DECLARE @var2 sysname;
SELECT @var2 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserPermissions]') AND [c].[name] = N'CreatedBy');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [UserPermissions] DROP CONSTRAINT [' + @var2 + '];');
ALTER TABLE [UserPermissions] ALTER COLUMN [CreatedBy] nvarchar(max) NULL;

DECLARE @var3 sysname;
SELECT @var3 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Schedules]') AND [c].[name] = N'CreatedBy');
IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Schedules] DROP CONSTRAINT [' + @var3 + '];');
ALTER TABLE [Schedules] ALTER COLUMN [CreatedBy] nvarchar(max) NULL;

DECLARE @var4 sysname;
SELECT @var4 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[NotificationSettings]') AND [c].[name] = N'CreatedBy');
IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [NotificationSettings] DROP CONSTRAINT [' + @var4 + '];');
ALTER TABLE [NotificationSettings] ALTER COLUMN [CreatedBy] nvarchar(max) NULL;

DECLARE @var5 sysname;
SELECT @var5 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[JobParameters]') AND [c].[name] = N'CreatedBy');
IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [JobParameters] DROP CONSTRAINT [' + @var5 + '];');
ALTER TABLE [JobParameters] ALTER COLUMN [CreatedBy] nvarchar(max) NULL;

DECLARE @var6 sysname;
SELECT @var6 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[JobExecutions]') AND [c].[name] = N'CreatedBy');
IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [JobExecutions] DROP CONSTRAINT [' + @var6 + '];');
ALTER TABLE [JobExecutions] ALTER COLUMN [CreatedBy] nvarchar(max) NULL;

DECLARE @var7 sysname;
SELECT @var7 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Clients]') AND [c].[name] = N'CreatedBy');
IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [Clients] DROP CONSTRAINT [' + @var7 + '];');
ALTER TABLE [Clients] ALTER COLUMN [CreatedBy] nvarchar(max) NULL;

DECLARE @var8 sysname;
SELECT @var8 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AuditLogs]') AND [c].[name] = N'CreatedBy');
IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [AuditLogs] DROP CONSTRAINT [' + @var8 + '];');
ALTER TABLE [AuditLogs] ALTER COLUMN [CreatedBy] nvarchar(max) NULL;

CREATE TABLE [ScheduleSyncSources] (
    [SyncId] int NOT NULL IDENTITY,
    [AccountId] bigint NOT NULL,
    [AccountNumber] nvarchar(128) NOT NULL,
    [VendorId] bigint NOT NULL,
    [ClientId] bigint NOT NULL,
    [ScheduleFrequency] int NOT NULL,
    [LastInvoiceDate] datetime2 NOT NULL,
    [AccountName] nvarchar(64) NULL,
    [VendorName] nvarchar(64) NULL,
    [ClientName] nvarchar(64) NULL,
    [TandemAccountId] nvarchar(64) NULL,
    [LastSyncedAt] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_ScheduleSyncSources] PRIMARY KEY ([SyncId]),
    CONSTRAINT [FK_ScheduleSyncSources_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE NO ACTION
);

CREATE UNIQUE INDEX [IX_ScheduleSyncSources_AccountId] ON [ScheduleSyncSources] ([AccountId]);

CREATE INDEX [IX_ScheduleSyncSources_ClientId] ON [ScheduleSyncSources] ([ClientId]);

CREATE INDEX [IX_ScheduleSyncSources_VendorId] ON [ScheduleSyncSources] ([VendorId]);

CREATE INDEX [IX_ScheduleSyncSources_LastSyncedAt] ON [ScheduleSyncSources] ([LastSyncedAt]);

CREATE INDEX [IX_ScheduleSyncSources_ClientId_VendorId_AccountNumber] ON [ScheduleSyncSources] ([ClientId], [VendorId], [AccountNumber]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251024190624_AddScheduleSyncSourceTable', N'9.0.10');

ALTER TABLE [Schedules] ADD [TimeoutMinutes] int NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251027162823_AddTimeoutMinutesToSchedule', N'9.0.10');

ALTER TABLE [JobExecutions] ADD [CancelledBy] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251027195050_AddCancelledByToJobExecution', N'9.0.10');

ALTER TABLE [Users] ADD [ExternalIssuer] nvarchar(500) NULL;
ALTER TABLE [Users] ADD [PasswordHash] nvarchar(500) NULL;
ALTER TABLE [Users] ADD [IsSystemAdmin] bit NOT NULL DEFAULT 0;
ALTER TABLE [Users] ADD [LastLoginAt] datetime2 NULL;

CREATE INDEX [IX_Users_ExternalIssuer_ExternalUserId] ON [Users] ([ExternalIssuer], [ExternalUserId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251113180300_AddUserAuthenticationFields', N'10.0.0');

CREATE TABLE [PasswordHistories] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [PasswordHash] nvarchar(500) NOT NULL,
    [ChangedAt] datetime2 NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_PasswordHistories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PasswordHistories_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_PasswordHistories_UserId_ChangedAt] ON [PasswordHistories] ([UserId], [ChangedAt]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251113184700_AddPasswordHistory', N'10.0.0');

COMMIT;
GO


BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM [Clients] WHERE [ClientCode] = 'INTERNAL')
BEGIN
    INSERT INTO [Clients] ([ClientName], [ClientCode], [IsActive], [ContactEmail], [CreatedAt], [CreatedBy], [IsDeleted])
    VALUES (N'Internal', N'INTERNAL', 1, N'admin@cassinfo.com', GETUTCDATE(), N'System', 0);
END

DECLARE @ClientId INT = (SELECT [Id] FROM [Clients] WHERE [ClientCode] = 'INTERNAL');

IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Email] = 'superadmin@cassinfo.com')
BEGIN
    INSERT INTO [Users] ([Username], [Email], [FirstName], [LastName], [ClientId], [IsActive], [IsSystemAdmin], [CreatedAt], [CreatedBy], [IsDeleted])
    VALUES (N'superadmin', N'superadmin@cassinfo.com', N'Super', N'Admin', @ClientId, 1, 1, GETUTCDATE(), N'System', 0);
    
    DECLARE @SuperAdminId INT = SCOPE_IDENTITY();
    
    INSERT INTO [UserPermissions] ([UserId], [PermissionName], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedAt], [CreatedBy], [IsDeleted])
    VALUES 
        (@SuperAdminId, N'scheduler', 1, 1, 1, 1, 1, GETUTCDATE(), N'System', 0),
        (@SuperAdminId, N'schedules', 1, 1, 1, 1, 1, GETUTCDATE(), N'System', 0),
        (@SuperAdminId, N'jobs', 1, 1, 1, 1, 1, GETUTCDATE(), N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Email] = 'admin@cassinfo.com')
BEGIN
    INSERT INTO [Users] ([Username], [Email], [FirstName], [LastName], [ClientId], [IsActive], [IsSystemAdmin], [CreatedAt], [CreatedBy], [IsDeleted])
    VALUES (N'admin', N'admin@cassinfo.com', N'Default', N'Admin', @ClientId, 1, 0, GETUTCDATE(), N'System', 0);
    
    DECLARE @AdminId INT = SCOPE_IDENTITY();
    
    INSERT INTO [UserPermissions] ([UserId], [PermissionName], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedAt], [CreatedBy], [IsDeleted])
    VALUES 
        (@AdminId, N'scheduler', 1, 1, 1, 1, 1, GETUTCDATE(), N'System', 0),
        (@AdminId, N'schedules', 1, 1, 1, 1, 1, GETUTCDATE(), N'System', 0),
        (@AdminId, N'jobs', 1, 1, 1, 1, 1, GETUTCDATE(), N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Email] = 'viewer@cassinfo.com')
BEGIN
    INSERT INTO [Users] ([Username], [Email], [FirstName], [LastName], [ClientId], [IsActive], [IsSystemAdmin], [CreatedAt], [CreatedBy], [IsDeleted])
    VALUES (N'viewer', N'viewer@cassinfo.com', N'View', N'Only', @ClientId, 1, 0, GETUTCDATE(), N'System', 0);
    
    DECLARE @ViewerId INT = SCOPE_IDENTITY();
    
    INSERT INTO [UserPermissions] ([UserId], [PermissionName], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedAt], [CreatedBy], [IsDeleted])
    VALUES 
        (@ViewerId, N'scheduler', 0, 1, 0, 0, 0, GETUTCDATE(), N'System', 0),
        (@ViewerId, N'schedules', 0, 1, 0, 0, 0, GETUTCDATE(), N'System', 0),
        (@ViewerId, N'jobs', 0, 1, 0, 0, 0, GETUTCDATE(), N'System', 0);
END

IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Email] = 'editor@cassinfo.com')
BEGIN
    INSERT INTO [Users] ([Username], [Email], [FirstName], [LastName], [ClientId], [IsActive], [IsSystemAdmin], [CreatedAt], [CreatedBy], [IsDeleted])
    VALUES (N'editor', N'editor@cassinfo.com', N'Schedule', N'Editor', @ClientId, 1, 0, GETUTCDATE(), N'System', 0);
    
    DECLARE @EditorId INT = SCOPE_IDENTITY();
    
    INSERT INTO [UserPermissions] ([UserId], [PermissionName], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedAt], [CreatedBy], [IsDeleted])
    VALUES 
        (@EditorId, N'scheduler', 0, 1, 0, 0, 0, GETUTCDATE(), N'System', 0),
        (@EditorId, N'schedules', 1, 1, 1, 1, 1, GETUTCDATE(), N'System', 0),
        (@EditorId, N'jobs', 0, 1, 0, 0, 0, GETUTCDATE(), N'System', 0);
END

COMMIT;
GO

PRINT 'Database creation and seeding completed successfully!';
PRINT '';
PRINT 'Created users:';
PRINT '  - superadmin@cassinfo.com (Super Admin - cannot be modified via UI)';
PRINT '  - admin@cassinfo.com (Default Admin)';
PRINT '  - viewer@cassinfo.com (Read-only access)';
PRINT '  - editor@cassinfo.com (Can create/edit/delete schedules)';
PRINT '';
PRINT 'Permission Templates:';
PRINT '  - Viewer: scheduler:read, schedules:read, jobs:read';
PRINT '  - Editor: scheduler:read, schedules:*, jobs:read';
PRINT '  - Admin: All permissions';
PRINT '  - Super Admin: All permissions + users:manage (IsSystemAdmin=true)';
PRINT '';
PRINT 'Service Account Configuration:';
PRINT '  - Client ID: svc-adrscheduler';
PRINT '  - Configured in Duende IdentityServer Config.cs';
PRINT '  - Permissions: Editor template (schedules:*)';
PRINT '';
PRINT 'Azure AD Configuration Required:';
PRINT '  - Tenant ID: 08717c9a-7042-4ddf-b86a-e0a500d32cde';
PRINT '  - Update appsettings.json with ClientId and ClientSecret';
PRINT '  - See AZURE_AD_SETUP.md for complete setup instructions';
VALUES (1, 1, N'test_credential', GETDATE(), GETDATE(), N'System', N'System', 0);

/** Optional - Create Test Job Execution **/

INSERT INTO [JobExecutions] ([JobId], [Status], [StartedAt], [EndedAt], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy], [IsDeleted])
VALUES (1, N'Pending', GETDATE(), NULL, GETDATE(), GETDATE(), N'System', N'System', 0);
GO

/* End of SQL Database Creation Script */
