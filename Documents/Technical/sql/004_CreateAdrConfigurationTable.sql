-- ============================================================================
-- Script: 004_CreateAdrConfigurationTable.sql
-- Description: Creates the AdrConfiguration table for storing global ADR
--              orchestration settings. This is a single-row table that replaces
--              hardcoded values in appsettings.json. Part of Phase 2 BRD compliance.
-- Author: Devin AI
-- Date: 2025-12-17
-- ============================================================================

-- Create AdrConfiguration table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AdrConfiguration]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AdrConfiguration] (
        -- Primary Key
        [AdrConfigurationId] INT IDENTITY(1,1) NOT NULL,
        
        -- Credential verification settings
        [CredentialCheckLeadDays] INT NOT NULL DEFAULT 7,
        
        -- Scrape retry settings
        [ScrapeRetryDays] INT NOT NULL DEFAULT 5,
        [MaxRetries] INT NOT NULL DEFAULT 5,
        
        -- Status check settings
        [FinalStatusCheckDelayDays] INT NOT NULL DEFAULT 5,
        [DailyStatusCheckDelayDays] INT NOT NULL DEFAULT 1,
        
        -- Parallel processing settings
        [MaxParallelRequests] INT NOT NULL DEFAULT 8,
        [BatchSize] INT NOT NULL DEFAULT 1000,
        
        -- Invoice window settings
        [DefaultWindowDaysBefore] INT NOT NULL DEFAULT 5,
        [DefaultWindowDaysAfter] INT NOT NULL DEFAULT 5,
        
        -- Feature flags
        [AutoCreateTestLoginRules] BIT NOT NULL DEFAULT 1,
        [AutoCreateMissingInvoiceAlerts] BIT NOT NULL DEFAULT 1,
        [MissingInvoiceAlertEmail] NVARCHAR(255) NULL,
        [IsOrchestrationEnabled] BIT NOT NULL DEFAULT 1,
        
        -- Notes
        [Notes] NVARCHAR(MAX) NULL,
        
        -- Audit columns (standard convention)
        [CreatedDateTime] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CreatedBy] NVARCHAR(200) NOT NULL DEFAULT 'System',
        [ModifiedDateTime] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [ModifiedBy] NVARCHAR(200) NOT NULL DEFAULT 'System',
        [IsDeleted] BIT NOT NULL DEFAULT 0,
        
        -- Primary Key Constraint
        CONSTRAINT [PK_AdrConfiguration] PRIMARY KEY CLUSTERED ([AdrConfigurationId] ASC)
    );
    
    PRINT 'Created table AdrConfiguration';
END
ELSE
BEGIN
    PRINT 'Table AdrConfiguration already exists';
END
GO

-- Insert default configuration row if table is empty
IF NOT EXISTS (SELECT 1 FROM [dbo].[AdrConfiguration] WHERE [IsDeleted] = 0)
BEGIN
    INSERT INTO [dbo].[AdrConfiguration] (
        [CredentialCheckLeadDays],
        [ScrapeRetryDays],
        [MaxRetries],
        [FinalStatusCheckDelayDays],
        [DailyStatusCheckDelayDays],
        [MaxParallelRequests],
        [BatchSize],
        [DefaultWindowDaysBefore],
        [DefaultWindowDaysAfter],
        [AutoCreateTestLoginRules],
        [AutoCreateMissingInvoiceAlerts],
        [IsOrchestrationEnabled],
        [Notes],
        [CreatedDateTime],
        [CreatedBy],
        [ModifiedDateTime],
        [ModifiedBy],
        [IsDeleted]
    )
    VALUES (
        7,      -- CredentialCheckLeadDays
        5,      -- ScrapeRetryDays
        5,      -- MaxRetries
        5,      -- FinalStatusCheckDelayDays
        1,      -- DailyStatusCheckDelayDays
        8,      -- MaxParallelRequests
        1000,   -- BatchSize
        5,      -- DefaultWindowDaysBefore
        5,      -- DefaultWindowDaysAfter
        1,      -- AutoCreateTestLoginRules
        1,      -- AutoCreateMissingInvoiceAlerts
        1,      -- IsOrchestrationEnabled
        'Default configuration created during Phase 2 migration',
        GETUTCDATE(),
        'System-Migration',
        GETUTCDATE(),
        'System-Migration',
        0
    );
    
    PRINT 'Inserted default configuration row';
END
ELSE
BEGIN
    PRINT 'Configuration row already exists';
END
GO

PRINT 'AdrConfiguration table creation complete';
GO
