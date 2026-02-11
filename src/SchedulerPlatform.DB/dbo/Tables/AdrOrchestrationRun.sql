CREATE TABLE [dbo].[AdrOrchestrationRun] (
    [AdrOrchestrationRunId] INT            IDENTITY (1, 1) NOT NULL,
    [RequestId]             NVARCHAR (50)  NOT NULL,
    [RequestedBy]           NVARCHAR (200) NOT NULL,
    [RequestedDateTime]     DATETIME2 (7)  NOT NULL,
    [StartedDateTime]       DATETIME2 (7)  NULL,
    [CompletedDateTime]     DATETIME2 (7)  NULL,
    [Status]                NVARCHAR (20)  NOT NULL,
    [CurrentStep]           NVARCHAR (50)  NULL,
    [CurrentProgress]       NVARCHAR (50)  NULL,
    [TotalItems]            INT            NULL,
    [ProcessedItems]        INT            NULL,
    [ErrorMessage]          NVARCHAR (MAX) NULL,
    [SyncAccountsInserted]  INT            NULL,
    [SyncAccountsUpdated]   INT            NULL,
    [SyncAccountsTotal]     INT            NULL,
    [JobsCreated]           INT            NULL,
    [JobsSkipped]           INT            NULL,
    [CredentialsVerified]   INT            NULL,
    [CredentialsFailed]     INT            NULL,
    [ScrapingRequested]     INT            NULL,
    [ScrapingFailed]        INT            NULL,
    [StatusesChecked]       INT            NULL,
    [StatusesFailed]        INT            NULL,
    [SyncDurationSeconds]   FLOAT          NULL,
    [JobCreationDurationSeconds] FLOAT     NULL,
    [RebillDurationSeconds] FLOAT          NULL,
    [ScrapingDurationSeconds] FLOAT        NULL,
    [StatusCheckDurationSeconds] FLOAT     NULL,
    [CreatedDateTime]       DATETIME2 (7)  NOT NULL,
    [CreatedBy]             NVARCHAR (200) NOT NULL,
    [ModifiedDateTime]      DATETIME2 (7)  NOT NULL,
    [ModifiedBy]            NVARCHAR (200) NOT NULL,
    [IsDeleted]             BIT            DEFAULT ((0)) NOT NULL,
    CONSTRAINT [PK_AdrOrchestrationRun] PRIMARY KEY CLUSTERED ([AdrOrchestrationRunId] ASC)
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_AdrOrchestrationRun_RequestId]
    ON [dbo].[AdrOrchestrationRun]([RequestId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrOrchestrationRun_RequestedDateTime]
    ON [dbo].[AdrOrchestrationRun]([RequestedDateTime] DESC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrOrchestrationRun_Status]
    ON [dbo].[AdrOrchestrationRun]([Status] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrOrchestrationRun_Status_RequestedDateTime]
    ON [dbo].[AdrOrchestrationRun]([Status] ASC, [RequestedDateTime] DESC);

