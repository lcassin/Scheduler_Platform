CREATE TABLE [dbo].[AdrJobArchive] (
    [AdrJobArchiveId]            INT            IDENTITY (1, 1) NOT NULL,
    [OriginalAdrJobId]           INT            NOT NULL,
    [AdrAccountId]               INT            NOT NULL,
    [AdrAccountRuleId]           INT            NULL,
    [VMAccountId]                BIGINT         NOT NULL,
    [VMAccountNumber]            NVARCHAR (128) NOT NULL,
    [VendorCode]                 NVARCHAR (128) NULL,
    [CredentialId]               INT            NOT NULL,
    [PeriodType]                 NVARCHAR (13)  NULL,
    [BillingPeriodStartDateTime] DATETIME2 (7)  NOT NULL,
    [BillingPeriodEndDateTime]   DATETIME2 (7)  NOT NULL,
    [NextRunDateTime]            DATETIME2 (7)  NULL,
    [NextRangeStartDateTime]     DATETIME2 (7)  NULL,
    [NextRangeEndDateTime]       DATETIME2 (7)  NULL,
    [Status]                     NVARCHAR (50)  NOT NULL,
    [IsMissing]                  BIT            DEFAULT ((0)) NOT NULL,
    [AdrStatusId]                INT            NULL,
    [AdrStatusDescription]       NVARCHAR (100) NULL,
    [AdrIndexId]                 BIGINT         NULL,
    [CredentialVerifiedDateTime] DATETIME2 (7)  NULL,
    [ScrapingCompletedDateTime]  DATETIME2 (7)  NULL,
    [ErrorMessage]               NVARCHAR (MAX) NULL,
    [RetryCount]                 INT            DEFAULT ((0)) NOT NULL,
    [IsManualRequest]            BIT            DEFAULT ((0)) NOT NULL,
    [ManualRequestReason]        NVARCHAR (MAX) NULL,
    [LastStatusCheckResponse]    NVARCHAR (MAX) NULL,
    [LastStatusCheckDateTime]    DATETIME2 (7)  NULL,
    [CreatedDateTime]            DATETIME2 (7)  NOT NULL,
    [CreatedBy]                  NVARCHAR (200) NOT NULL,
    [ModifiedDateTime]           DATETIME2 (7)  NOT NULL,
    [ModifiedBy]                 NVARCHAR (200) NOT NULL,
    [ArchivedDateTime]           DATETIME2 (7)  NOT NULL,
    [ArchivedBy]                 NVARCHAR (200) NOT NULL,
    CONSTRAINT [PK_AdrJobArchive] PRIMARY KEY CLUSTERED ([AdrJobArchiveId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJobArchive_OriginalAdrJobId]
    ON [dbo].[AdrJobArchive]([OriginalAdrJobId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJobArchive_AdrAccountId]
    ON [dbo].[AdrJobArchive]([AdrAccountId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJobArchive_VMAccountId]
    ON [dbo].[AdrJobArchive]([VMAccountId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJobArchive_ArchivedDateTime]
    ON [dbo].[AdrJobArchive]([ArchivedDateTime] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJobArchive_BillingPeriodStartDateTime]
    ON [dbo].[AdrJobArchive]([BillingPeriodStartDateTime] ASC);

