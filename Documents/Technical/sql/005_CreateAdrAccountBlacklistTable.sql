-- ============================================================================
-- Script: 005_CreateAdrAccountBlacklistTable.sql
-- Description: Creates the AdrAccountBlacklist table for storing exclusions
--              from ADR job creation. Allows excluding specific vendors,
--              accounts, or credentials. Part of Phase 2 BRD compliance.
-- Author: Devin AI
-- Date: 2025-12-17
-- ============================================================================

-- Create AdrAccountBlacklist table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AdrAccountBlacklist]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AdrAccountBlacklist] (
        -- Primary Key
        [AdrAccountBlacklistId] INT IDENTITY(1,1) NOT NULL,
        
        -- Exclusion criteria (can be used alone or in combination)
        [VendorCode] NVARCHAR(128) NULL,
        [VMAccountId] BIGINT NULL,
        [VMAccountNumber] NVARCHAR(128) NULL,
        [CredentialId] INT NULL,
        
        -- Exclusion type: All, CredentialCheck, Download
        [ExclusionType] NVARCHAR(20) NOT NULL DEFAULT 'All',
        
        -- Reason for blacklisting (required for audit)
        [Reason] NVARCHAR(500) NOT NULL,
        
        -- Active status
        [IsActive] BIT NOT NULL DEFAULT 1,
        
        -- Optional date range for temporary exclusions
        [EffectiveStartDate] DATETIME2 NULL,
        [EffectiveEndDate] DATETIME2 NULL,
        
        -- Blacklist tracking
        [BlacklistedBy] NVARCHAR(200) NULL,
        [BlacklistedDateTime] DATETIME2 NULL,
        
        -- Notes
        [Notes] NVARCHAR(MAX) NULL,
        
        -- Audit columns (standard convention)
        [CreatedDateTime] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CreatedBy] NVARCHAR(200) NOT NULL DEFAULT 'System',
        [ModifiedDateTime] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [ModifiedBy] NVARCHAR(200) NOT NULL DEFAULT 'System',
        [IsDeleted] BIT NOT NULL DEFAULT 0,
        
        -- Primary Key Constraint
        CONSTRAINT [PK_AdrAccountBlacklist] PRIMARY KEY CLUSTERED ([AdrAccountBlacklistId] ASC),
        
        -- Check constraint for ExclusionType
        CONSTRAINT [CK_AdrAccountBlacklist_ExclusionType] CHECK ([ExclusionType] IN ('All', 'CredentialCheck', 'Download'))
    );
    
    PRINT 'Created table AdrAccountBlacklist';
END
ELSE
BEGIN
    PRINT 'Table AdrAccountBlacklist already exists';
END
GO

-- Create indexes (matching DbContext configuration)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AdrAccountBlacklist_VendorCode' AND object_id = OBJECT_ID('dbo.AdrAccountBlacklist'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountBlacklist_VendorCode] 
    ON [dbo].[AdrAccountBlacklist] ([VendorCode]);
    PRINT 'Created index IX_AdrAccountBlacklist_VendorCode';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AdrAccountBlacklist_VMAccountId' AND object_id = OBJECT_ID('dbo.AdrAccountBlacklist'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountBlacklist_VMAccountId] 
    ON [dbo].[AdrAccountBlacklist] ([VMAccountId]);
    PRINT 'Created index IX_AdrAccountBlacklist_VMAccountId';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AdrAccountBlacklist_VMAccountNumber' AND object_id = OBJECT_ID('dbo.AdrAccountBlacklist'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountBlacklist_VMAccountNumber] 
    ON [dbo].[AdrAccountBlacklist] ([VMAccountNumber]);
    PRINT 'Created index IX_AdrAccountBlacklist_VMAccountNumber';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AdrAccountBlacklist_CredentialId' AND object_id = OBJECT_ID('dbo.AdrAccountBlacklist'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountBlacklist_CredentialId] 
    ON [dbo].[AdrAccountBlacklist] ([CredentialId]);
    PRINT 'Created index IX_AdrAccountBlacklist_CredentialId';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AdrAccountBlacklist_IsActive' AND object_id = OBJECT_ID('dbo.AdrAccountBlacklist'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountBlacklist_IsActive] 
    ON [dbo].[AdrAccountBlacklist] ([IsActive]);
    PRINT 'Created index IX_AdrAccountBlacklist_IsActive';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AdrAccountBlacklist_IsDeleted_IsActive' AND object_id = OBJECT_ID('dbo.AdrAccountBlacklist'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountBlacklist_IsDeleted_IsActive] 
    ON [dbo].[AdrAccountBlacklist] ([IsDeleted], [IsActive]);
    PRINT 'Created index IX_AdrAccountBlacklist_IsDeleted_IsActive';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AdrAccountBlacklist_VendorCode_VMAccountId_CredentialId' AND object_id = OBJECT_ID('dbo.AdrAccountBlacklist'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountBlacklist_VendorCode_VMAccountId_CredentialId] 
    ON [dbo].[AdrAccountBlacklist] ([VendorCode], [VMAccountId], [CredentialId]);
    PRINT 'Created index IX_AdrAccountBlacklist_VendorCode_VMAccountId_CredentialId';
END
GO

PRINT 'AdrAccountBlacklist table creation complete';
GO
