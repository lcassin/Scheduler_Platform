-- =============================================
-- Script: 007_CreateAdrJobTypeTable.sql
-- Description: Creates the AdrJobType table to replace the hardcoded AdrRequestType enum.
--              This table allows job types to be managed via the admin UI.
-- Phase: 3 - Job Types
-- Run Order: After scripts 001-006
-- =============================================

-- Create AdrJobType table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AdrJobType')
BEGIN
    CREATE TABLE [dbo].[AdrJobType] (
        [AdrJobTypeId] INT IDENTITY(1,1) NOT NULL,
        [Code] NVARCHAR(50) NOT NULL,
        [Name] NVARCHAR(100) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [EndpointUrl] NVARCHAR(500) NULL,
        [AdrRequestTypeId] INT NOT NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [DisplayOrder] INT NOT NULL DEFAULT 0,
        [Notes] NVARCHAR(MAX) NULL,
        [CreatedDateTime] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CreatedBy] NVARCHAR(200) NULL,
        [ModifiedDateTime] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [ModifiedBy] NVARCHAR(200) NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0,
        CONSTRAINT [PK_AdrJobType] PRIMARY KEY CLUSTERED ([AdrJobTypeId])
    );
    
    PRINT 'Created AdrJobType table';
END
ELSE
BEGIN
    PRINT 'AdrJobType table already exists';
END
GO

-- Create unique index on Code
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AdrJobType_Code' AND object_id = OBJECT_ID('AdrJobType'))
BEGIN
    CREATE UNIQUE INDEX [IX_AdrJobType_Code] ON [dbo].[AdrJobType] ([Code]);
    PRINT 'Created unique index IX_AdrJobType_Code';
END
GO

-- Create index on IsActive
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AdrJobType_IsActive' AND object_id = OBJECT_ID('AdrJobType'))
BEGIN
    CREATE INDEX [IX_AdrJobType_IsActive] ON [dbo].[AdrJobType] ([IsActive]);
    PRINT 'Created index IX_AdrJobType_IsActive';
END
GO

-- Create index on AdrRequestTypeId
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AdrJobType_AdrRequestTypeId' AND object_id = OBJECT_ID('AdrJobType'))
BEGIN
    CREATE INDEX [IX_AdrJobType_AdrRequestTypeId] ON [dbo].[AdrJobType] ([AdrRequestTypeId]);
    PRINT 'Created index IX_AdrJobType_AdrRequestTypeId';
END
GO

-- Insert default job types (matching existing AdrRequestType enum values)
-- AdrRequestType.AttemptLogin = 1, AdrRequestType.DownloadInvoice = 2, AdrRequestType.Rebill = 3
IF NOT EXISTS (SELECT 1 FROM [dbo].[AdrJobType] WHERE [Code] = 'CREDENTIAL_CHECK')
BEGIN
    INSERT INTO [dbo].[AdrJobType] (
        [Code],
        [Name],
        [Description],
        [EndpointUrl],
        [AdrRequestTypeId],
        [IsActive],
        [DisplayOrder],
        [CreatedBy],
        [ModifiedBy]
    )
    VALUES (
        'CREDENTIAL_CHECK',
        'Credential Check',
        'Verifies that the stored credentials can successfully log in to the vendor website. This is typically run before attempting to download invoices.',
        NULL,  -- Uses default endpoint from configuration
        1,     -- Maps to AdrRequestType.AttemptLogin
        1,     -- IsActive
        1,     -- DisplayOrder
        'System',
        'System'
    );
    PRINT 'Inserted CREDENTIAL_CHECK job type';
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[AdrJobType] WHERE [Code] = 'DOWNLOAD_INVOICE')
BEGIN
    INSERT INTO [dbo].[AdrJobType] (
        [Code],
        [Name],
        [Description],
        [EndpointUrl],
        [AdrRequestTypeId],
        [IsActive],
        [DisplayOrder],
        [CreatedBy],
        [ModifiedBy]
    )
    VALUES (
        'DOWNLOAD_INVOICE',
        'Download Invoice',
        'Downloads invoices from the vendor website for the specified billing period. This is the primary ADR request type.',
        NULL,  -- Uses default endpoint from configuration
        2,     -- Maps to AdrRequestType.DownloadInvoice
        1,     -- IsActive
        2,     -- DisplayOrder
        'System',
        'System'
    );
    PRINT 'Inserted DOWNLOAD_INVOICE job type';
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[AdrJobType] WHERE [Code] = 'REBILL')
BEGIN
    INSERT INTO [dbo].[AdrJobType] (
        [Code],
        [Name],
        [Description],
        [EndpointUrl],
        [AdrRequestTypeId],
        [IsActive],
        [DisplayOrder],
        [CreatedBy],
        [ModifiedBy]
    )
    VALUES (
        'REBILL',
        'Rebill Check',
        'Weekly check for updated bills, partial invoices, and off-cycle invoices. Also verifies credentials. Unlike Download Invoice, rebill checks do NOT create Zendesk tickets when no document is found (only for credential failures).',
        NULL,  -- Uses default endpoint from configuration
        3,     -- Maps to AdrRequestType.Rebill
        1,     -- IsActive
        3,     -- DisplayOrder
        'System',
        'System'
    );
    PRINT 'Inserted REBILL job type';
END
GO

-- Verification queries
SELECT 'AdrJobType Records' AS [Check], COUNT(*) AS [Count] FROM [dbo].[AdrJobType];
SELECT * FROM [dbo].[AdrJobType] ORDER BY [AdrJobTypeId];
GO
