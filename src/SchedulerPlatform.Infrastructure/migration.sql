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
VALUES (N'20251020133314_InitialCreate', N'10.0.0');

COMMIT;
GO

BEGIN TRANSACTION;
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
VALUES (N'20251020135737_AddAuditLogAndNotificationSettings', N'10.0.0');

COMMIT;
GO

BEGIN TRANSACTION;
DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Users]') AND [c].[name] = N'CreatedBy');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [Users] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [Users] ALTER COLUMN [CreatedBy] nvarchar(max) NULL;

DECLARE @var1 nvarchar(max);
SELECT @var1 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[UserPermissions]') AND [c].[name] = N'CreatedBy');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [UserPermissions] DROP CONSTRAINT ' + @var1 + ';');
ALTER TABLE [UserPermissions] ALTER COLUMN [CreatedBy] nvarchar(max) NULL;

DECLARE @var2 nvarchar(max);
SELECT @var2 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Schedules]') AND [c].[name] = N'CreatedBy');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Schedules] DROP CONSTRAINT ' + @var2 + ';');
ALTER TABLE [Schedules] ALTER COLUMN [CreatedBy] nvarchar(max) NULL;

DECLARE @var3 nvarchar(max);
SELECT @var3 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[NotificationSettings]') AND [c].[name] = N'CreatedBy');
IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [NotificationSettings] DROP CONSTRAINT ' + @var3 + ';');
ALTER TABLE [NotificationSettings] ALTER COLUMN [CreatedBy] nvarchar(max) NULL;

DECLARE @var4 nvarchar(max);
SELECT @var4 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[JobParameters]') AND [c].[name] = N'CreatedBy');
IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [JobParameters] DROP CONSTRAINT ' + @var4 + ';');
ALTER TABLE [JobParameters] ALTER COLUMN [CreatedBy] nvarchar(max) NULL;

DECLARE @var5 nvarchar(max);
SELECT @var5 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[JobExecutions]') AND [c].[name] = N'CreatedBy');
IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [JobExecutions] DROP CONSTRAINT ' + @var5 + ';');
ALTER TABLE [JobExecutions] ALTER COLUMN [CreatedBy] nvarchar(max) NULL;

DECLARE @var6 nvarchar(max);
SELECT @var6 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Clients]') AND [c].[name] = N'CreatedBy');
IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [Clients] DROP CONSTRAINT ' + @var6 + ';');
ALTER TABLE [Clients] ALTER COLUMN [CreatedBy] nvarchar(max) NULL;

DECLARE @var7 nvarchar(max);
SELECT @var7 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AuditLogs]') AND [c].[name] = N'CreatedBy');
IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [AuditLogs] DROP CONSTRAINT ' + @var7 + ';');
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
VALUES (N'20251024190624_AddScheduleSyncSourceTable', N'10.0.0');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [Schedules] ADD [TimeoutMinutes] int NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251027162823_AddTimeoutMinutesToSchedule', N'10.0.0');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [Users] ADD [ExternalIssuer] nvarchar(500) NULL;

ALTER TABLE [Users] ADD [PasswordHash] nvarchar(500) NULL;

ALTER TABLE [Users] ADD [IsSystemAdmin] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Users] ADD [LastLoginAt] datetime2 NULL;

CREATE INDEX [IX_Users_ExternalIssuer_ExternalUserId] ON [Users] ([ExternalIssuer], [ExternalUserId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251113180300_AddUserAuthenticationFields', N'10.0.0');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [PasswordHistories] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [PasswordHash] nvarchar(500) NOT NULL,
    [ChangedAt] datetime2 NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    [CreatedBy] nvarchar(max) NOT NULL,
    [UpdatedBy] nvarchar(max) NOT NULL,
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
ALTER TABLE [Users] ADD [MustChangePassword] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Users] ADD [PasswordChangedAt] datetime2 NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251124195400_AddPasswordManagementFields', N'10.0.0');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [JobExecutions] DROP CONSTRAINT [FK_JobExecutions_Schedules_ScheduleId];

ALTER TABLE [JobParameters] DROP CONSTRAINT [FK_JobParameters_Schedules_ScheduleId];

ALTER TABLE [NotificationSettings] DROP CONSTRAINT [FK_NotificationSettings_Schedules_ScheduleId];

ALTER TABLE [PasswordHistories] DROP CONSTRAINT [FK_PasswordHistories_Users_UserId];

ALTER TABLE [Schedules] DROP CONSTRAINT [FK_Schedules_Clients_ClientId];

ALTER TABLE [ScheduleSyncSources] DROP CONSTRAINT [FK_ScheduleSyncSources_Clients_ClientId];

ALTER TABLE [UserPermissions] DROP CONSTRAINT [FK_UserPermissions_Users_UserId];

ALTER TABLE [Users] DROP CONSTRAINT [FK_Users_Clients_ClientId];

ALTER TABLE [Users] DROP CONSTRAINT [PK_Users];

ALTER TABLE [UserPermissions] DROP CONSTRAINT [PK_UserPermissions];

ALTER TABLE [ScheduleSyncSources] DROP CONSTRAINT [PK_ScheduleSyncSources];

ALTER TABLE [Schedules] DROP CONSTRAINT [PK_Schedules];

ALTER TABLE [PasswordHistories] DROP CONSTRAINT [PK_PasswordHistories];

ALTER TABLE [NotificationSettings] DROP CONSTRAINT [PK_NotificationSettings];

ALTER TABLE [JobParameters] DROP CONSTRAINT [PK_JobParameters];

ALTER TABLE [JobExecutions] DROP CONSTRAINT [PK_JobExecutions];

ALTER TABLE [Clients] DROP CONSTRAINT [PK_Clients];

ALTER TABLE [AuditLogs] DROP CONSTRAINT [PK_AuditLogs];

EXEC sp_rename N'[Users]', N'User', 'OBJECT';

EXEC sp_rename N'[UserPermissions]', N'UserPermission', 'OBJECT';

EXEC sp_rename N'[ScheduleSyncSources]', N'ScheduleSyncSource', 'OBJECT';

EXEC sp_rename N'[Schedules]', N'Schedule', 'OBJECT';

EXEC sp_rename N'[PasswordHistories]', N'PasswordHistory', 'OBJECT';

EXEC sp_rename N'[NotificationSettings]', N'NotificationSetting', 'OBJECT';

EXEC sp_rename N'[JobParameters]', N'JobParameter', 'OBJECT';

EXEC sp_rename N'[JobExecutions]', N'JobExecution', 'OBJECT';

EXEC sp_rename N'[Clients]', N'Client', 'OBJECT';

EXEC sp_rename N'[AuditLogs]', N'AuditLog', 'OBJECT';

EXEC sp_rename N'[User].[Id]', N'UserId', 'COLUMN';

EXEC sp_rename N'[User].[UpdatedBy]', N'ModifiedBy', 'COLUMN';

EXEC sp_rename N'[User].[UpdatedAt]', N'PasswordChangedDateTime', 'COLUMN';

EXEC sp_rename N'[User].[LastLoginAt]', N'ModifiedDateTime', 'COLUMN';

EXEC sp_rename N'[User].[CreatedAt]', N'CreatedDateTime', 'COLUMN';

EXEC sp_rename N'[User].[IX_Users_ExternalIssuer_ExternalUserId]', N'IX_User_ExternalIssuer_ExternalUserId', 'INDEX';

EXEC sp_rename N'[User].[IX_Users_Email]', N'IX_User_Email', 'INDEX';

EXEC sp_rename N'[User].[IX_Users_ClientId]', N'IX_User_ClientId', 'INDEX';

EXEC sp_rename N'[UserPermission].[Id]', N'UserPermissionId', 'COLUMN';

EXEC sp_rename N'[UserPermission].[UpdatedBy]', N'ModifiedBy', 'COLUMN';

EXEC sp_rename N'[UserPermission].[UpdatedAt]', N'ModifiedDateTime', 'COLUMN';

EXEC sp_rename N'[UserPermission].[CreatedAt]', N'CreatedDateTime', 'COLUMN';

EXEC sp_rename N'[UserPermission].[IX_UserPermissions_UserId]', N'IX_UserPermission_UserId', 'INDEX';

EXEC sp_rename N'[ScheduleSyncSource].[SyncId]', N'ScheduleSyncSourceId', 'COLUMN';

EXEC sp_rename N'[ScheduleSyncSource].[UpdatedBy]', N'ModifiedBy', 'COLUMN';

EXEC sp_rename N'[ScheduleSyncSource].[UpdatedAt]', N'ModifiedDateTime', 'COLUMN';

EXEC sp_rename N'[ScheduleSyncSource].[LastSyncedAt]', N'LastSyncedDateTime', 'COLUMN';

EXEC sp_rename N'[ScheduleSyncSource].[LastInvoiceDate]', N'LastInvoiceDateTime', 'COLUMN';

EXEC sp_rename N'[ScheduleSyncSource].[CreatedAt]', N'CreatedDateTime', 'COLUMN';

EXEC sp_rename N'[ScheduleSyncSource].[IX_ScheduleSyncSources_LastSyncedAt]', N'IX_ScheduleSyncSource_LastSyncedDateTime', 'INDEX';

EXEC sp_rename N'[ScheduleSyncSource].[IX_ScheduleSyncSources_ExternalVendorId]', N'IX_ScheduleSyncSource_ExternalVendorId', 'INDEX';

EXEC sp_rename N'[ScheduleSyncSource].[IX_ScheduleSyncSources_ExternalClientId_ExternalVendorId_AccountNumber]', N'IX_ScheduleSyncSource_ExternalClientId_ExternalVendorId_AccountNumber', 'INDEX';

EXEC sp_rename N'[ScheduleSyncSource].[IX_ScheduleSyncSources_ExternalClientId]', N'IX_ScheduleSyncSource_ExternalClientId', 'INDEX';

EXEC sp_rename N'[ScheduleSyncSource].[IX_ScheduleSyncSources_ExternalAccountId]', N'IX_ScheduleSyncSource_ExternalAccountId', 'INDEX';

EXEC sp_rename N'[ScheduleSyncSource].[IX_ScheduleSyncSources_ClientId]', N'IX_ScheduleSyncSource_ClientId', 'INDEX';

EXEC sp_rename N'[Schedule].[Id]', N'ScheduleId', 'COLUMN';

EXEC sp_rename N'[Schedule].[UpdatedBy]', N'ModifiedBy', 'COLUMN';

EXEC sp_rename N'[Schedule].[UpdatedAt]', N'NextRunDateTime', 'COLUMN';

EXEC sp_rename N'[Schedule].[NextRunTime]', N'ModifiedDateTime', 'COLUMN';

EXEC sp_rename N'[Schedule].[LastRunTime]', N'LastRunDateTime', 'COLUMN';

EXEC sp_rename N'[Schedule].[CreatedAt]', N'CreatedDateTime', 'COLUMN';

EXEC sp_rename N'[Schedule].[IX_Schedules_ClientId]', N'IX_Schedule_ClientId', 'INDEX';

EXEC sp_rename N'[PasswordHistory].[Id]', N'PasswordHistoryId', 'COLUMN';

EXEC sp_rename N'[PasswordHistory].[UpdatedBy]', N'ModifiedBy', 'COLUMN';

EXEC sp_rename N'[PasswordHistory].[UpdatedAt]', N'ModifiedDateTime', 'COLUMN';

EXEC sp_rename N'[PasswordHistory].[CreatedAt]', N'CreatedDateTime', 'COLUMN';

EXEC sp_rename N'[PasswordHistory].[ChangedAt]', N'ChangedDateTime', 'COLUMN';

EXEC sp_rename N'[PasswordHistory].[IX_PasswordHistories_UserId_ChangedAt]', N'IX_PasswordHistory_UserId_ChangedDateTime', 'INDEX';

EXEC sp_rename N'[NotificationSetting].[Id]', N'NotificationSettingId', 'COLUMN';

EXEC sp_rename N'[NotificationSetting].[UpdatedBy]', N'ModifiedBy', 'COLUMN';

EXEC sp_rename N'[NotificationSetting].[UpdatedAt]', N'ModifiedDateTime', 'COLUMN';

EXEC sp_rename N'[NotificationSetting].[CreatedAt]', N'CreatedDateTime', 'COLUMN';

EXEC sp_rename N'[NotificationSetting].[IX_NotificationSettings_ScheduleId]', N'IX_NotificationSetting_ScheduleId', 'INDEX';

EXEC sp_rename N'[JobParameter].[Id]', N'JobParameterId', 'COLUMN';

EXEC sp_rename N'[JobParameter].[UpdatedBy]', N'ModifiedBy', 'COLUMN';

EXEC sp_rename N'[JobParameter].[UpdatedAt]', N'ModifiedDateTime', 'COLUMN';

EXEC sp_rename N'[JobParameter].[CreatedAt]', N'CreatedDateTime', 'COLUMN';

EXEC sp_rename N'[JobParameter].[IX_JobParameters_ScheduleId]', N'IX_JobParameter_ScheduleId', 'INDEX';

EXEC sp_rename N'[JobExecution].[Id]', N'JobExecutionId', 'COLUMN';

EXEC sp_rename N'[JobExecution].[UpdatedBy]', N'ModifiedBy', 'COLUMN';

EXEC sp_rename N'[JobExecution].[UpdatedAt]', N'ModifiedDateTime', 'COLUMN';

EXEC sp_rename N'[JobExecution].[StartTime]', N'StartDateTime', 'COLUMN';

EXEC sp_rename N'[JobExecution].[EndTime]', N'EndDateTime', 'COLUMN';

EXEC sp_rename N'[JobExecution].[CreatedAt]', N'CreatedDateTime', 'COLUMN';

EXEC sp_rename N'[JobExecution].[IX_JobExecutions_Status]', N'IX_JobExecution_Status', 'INDEX';

EXEC sp_rename N'[JobExecution].[IX_JobExecutions_StartTime]', N'IX_JobExecution_StartDateTime', 'INDEX';

EXEC sp_rename N'[JobExecution].[IX_JobExecutions_ScheduleId]', N'IX_JobExecution_ScheduleId', 'INDEX';

EXEC sp_rename N'[Client].[Id]', N'ClientId', 'COLUMN';

EXEC sp_rename N'[Client].[UpdatedBy]', N'ModifiedBy', 'COLUMN';

EXEC sp_rename N'[Client].[UpdatedAt]', N'ModifiedDateTime', 'COLUMN';

EXEC sp_rename N'[Client].[LastSyncedAt]', N'LastSyncedDateTime', 'COLUMN';

EXEC sp_rename N'[Client].[CreatedAt]', N'CreatedDateTime', 'COLUMN';

EXEC sp_rename N'[Client].[IX_Clients_LastSyncedAt]', N'IX_Client_LastSyncedDateTime', 'INDEX';

EXEC sp_rename N'[Client].[IX_Clients_ExternalClientId]', N'IX_Client_ExternalClientId', 'INDEX';

EXEC sp_rename N'[AuditLog].[Id]', N'AuditLogId', 'COLUMN';

EXEC sp_rename N'[AuditLog].[UpdatedBy]', N'ModifiedBy', 'COLUMN';

EXEC sp_rename N'[AuditLog].[UpdatedAt]', N'ModifiedDateTime', 'COLUMN';

EXEC sp_rename N'[AuditLog].[Timestamp]', N'TimestampDateTime', 'COLUMN';

EXEC sp_rename N'[AuditLog].[CreatedAt]', N'CreatedDateTime', 'COLUMN';

EXEC sp_rename N'[AuditLog].[IX_AuditLogs_Timestamp]', N'IX_AuditLog_TimestampDateTime', 'INDEX';

EXEC sp_rename N'[AuditLog].[IX_AuditLogs_EntityType_EntityId]', N'IX_AuditLog_EntityType_EntityId', 'INDEX';

DROP INDEX [IX_User_ClientId] ON [User];
DECLARE @var8 nvarchar(max);
SELECT @var8 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[User]') AND [c].[name] = N'ClientId');
IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [User] DROP CONSTRAINT ' + @var8 + ';');
ALTER TABLE [User] ALTER COLUMN [ClientId] bigint NOT NULL;
CREATE INDEX [IX_User_ClientId] ON [User] ([ClientId]);

ALTER TABLE [User] ADD [LastLoginDateTime] datetime2 NULL;

ALTER TABLE [User] ADD [MustChangePassword] bit NOT NULL DEFAULT CAST(0 AS bit);

DROP INDEX [IX_ScheduleSyncSource_ClientId] ON [ScheduleSyncSource];
DECLARE @var9 nvarchar(max);
SELECT @var9 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ScheduleSyncSource]') AND [c].[name] = N'ClientId');
IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [ScheduleSyncSource] DROP CONSTRAINT ' + @var9 + ';');
ALTER TABLE [ScheduleSyncSource] ALTER COLUMN [ClientId] bigint NULL;
CREATE INDEX [IX_ScheduleSyncSource_ClientId] ON [ScheduleSyncSource] ([ClientId]);

DROP INDEX [IX_Schedule_ClientId] ON [Schedule];
DECLARE @var10 nvarchar(max);
SELECT @var10 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Schedule]') AND [c].[name] = N'ClientId');
IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [Schedule] DROP CONSTRAINT ' + @var10 + ';');
ALTER TABLE [Schedule] ALTER COLUMN [ClientId] bigint NOT NULL;
CREATE INDEX [IX_Schedule_ClientId] ON [Schedule] ([ClientId]);

DECLARE @var11 nvarchar(max);
SELECT @var11 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[JobExecution]') AND [c].[name] = N'JobExecutionId');
IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [JobExecution] DROP CONSTRAINT ' + @var11 + ';');
ALTER TABLE [JobExecution] ALTER COLUMN [JobExecutionId] int NOT NULL;

DECLARE @var12 nvarchar(max);
SELECT @var12 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Client]') AND [c].[name] = N'ClientId');
IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [Client] DROP CONSTRAINT ' + @var12 + ';');
ALTER TABLE [Client] ALTER COLUMN [ClientId] bigint NOT NULL;

ALTER TABLE [Client] ADD [ClientCode] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [User] ADD CONSTRAINT [PK_User] PRIMARY KEY ([UserId]);

ALTER TABLE [UserPermission] ADD CONSTRAINT [PK_UserPermission] PRIMARY KEY ([UserPermissionId]);

ALTER TABLE [ScheduleSyncSource] ADD CONSTRAINT [PK_ScheduleSyncSource] PRIMARY KEY ([ScheduleSyncSourceId]);

ALTER TABLE [Schedule] ADD CONSTRAINT [PK_Schedule] PRIMARY KEY ([ScheduleId]);

ALTER TABLE [PasswordHistory] ADD CONSTRAINT [PK_PasswordHistory] PRIMARY KEY ([PasswordHistoryId]);

ALTER TABLE [NotificationSetting] ADD CONSTRAINT [PK_NotificationSetting] PRIMARY KEY ([NotificationSettingId]);

ALTER TABLE [JobParameter] ADD CONSTRAINT [PK_JobParameter] PRIMARY KEY ([JobParameterId]);

ALTER TABLE [JobExecution] ADD CONSTRAINT [PK_JobExecution] PRIMARY KEY ([JobExecutionId]);

ALTER TABLE [Client] ADD CONSTRAINT [PK_Client] PRIMARY KEY ([ClientId]);

ALTER TABLE [AuditLog] ADD CONSTRAINT [PK_AuditLog] PRIMARY KEY ([AuditLogId]);

ALTER TABLE [JobExecution] ADD CONSTRAINT [FK_JobExecution_Schedule_ScheduleId] FOREIGN KEY ([ScheduleId]) REFERENCES [Schedule] ([ScheduleId]) ON DELETE CASCADE;

ALTER TABLE [JobParameter] ADD CONSTRAINT [FK_JobParameter_Schedule_ScheduleId] FOREIGN KEY ([ScheduleId]) REFERENCES [Schedule] ([ScheduleId]) ON DELETE CASCADE;

ALTER TABLE [NotificationSetting] ADD CONSTRAINT [FK_NotificationSetting_Schedule_ScheduleId] FOREIGN KEY ([ScheduleId]) REFERENCES [Schedule] ([ScheduleId]) ON DELETE CASCADE;

ALTER TABLE [PasswordHistory] ADD CONSTRAINT [FK_PasswordHistory_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [User] ([UserId]) ON DELETE CASCADE;

ALTER TABLE [Schedule] ADD CONSTRAINT [FK_Schedule_Client_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Client] ([ClientId]) ON DELETE NO ACTION;

ALTER TABLE [ScheduleSyncSource] ADD CONSTRAINT [FK_ScheduleSyncSource_Client_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Client] ([ClientId]) ON DELETE NO ACTION;

ALTER TABLE [User] ADD CONSTRAINT [FK_User_Client_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Client] ([ClientId]) ON DELETE NO ACTION;

ALTER TABLE [UserPermission] ADD CONSTRAINT [FK_UserPermission_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [User] ([UserId]) ON DELETE CASCADE;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251203235246_RenameColumnsToNamingConventions', N'10.0.0');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [AdrAccount] (
    [AdrAccountId] int NOT NULL IDENTITY,
    [VMAccountId] bigint NOT NULL,
    [VMAccountNumber] nvarchar(30) NOT NULL,
    [InterfaceAccountId] nvarchar(30) NULL,
    [ClientId] bigint NULL,
    [ClientName] nvarchar(128) NULL,
    [CredentialId] int NOT NULL,
    [VendorCode] nvarchar(30) NULL,
    [PeriodType] nvarchar(13) NULL,
    [PeriodDays] int NULL,
    [MedianDays] float NULL,
    [InvoiceCount] int NOT NULL,
    [LastInvoiceDateTime] datetime2 NULL,
    [ExpectedNextDateTime] datetime2 NULL,
    [ExpectedRangeStartDateTime] datetime2 NULL,
    [ExpectedRangeEndDateTime] datetime2 NULL,
    [NextRunDateTime] datetime2 NULL,
    [NextRangeStartDateTime] datetime2 NULL,
    [NextRangeEndDateTime] datetime2 NULL,
    [DaysUntilNextRun] int NULL,
    [NextRunStatus] nvarchar(10) NULL,
    [HistoricalBillingStatus] nvarchar(10) NULL,
    [LastSyncedDateTime] datetime2 NULL,
    [CreatedDateTime] datetime2 NOT NULL,
    [ModifiedDateTime] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [ModifiedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_AdrAccount] PRIMARY KEY ([AdrAccountId]),
    CONSTRAINT [FK_AdrAccount_Client_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Client] ([ClientId]) ON DELETE NO ACTION
);

CREATE TABLE [AdrJob] (
    [AdrJobId] int NOT NULL IDENTITY,
    [AdrAccountId] int NOT NULL,
    [VMAccountId] bigint NOT NULL,
    [VMAccountNumber] nvarchar(30) NOT NULL,
    [CredentialId] int NOT NULL,
    [PeriodType] nvarchar(13) NULL,
    [BillingPeriodStartDateTime] datetime2 NOT NULL,
    [BillingPeriodEndDateTime] datetime2 NOT NULL,
    [NextRunDateTime] datetime2 NULL,
    [NextRangeStartDateTime] datetime2 NULL,
    [NextRangeEndDateTime] datetime2 NULL,
    [Status] nvarchar(50) NOT NULL,
    [IsMissing] bit NOT NULL,
    [AdrStatusId] int NULL,
    [AdrStatusDescription] nvarchar(100) NULL,
    [AdrIndexId] bigint NULL,
    [CredentialVerifiedDateTime] datetime2 NULL,
    [ScrapingCompletedDateTime] datetime2 NULL,
    [ErrorMessage] nvarchar(max) NULL,
    [RetryCount] int NOT NULL,
    [CreatedDateTime] datetime2 NOT NULL,
    [ModifiedDateTime] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [ModifiedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_AdrJob] PRIMARY KEY ([AdrJobId]),
    CONSTRAINT [FK_AdrJob_AdrAccount_AdrAccountId] FOREIGN KEY ([AdrAccountId]) REFERENCES [AdrAccount] ([AdrAccountId]) ON DELETE CASCADE
);

CREATE TABLE [AdrJobExecution] (
    [AdrJobExecutionId] int NOT NULL IDENTITY,
    [AdrJobId] int NOT NULL,
    [AdrRequestTypeId] int NOT NULL,
    [StartDateTime] datetime2 NOT NULL,
    [EndDateTime] datetime2 NULL,
    [AdrStatusId] int NULL,
    [AdrStatusDescription] nvarchar(100) NULL,
    [IsError] bit NOT NULL,
    [IsFinal] bit NOT NULL,
    [AdrIndexId] bigint NULL,
    [HttpStatusCode] int NULL,
    [IsSuccess] bit NOT NULL,
    [ErrorMessage] nvarchar(max) NULL,
    [ApiResponse] nvarchar(max) NULL,
    [RequestPayload] nvarchar(max) NULL,
    [CreatedDateTime] datetime2 NOT NULL,
    [ModifiedDateTime] datetime2 NULL,
    [CreatedBy] nvarchar(max) NULL,
    [ModifiedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_AdrJobExecution] PRIMARY KEY ([AdrJobExecutionId]),
    CONSTRAINT [FK_AdrJobExecution_AdrJob_AdrJobId] FOREIGN KEY ([AdrJobId]) REFERENCES [AdrJob] ([AdrJobId]) ON DELETE CASCADE
);

CREATE INDEX [IX_AdrAccount_ClientId] ON [AdrAccount] ([ClientId]);

CREATE INDEX [IX_AdrAccount_CredentialId] ON [AdrAccount] ([CredentialId]);

CREATE INDEX [IX_AdrAccount_HistoricalBillingStatus] ON [AdrAccount] ([HistoricalBillingStatus]);

CREATE INDEX [IX_AdrAccount_NextRunStatus] ON [AdrAccount] ([NextRunStatus]);

CREATE INDEX [IX_AdrAccount_VMAccountId] ON [AdrAccount] ([VMAccountId]);

CREATE INDEX [IX_AdrAccount_VMAccountId_VMAccountNumber] ON [AdrAccount] ([VMAccountId], [VMAccountNumber]);

CREATE INDEX [IX_AdrAccount_VMAccountNumber] ON [AdrAccount] ([VMAccountNumber]);

CREATE INDEX [IX_AdrJob_AdrAccountId] ON [AdrJob] ([AdrAccountId]);

CREATE INDEX [IX_AdrJob_AdrAccountId_BillingPeriodStartDateTime_BillingPeriodEndDateTime] ON [AdrJob] ([AdrAccountId], [BillingPeriodStartDateTime], [BillingPeriodEndDateTime]);

CREATE INDEX [IX_AdrJob_BillingPeriodStartDateTime] ON [AdrJob] ([BillingPeriodStartDateTime]);

CREATE INDEX [IX_AdrJob_CredentialId] ON [AdrJob] ([CredentialId]);

CREATE INDEX [IX_AdrJob_Status] ON [AdrJob] ([Status]);

CREATE INDEX [IX_AdrJob_VMAccountId] ON [AdrJob] ([VMAccountId]);

CREATE INDEX [IX_AdrJobExecution_AdrJobId] ON [AdrJobExecution] ([AdrJobId]);

CREATE INDEX [IX_AdrJobExecution_AdrRequestTypeId] ON [AdrJobExecution] ([AdrRequestTypeId]);

CREATE INDEX [IX_AdrJobExecution_IsSuccess] ON [AdrJobExecution] ([IsSuccess]);

CREATE INDEX [IX_AdrJobExecution_StartDateTime] ON [AdrJobExecution] ([StartDateTime]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20251208133504_AddAdrEntities', N'10.0.0');

COMMIT;
GO

