-- Create AdrOrchestrationRun table for persisting orchestration history
-- This table stores the history of ADR orchestration runs so they survive application restarts

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AdrOrchestrationRun')
BEGIN
    CREATE TABLE [dbo].[AdrOrchestrationRun] (
        [AdrOrchestrationRunId] INT IDENTITY(1,1) NOT NULL,
        [RequestId] NVARCHAR(50) NOT NULL,
        [RequestedBy] NVARCHAR(200) NOT NULL,
        [RequestedDateTime] DATETIME2 NOT NULL,
        [StartedDateTime] DATETIME2 NULL,
        [CompletedDateTime] DATETIME2 NULL,
        [Status] NVARCHAR(20) NOT NULL,
        [CurrentStep] NVARCHAR(50) NULL,
        [CurrentProgress] NVARCHAR(50) NULL,
        [TotalItems] INT NULL,
        [ProcessedItems] INT NULL,
        [ErrorMessage] NVARCHAR(MAX) NULL,
        
        -- Step 1: Sync Accounts results
        [SyncAccountsInserted] INT NULL,
        [SyncAccountsUpdated] INT NULL,
        [SyncAccountsTotal] INT NULL,
        
        -- Step 2: Create Jobs results
        [JobsCreated] INT NULL,
        [JobsSkipped] INT NULL,
        
        -- Step 3: Verify Credentials results
        [CredentialsVerified] INT NULL,
        [CredentialsFailed] INT NULL,
        
        -- Step 4: Process Scraping results
        [ScrapingRequested] INT NULL,
        [ScrapingFailed] INT NULL,
        
        -- Step 5: Check Statuses results
        [StatusesChecked] INT NULL,
        [StatusesFailed] INT NULL,
        
        -- Audit fields (required by convention)
        [CreatedDateTime] DATETIME2 NOT NULL,
        [CreatedBy] NVARCHAR(200) NOT NULL,
        [ModifiedDateTime] DATETIME2 NOT NULL,
        [ModifiedBy] NVARCHAR(200) NOT NULL,
        [IsDeleted] BIT NOT NULL DEFAULT 0,
        
        CONSTRAINT [PK_AdrOrchestrationRun] PRIMARY KEY CLUSTERED ([AdrOrchestrationRunId] ASC)
    );
    
    PRINT 'Created table AdrOrchestrationRun';
END
ELSE
BEGIN
    PRINT 'Table AdrOrchestrationRun already exists';
END
GO

-- Create indexes for AdrOrchestrationRun table
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AdrOrchestrationRun_RequestId' AND object_id = OBJECT_ID('AdrOrchestrationRun'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_AdrOrchestrationRun_RequestId] 
    ON [dbo].[AdrOrchestrationRun] ([RequestId] ASC);
    PRINT 'Created index IX_AdrOrchestrationRun_RequestId';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AdrOrchestrationRun_RequestedDateTime' AND object_id = OBJECT_ID('AdrOrchestrationRun'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrOrchestrationRun_RequestedDateTime] 
    ON [dbo].[AdrOrchestrationRun] ([RequestedDateTime] DESC);
    PRINT 'Created index IX_AdrOrchestrationRun_RequestedDateTime';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AdrOrchestrationRun_Status' AND object_id = OBJECT_ID('AdrOrchestrationRun'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrOrchestrationRun_Status] 
    ON [dbo].[AdrOrchestrationRun] ([Status] ASC);
    PRINT 'Created index IX_AdrOrchestrationRun_Status';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AdrOrchestrationRun_Status_RequestedDateTime' AND object_id = OBJECT_ID('AdrOrchestrationRun'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrOrchestrationRun_Status_RequestedDateTime] 
    ON [dbo].[AdrOrchestrationRun] ([Status] ASC, [RequestedDateTime] DESC);
    PRINT 'Created index IX_AdrOrchestrationRun_Status_RequestedDateTime';
END
GO

PRINT 'AdrOrchestrationRun table setup complete';
