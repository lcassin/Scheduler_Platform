-- =============================================
-- Script: 009_CreateArchiveTables.sql
-- Description: Creates archive tables for data retention
--              - AdrJobArchive: Archives AdrJob records older than retention period
--              - AdrJobExecutionArchive: Archives AdrJobExecution records
--              - AuditLogArchive: Archives AuditLog records
-- Run Order: After 008_AddAdrAccountRuleJobTypeFk.sql
-- =============================================

USE [SchedulerPlatform]
GO

-- =============================================
-- Create AdrJobArchive table
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AdrJobArchive]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AdrJobArchive](
        [AdrJobArchiveId] INT IDENTITY(1,1) NOT NULL,
        [OriginalAdrJobId] INT NOT NULL,
        [AdrAccountId] INT NOT NULL,
        [AdrAccountRuleId] INT NULL,
        [VMAccountId] BIGINT NOT NULL,
        [VMAccountNumber] NVARCHAR(128) NOT NULL,
        [VendorCode] NVARCHAR(128) NULL,
        [CredentialId] INT NOT NULL,
        [PeriodType] NVARCHAR(13) NULL,
        [BillingPeriodStartDateTime] DATETIME2(7) NOT NULL,
        [BillingPeriodEndDateTime] DATETIME2(7) NOT NULL,
        [NextRunDateTime] DATETIME2(7) NULL,
        [NextRangeStartDateTime] DATETIME2(7) NULL,
        [NextRangeEndDateTime] DATETIME2(7) NULL,
        [Status] NVARCHAR(50) NOT NULL,
        [IsMissing] BIT NOT NULL DEFAULT 0,
        [AdrStatusId] INT NULL,
        [AdrStatusDescription] NVARCHAR(100) NULL,
        [AdrIndexId] BIGINT NULL,
        [CredentialVerifiedDateTime] DATETIME2(7) NULL,
        [ScrapingCompletedDateTime] DATETIME2(7) NULL,
        [ErrorMessage] NVARCHAR(MAX) NULL,
        [RetryCount] INT NOT NULL DEFAULT 0,
        [IsManualRequest] BIT NOT NULL DEFAULT 0,
        [ManualRequestReason] NVARCHAR(MAX) NULL,
        [LastStatusCheckResponse] NVARCHAR(MAX) NULL,
        [LastStatusCheckDateTime] DATETIME2(7) NULL,
        [CreatedDateTime] DATETIME2(7) NOT NULL,
        [CreatedBy] NVARCHAR(200) NOT NULL,
        [ModifiedDateTime] DATETIME2(7) NOT NULL,
        [ModifiedBy] NVARCHAR(200) NOT NULL,
        [ArchivedDateTime] DATETIME2(7) NOT NULL,
        [ArchivedBy] NVARCHAR(200) NOT NULL,
        CONSTRAINT [PK_AdrJobArchive] PRIMARY KEY CLUSTERED ([AdrJobArchiveId] ASC)
    );

    CREATE NONCLUSTERED INDEX [IX_AdrJobArchive_OriginalAdrJobId] ON [dbo].[AdrJobArchive]([OriginalAdrJobId]);
    CREATE NONCLUSTERED INDEX [IX_AdrJobArchive_AdrAccountId] ON [dbo].[AdrJobArchive]([AdrAccountId]);
    CREATE NONCLUSTERED INDEX [IX_AdrJobArchive_VMAccountId] ON [dbo].[AdrJobArchive]([VMAccountId]);
    CREATE NONCLUSTERED INDEX [IX_AdrJobArchive_ArchivedDateTime] ON [dbo].[AdrJobArchive]([ArchivedDateTime]);
    CREATE NONCLUSTERED INDEX [IX_AdrJobArchive_BillingPeriodStartDateTime] ON [dbo].[AdrJobArchive]([BillingPeriodStartDateTime]);

    PRINT 'Created AdrJobArchive table with indexes';
END
ELSE
BEGIN
    PRINT 'AdrJobArchive table already exists';
END
GO

-- =============================================
-- Create AdrJobExecutionArchive table
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AdrJobExecutionArchive]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AdrJobExecutionArchive](
        [AdrJobExecutionArchiveId] INT IDENTITY(1,1) NOT NULL,
        [OriginalAdrJobExecutionId] INT NOT NULL,
        [AdrJobId] INT NOT NULL,
        [AdrRequestTypeId] INT NOT NULL,
        [StartDateTime] DATETIME2(7) NOT NULL,
        [EndDateTime] DATETIME2(7) NULL,
        [AdrStatusId] INT NULL,
        [AdrStatusDescription] NVARCHAR(100) NULL,
        [IsError] BIT NOT NULL DEFAULT 0,
        [IsFinal] BIT NOT NULL DEFAULT 0,
        [AdrIndexId] BIGINT NULL,
        [HttpStatusCode] INT NULL,
        [IsSuccess] BIT NOT NULL DEFAULT 0,
        [ErrorMessage] NVARCHAR(MAX) NULL,
        [ApiResponse] NVARCHAR(MAX) NULL,
        [RequestPayload] NVARCHAR(MAX) NULL,
        [CreatedDateTime] DATETIME2(7) NOT NULL,
        [CreatedBy] NVARCHAR(200) NOT NULL,
        [ModifiedDateTime] DATETIME2(7) NOT NULL,
        [ModifiedBy] NVARCHAR(200) NOT NULL,
        [ArchivedDateTime] DATETIME2(7) NOT NULL,
        [ArchivedBy] NVARCHAR(200) NOT NULL,
        CONSTRAINT [PK_AdrJobExecutionArchive] PRIMARY KEY CLUSTERED ([AdrJobExecutionArchiveId] ASC)
    );

    CREATE NONCLUSTERED INDEX [IX_AdrJobExecutionArchive_OriginalAdrJobExecutionId] ON [dbo].[AdrJobExecutionArchive]([OriginalAdrJobExecutionId]);
    CREATE NONCLUSTERED INDEX [IX_AdrJobExecutionArchive_AdrJobId] ON [dbo].[AdrJobExecutionArchive]([AdrJobId]);
    CREATE NONCLUSTERED INDEX [IX_AdrJobExecutionArchive_ArchivedDateTime] ON [dbo].[AdrJobExecutionArchive]([ArchivedDateTime]);
    CREATE NONCLUSTERED INDEX [IX_AdrJobExecutionArchive_StartDateTime] ON [dbo].[AdrJobExecutionArchive]([StartDateTime]);

    PRINT 'Created AdrJobExecutionArchive table with indexes';
END
ELSE
BEGIN
    PRINT 'AdrJobExecutionArchive table already exists';
END
GO

-- =============================================
-- Create AuditLogArchive table
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AuditLogArchive]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AuditLogArchive](
        [AuditLogArchiveId] INT IDENTITY(1,1) NOT NULL,
        [OriginalAuditLogId] INT NOT NULL,
        [EventType] NVARCHAR(100) NOT NULL,
        [EntityType] NVARCHAR(100) NOT NULL,
        [EntityId] INT NULL,
        [Action] NVARCHAR(50) NOT NULL,
        [OldValues] NVARCHAR(MAX) NULL,
        [NewValues] NVARCHAR(MAX) NULL,
        [UserName] NVARCHAR(200) NOT NULL,
        [ClientId] INT NULL,
        [IpAddress] NVARCHAR(50) NULL,
        [UserAgent] NVARCHAR(500) NULL,
        [TimestampDateTime] DATETIME2(7) NOT NULL,
        [AdditionalData] NVARCHAR(MAX) NULL,
        [CreatedDateTime] DATETIME2(7) NOT NULL,
        [CreatedBy] NVARCHAR(200) NOT NULL,
        [ModifiedDateTime] DATETIME2(7) NOT NULL,
        [ModifiedBy] NVARCHAR(200) NOT NULL,
        [ArchivedDateTime] DATETIME2(7) NOT NULL,
        [ArchivedBy] NVARCHAR(200) NOT NULL,
        CONSTRAINT [PK_AuditLogArchive] PRIMARY KEY CLUSTERED ([AuditLogArchiveId] ASC)
    );

    CREATE NONCLUSTERED INDEX [IX_AuditLogArchive_OriginalAuditLogId] ON [dbo].[AuditLogArchive]([OriginalAuditLogId]);
    CREATE NONCLUSTERED INDEX [IX_AuditLogArchive_ArchivedDateTime] ON [dbo].[AuditLogArchive]([ArchivedDateTime]);
    CREATE NONCLUSTERED INDEX [IX_AuditLogArchive_TimestampDateTime] ON [dbo].[AuditLogArchive]([TimestampDateTime]);
    CREATE NONCLUSTERED INDEX [IX_AuditLogArchive_EntityType_EntityId] ON [dbo].[AuditLogArchive]([EntityType], [EntityId]);

    PRINT 'Created AuditLogArchive table with indexes';
END
ELSE
BEGIN
    PRINT 'AuditLogArchive table already exists';
END
GO

-- =============================================
-- Add retention configuration columns to AdrConfiguration
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AdrConfiguration]') AND name = 'JobRetentionMonths')
BEGIN
    ALTER TABLE [dbo].[AdrConfiguration] ADD [JobRetentionMonths] INT NOT NULL DEFAULT 12;
    PRINT 'Added JobRetentionMonths column to AdrConfiguration';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AdrConfiguration]') AND name = 'JobExecutionRetentionMonths')
BEGIN
    ALTER TABLE [dbo].[AdrConfiguration] ADD [JobExecutionRetentionMonths] INT NOT NULL DEFAULT 12;
    PRINT 'Added JobExecutionRetentionMonths column to AdrConfiguration';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AdrConfiguration]') AND name = 'AuditLogRetentionDays')
BEGIN
    ALTER TABLE [dbo].[AdrConfiguration] ADD [AuditLogRetentionDays] INT NOT NULL DEFAULT 90;
    PRINT 'Added AuditLogRetentionDays column to AdrConfiguration';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AdrConfiguration]') AND name = 'IsArchivalEnabled')
BEGIN
    ALTER TABLE [dbo].[AdrConfiguration] ADD [IsArchivalEnabled] BIT NOT NULL DEFAULT 1;
    PRINT 'Added IsArchivalEnabled column to AdrConfiguration';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AdrConfiguration]') AND name = 'ArchivalBatchSize')
BEGIN
    ALTER TABLE [dbo].[AdrConfiguration] ADD [ArchivalBatchSize] INT NOT NULL DEFAULT 5000;
    PRINT 'Added ArchivalBatchSize column to AdrConfiguration';
END
GO

-- =============================================
-- Update existing configuration record with default retention values
-- =============================================
UPDATE [dbo].[AdrConfiguration]
SET [JobRetentionMonths] = 12,
    [JobExecutionRetentionMonths] = 12,
    [AuditLogRetentionDays] = 90,
    [IsArchivalEnabled] = 1,
    [ArchivalBatchSize] = 5000,
    [ModifiedDateTime] = GETUTCDATE(),
    [ModifiedBy] = 'System Migration'
WHERE [IsDeleted] = 0;

PRINT 'Updated existing AdrConfiguration record with retention settings';
GO

-- =============================================
-- Verification queries
-- =============================================
PRINT '';
PRINT '=== Archive Tables Created ===';
SELECT 
    t.name AS TableName,
    SUM(p.rows) AS RowCount
FROM sys.tables t
INNER JOIN sys.partitions p ON t.object_id = p.object_id
WHERE t.name IN ('AdrJobArchive', 'AdrJobExecutionArchive', 'AuditLogArchive')
    AND p.index_id IN (0, 1)
GROUP BY t.name
ORDER BY t.name;

PRINT '';
PRINT '=== AdrConfiguration Retention Settings ===';
SELECT 
    AdrConfigurationId,
    JobRetentionMonths,
    JobExecutionRetentionMonths,
    AuditLogRetentionDays,
    IsArchivalEnabled,
    ArchivalBatchSize
FROM [dbo].[AdrConfiguration]
WHERE [IsDeleted] = 0;
GO
