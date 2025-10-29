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
    [Id] int NOT NULL IDENTITY,
    [ClientId] int NOT NULL,
    [Vendor] nvarchar(200) NOT NULL,
    [AccountNumber] nvarchar(100) NOT NULL,
    [ScheduleFrequency] int NOT NULL,
    [ScheduleDate] datetime2 NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [UpdatedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_ScheduleSyncSources] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ScheduleSyncSources_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_ScheduleSyncSources_ClientId] ON [ScheduleSyncSources] ([ClientId]);

CREATE INDEX [IX_ScheduleSyncSources_ClientId_Vendor_AccountNumber] ON [ScheduleSyncSources] ([ClientId], [Vendor], [AccountNumber]);

CREATE INDEX [IX_ScheduleSyncSources_ScheduleDate] ON [ScheduleSyncSources] ([ScheduleDate]);

CREATE INDEX [IX_ScheduleSyncSources_ScheduleFrequency] ON [ScheduleSyncSources] ([ScheduleFrequency]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251024190624_AddScheduleSyncSourceTable', N'9.0.10');

ALTER TABLE [Schedules] ADD [TimeoutMinutes] int NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251027162823_AddTimeoutMinutesToSchedule', N'9.0.10');

COMMIT;
GO

/** Optional - Create Test Client **/

INSERT INTO [Clients] ([Name], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy], [IsDeleted])
VALUES (N'Test Client', GETDATE(), GETDATE(), N'System', N'System', 0);

/** Optional - Create Test User **/

INSERT INTO [Users] ([Username], [Email], [PasswordHash], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy], [IsDeleted])
VALUES (N'TestUser', N'testuser@example.com', N'hashed_password', GETDATE(), GETDATE(), N'System', N'System', 0);

/** Optional - Create Test Schedule **/

INSERT INTO [Schedules] ([Name], [Description], [CronExpression], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy], [IsDeleted])
VALUES (N'Test Schedule', N'This is a test schedule.', N'0 0 * * *', GETDATE(), GETDATE(), N'System', N'System', 0);

/** Optional - Create Test Vendor Credential **/

INSERT INTO [VendorCredentials] ([VendorId], [ClientId], [Credential], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy], [IsDeleted])
VALUES (1, 1, N'test_credential', GETDATE(), GETDATE(), N'System', N'System', 0);

/** Optional - Create Test Job Execution **/

INSERT INTO [JobExecutions] ([JobId], [Status], [StartedAt], [EndedAt], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy], [IsDeleted])
VALUES (1, N'Pending', GETDATE(), NULL, GETDATE(), GETDATE(), N'System', N'System', 0);
GO

/* End of SQL Database Creation Script */
