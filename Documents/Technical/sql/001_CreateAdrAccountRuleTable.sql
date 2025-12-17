-- ============================================================================
-- Script: 001_CreateAdrAccountRuleTable.sql
-- Description: Creates the AdrAccountRule table for storing scheduling rules
--              per ADR account. Part of Phase 1 BRD compliance implementation.
-- Author: Devin AI
-- Date: 2025-12-17
-- ============================================================================

-- Create AdrAccountRule table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AdrAccountRule]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AdrAccountRule] (
        -- Primary Key
        [AdrAccountRuleId] INT IDENTITY(1,1) NOT NULL,
        
        -- Foreign Key to AdrAccount
        [AdrAccountId] INT NOT NULL,
        
        -- Job Type (1 = AttemptLogin/Credential Check, 2 = DownloadInvoice/ADR Request)
        [JobTypeId] INT NOT NULL,
        
        -- Rule display name
        [RuleName] NVARCHAR(200) NOT NULL,
        
        -- Billing frequency configuration
        [PeriodType] NVARCHAR(13) NULL,  -- Bi-Weekly, Monthly, Bi-Monthly, Quarterly, Semi-Annually, Annually
        [PeriodDays] INT NULL,
        [DayOfMonth] INT NULL,
        
        -- Scheduling dates
        [NextRunDateTime] DATETIME2 NULL,
        [NextRangeStartDateTime] DATETIME2 NULL,
        [NextRangeEndDateTime] DATETIME2 NULL,
        
        -- Search window offsets
        [WindowDaysBefore] INT NULL,
        [WindowDaysAfter] INT NULL,
        
        -- Rule control
        [IsEnabled] BIT NOT NULL DEFAULT 1,
        [Priority] INT NOT NULL DEFAULT 100,
        
        -- Manual override tracking
        [IsManuallyOverridden] BIT NOT NULL DEFAULT 0,
        [OverriddenBy] NVARCHAR(200) NULL,
        [OverriddenDateTime] DATETIME2 NULL,
        
        -- Notes
        [Notes] NVARCHAR(MAX) NULL,
        
        -- Audit columns (standard convention)
        [CreatedDateTime] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CreatedBy] NVARCHAR(200) NOT NULL DEFAULT 'System',
        [ModifiedDateTime] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [ModifiedBy] NVARCHAR(200) NOT NULL DEFAULT 'System',
        [IsDeleted] BIT NOT NULL DEFAULT 0,
        
        -- Primary Key Constraint
        CONSTRAINT [PK_AdrAccountRule] PRIMARY KEY CLUSTERED ([AdrAccountRuleId] ASC),
        
        -- Foreign Key Constraint
        CONSTRAINT [FK_AdrAccountRule_AdrAccount] FOREIGN KEY ([AdrAccountId])
            REFERENCES [dbo].[AdrAccount] ([AdrAccountId])
            ON DELETE CASCADE
    );
    
    PRINT 'Created table AdrAccountRule';
END
ELSE
BEGIN
    PRINT 'Table AdrAccountRule already exists';
END
GO

-- Create indexes (matching DbContext configuration)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AdrAccountRule_AdrAccountId' AND object_id = OBJECT_ID('dbo.AdrAccountRule'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountRule_AdrAccountId] 
    ON [dbo].[AdrAccountRule] ([AdrAccountId]);
    PRINT 'Created index IX_AdrAccountRule_AdrAccountId';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AdrAccountRule_JobTypeId' AND object_id = OBJECT_ID('dbo.AdrAccountRule'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountRule_JobTypeId] 
    ON [dbo].[AdrAccountRule] ([JobTypeId]);
    PRINT 'Created index IX_AdrAccountRule_JobTypeId';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AdrAccountRule_IsEnabled' AND object_id = OBJECT_ID('dbo.AdrAccountRule'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountRule_IsEnabled] 
    ON [dbo].[AdrAccountRule] ([IsEnabled]);
    PRINT 'Created index IX_AdrAccountRule_IsEnabled';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AdrAccountRule_NextRunDateTime' AND object_id = OBJECT_ID('dbo.AdrAccountRule'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountRule_NextRunDateTime] 
    ON [dbo].[AdrAccountRule] ([NextRunDateTime]);
    PRINT 'Created index IX_AdrAccountRule_NextRunDateTime';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AdrAccountRule_AdrAccountId_JobTypeId' AND object_id = OBJECT_ID('dbo.AdrAccountRule'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountRule_AdrAccountId_JobTypeId] 
    ON [dbo].[AdrAccountRule] ([AdrAccountId], [JobTypeId]);
    PRINT 'Created index IX_AdrAccountRule_AdrAccountId_JobTypeId';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AdrAccountRule_IsDeleted_IsEnabled_NextRunDateTime' AND object_id = OBJECT_ID('dbo.AdrAccountRule'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountRule_IsDeleted_IsEnabled_NextRunDateTime] 
    ON [dbo].[AdrAccountRule] ([IsDeleted], [IsEnabled], [NextRunDateTime]);
    PRINT 'Created index IX_AdrAccountRule_IsDeleted_IsEnabled_NextRunDateTime';
END
GO

PRINT 'AdrAccountRule table creation complete';
GO
