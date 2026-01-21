CREATE TABLE [dbo].[AdrJob] (
    [AdrJobId]                   INT            IDENTITY (1, 1) NOT NULL,
    [AdrAccountId]               INT            NOT NULL,
    [VMAccountId]                BIGINT         NOT NULL,
    [VMAccountNumber]            NVARCHAR (30)  NOT NULL,
    [VendorCode]                 NVARCHAR (128) NULL,
    [CredentialId]               INT            NOT NULL,
    [PeriodType]                 NVARCHAR (13)  NULL,
    [BillingPeriodStartDateTime] DATETIME2 (7)  NOT NULL,
    [BillingPeriodEndDateTime]   DATETIME2 (7)  NOT NULL,
    [NextRunDateTime]            DATETIME2 (7)  NULL,
    [NextRangeStartDateTime]     DATETIME2 (7)  NULL,
    [NextRangeEndDateTime]       DATETIME2 (7)  NULL,
    [Status]                     NVARCHAR (50)  NOT NULL,
    [IsMissing]                  BIT            NOT NULL,
    [AdrStatusId]                INT            NULL,
    [AdrStatusDescription]       NVARCHAR (100) NULL,
    [AdrIndexId]                 BIGINT         NULL,
    [CredentialVerifiedDateTime] DATETIME2 (7)  NULL,
    [ScrapingCompletedDateTime]  DATETIME2 (7)  NULL,
    [ErrorMessage]               NVARCHAR (MAX) NULL,
    [RetryCount]                 INT            NOT NULL,
    [CreatedDateTime]            DATETIME2 (7)  NOT NULL,
    [ModifiedDateTime]           DATETIME2 (7)  NOT NULL,
    [CreatedBy]                  NVARCHAR (MAX) NOT NULL,
    [ModifiedBy]                 NVARCHAR (MAX) NOT NULL,
    [IsDeleted]                  BIT            NOT NULL,
    [IsManualRequest]            BIT            DEFAULT ((0)) NOT NULL,
    [ManualRequestReason]        NVARCHAR (500) NULL,
    [LastStatusCheckResponse]    NVARCHAR (MAX) NULL,
    [LastStatusCheckDateTime]    DATETIME2 (7)  NULL,
    [AdrAccountRuleId]           INT            NULL,
    CONSTRAINT [PK_AdrJob] PRIMARY KEY CLUSTERED ([AdrJobId] ASC),
    CONSTRAINT [FK_AdrJob_AdrAccount_AdrAccountId] FOREIGN KEY ([AdrAccountId]) REFERENCES [dbo].[AdrAccount] ([AdrAccountId]) ON DELETE CASCADE,
    CONSTRAINT [FK_AdrJob_AdrAccountRule] FOREIGN KEY ([AdrAccountRuleId]) REFERENCES [dbo].[AdrAccountRule] ([AdrAccountRuleId])
);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJob_AdrAccountId]
    ON [dbo].[AdrJob]([AdrAccountId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJob_AdrAccountId_BillingPeriodStartDateTime_BillingPeriodEndDateTime]
    ON [dbo].[AdrJob]([AdrAccountId] ASC, [BillingPeriodStartDateTime] ASC, [BillingPeriodEndDateTime] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJob_BillingPeriodStartDateTime]
    ON [dbo].[AdrJob]([BillingPeriodStartDateTime] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJob_CredentialId]
    ON [dbo].[AdrJob]([CredentialId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJob_Status]
    ON [dbo].[AdrJob]([Status] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJob_VMAccountId]
    ON [dbo].[AdrJob]([VMAccountId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJob_VendorCode]
    ON [dbo].[AdrJob]([VendorCode] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJob_VMAccountNumber]
    ON [dbo].[AdrJob]([VMAccountNumber] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJob_ModifiedDateTime]
    ON [dbo].[AdrJob]([ModifiedDateTime] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJob_IsDeleted_Status]
    ON [dbo].[AdrJob]([IsDeleted] ASC, [Status] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJob_IsDeleted_Status_BillingPeriodStartDateTime]
    ON [dbo].[AdrJob]([IsDeleted] ASC, [Status] ASC, [BillingPeriodStartDateTime] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJob_IsDeleted_AdrAccountId_BillingPeriodStartDateTime]
    ON [dbo].[AdrJob]([IsDeleted] ASC, [AdrAccountId] ASC, [BillingPeriodStartDateTime] DESC)
    INCLUDE([Status]);


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_AdrJob_Account_BillingPeriod]
    ON [dbo].[AdrJob]([AdrAccountId] ASC, [BillingPeriodStartDateTime] ASC, [BillingPeriodEndDateTime] ASC) WHERE ([IsDeleted]=(0));


GO
CREATE NONCLUSTERED INDEX [IX_AdrJob_IsManualRequest]
    ON [dbo].[AdrJob]([IsManualRequest] ASC) WHERE ([IsDeleted]=(0));


GO
CREATE NONCLUSTERED INDEX [IX_AdrJob_AdrAccountRuleId]
    ON [dbo].[AdrJob]([AdrAccountRuleId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJob_ScrapingCompletedDateTime]
    ON [dbo].[AdrJob]([ScrapingCompletedDateTime] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJob_IsDeleted_AdrAccountId_ScrapingCompletedDateTime]
    ON [dbo].[AdrJob]([IsDeleted] ASC, [AdrAccountId] ASC, [ScrapingCompletedDateTime] DESC)
    INCLUDE([Status]);

