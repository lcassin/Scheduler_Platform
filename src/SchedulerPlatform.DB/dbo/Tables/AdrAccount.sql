CREATE TABLE [dbo].[AdrAccount] (
    [AdrAccountId]               INT            IDENTITY (1, 1) NOT NULL,
    [VMAccountId]                BIGINT         NOT NULL,
    [VMAccountNumber]            NVARCHAR (128) NULL,
    [InterfaceAccountId]         NVARCHAR (128) NULL,
    [ClientId]                   BIGINT         NULL,
    [ClientName]                 NVARCHAR (128) NULL,
    [CredentialId]               INT            NOT NULL,
    [VendorCode]                 NVARCHAR (128) NULL,
    [PeriodType]                 NVARCHAR (13)  NULL,
    [PeriodDays]                 INT            NULL,
    [MedianDays]                 FLOAT (53)     NULL,
    [InvoiceCount]               INT            NOT NULL,
    [LastInvoiceDateTime]        DATETIME2 (7)  NULL,
    [ExpectedNextDateTime]       DATETIME2 (7)  NULL,
    [ExpectedRangeStartDateTime] DATETIME2 (7)  NULL,
    [ExpectedRangeEndDateTime]   DATETIME2 (7)  NULL,
    [NextRunDateTime]            DATETIME2 (7)  NULL,
    [NextRangeStartDateTime]     DATETIME2 (7)  NULL,
    [NextRangeEndDateTime]       DATETIME2 (7)  NULL,
    [DaysUntilNextRun]           INT            NULL,
    [NextRunStatus]              NVARCHAR (10)  NULL,
    [HistoricalBillingStatus]    NVARCHAR (10)  NULL,
    [LastSyncedDateTime]         DATETIME2 (7)  NULL,
    [CreatedDateTime]            DATETIME2 (7)  NOT NULL,
    [ModifiedDateTime]           DATETIME2 (7)  NOT NULL,
    [CreatedBy]                  NVARCHAR (MAX) NOT NULL,
    [ModifiedBy]                 NVARCHAR (MAX) NOT NULL,
    [IsDeleted]                  BIT            NOT NULL,
    [IsManuallyOverridden]       BIT            CONSTRAINT [DF_AdrAccount_IsManuallyOverridden] DEFAULT ((0)) NOT NULL,
    [OverriddenBy]               NVARCHAR (256) NULL,
    [OverriddenDateTime]         DATETIME2 (7)  NULL,
    [LastSuccessfulDownloadDate] DATETIME2 (7)  NULL,
    CONSTRAINT [PK_AdrAccount] PRIMARY KEY CLUSTERED ([AdrAccountId] ASC),
    CONSTRAINT [FK_AdrAccount_Client_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Client] ([ClientId])
);


GO
CREATE NONCLUSTERED INDEX [IX_AdrAccount_ClientId]
    ON [dbo].[AdrAccount]([ClientId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrAccount_CredentialId]
    ON [dbo].[AdrAccount]([CredentialId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrAccount_HistoricalBillingStatus]
    ON [dbo].[AdrAccount]([HistoricalBillingStatus] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrAccount_NextRunStatus]
    ON [dbo].[AdrAccount]([NextRunStatus] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrAccount_VMAccountId]
    ON [dbo].[AdrAccount]([VMAccountId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrAccount_VMAccountId_VMAccountNumber]
    ON [dbo].[AdrAccount]([VMAccountId] ASC, [VMAccountNumber] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrAccount_VMAccountNumber]
    ON [dbo].[AdrAccount]([VMAccountNumber] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrAccount_NextRunDateTime]
    ON [dbo].[AdrAccount]([NextRunDateTime] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrAccount_InterfaceAccountId]
    ON [dbo].[AdrAccount]([InterfaceAccountId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrAccount_VendorCode]
    ON [dbo].[AdrAccount]([VendorCode] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrAccount_IsDeleted_NextRunStatus_NextRunDateTime]
    ON [dbo].[AdrAccount]([IsDeleted] ASC, [NextRunStatus] ASC, [NextRunDateTime] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrAccount_IsDeleted_HistoricalBillingStatus]
    ON [dbo].[AdrAccount]([IsDeleted] ASC, [HistoricalBillingStatus] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrAccount_IsDeleted_ClientId_NextRunStatus]
    ON [dbo].[AdrAccount]([IsDeleted] ASC, [ClientId] ASC, [NextRunStatus] ASC);

