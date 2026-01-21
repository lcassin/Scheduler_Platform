CREATE TABLE [dbo].[AdrAccountRule] (
    [AdrAccountRuleId]       INT            IDENTITY (1, 1) NOT NULL,
    [AdrAccountId]           INT            NOT NULL,
    [JobTypeId]              INT            NOT NULL,
    [RuleName]               NVARCHAR (200) NOT NULL,
    [PeriodType]             NVARCHAR (13)  NULL,
    [PeriodDays]             INT            NULL,
    [DayOfMonth]             INT            NULL,
    [NextRunDateTime]        DATETIME2 (7)  NULL,
    [NextRangeStartDateTime] DATETIME2 (7)  NULL,
    [NextRangeEndDateTime]   DATETIME2 (7)  NULL,
    [WindowDaysBefore]       INT            NULL,
    [WindowDaysAfter]        INT            NULL,
    [IsEnabled]              BIT            DEFAULT ((1)) NOT NULL,
    [Priority]               INT            DEFAULT ((100)) NOT NULL,
    [IsManuallyOverridden]   BIT            DEFAULT ((0)) NOT NULL,
    [OverriddenBy]           NVARCHAR (200) NULL,
    [OverriddenDateTime]     DATETIME2 (7)  NULL,
    [Notes]                  NVARCHAR (MAX) NULL,
    [CreatedDateTime]        DATETIME2 (7)  DEFAULT (getutcdate()) NOT NULL,
    [CreatedBy]              NVARCHAR (200) DEFAULT ('System') NOT NULL,
    [ModifiedDateTime]       DATETIME2 (7)  DEFAULT (getutcdate()) NOT NULL,
    [ModifiedBy]             NVARCHAR (200) DEFAULT ('System') NOT NULL,
    [IsDeleted]              BIT            DEFAULT ((0)) NOT NULL,
    CONSTRAINT [PK_AdrAccountRule] PRIMARY KEY CLUSTERED ([AdrAccountRuleId] ASC),
    CONSTRAINT [FK_AdrAccountRule_AdrAccount] FOREIGN KEY ([AdrAccountId]) REFERENCES [dbo].[AdrAccount] ([AdrAccountId]) ON DELETE CASCADE,
    CONSTRAINT [FK_AdrAccountRule_AdrJobType] FOREIGN KEY ([JobTypeId]) REFERENCES [dbo].[AdrJobType] ([AdrJobTypeId])
);


GO
CREATE NONCLUSTERED INDEX [IX_AdrAccountRule_AdrAccountId]
    ON [dbo].[AdrAccountRule]([AdrAccountId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrAccountRule_JobTypeId]
    ON [dbo].[AdrAccountRule]([JobTypeId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrAccountRule_IsEnabled]
    ON [dbo].[AdrAccountRule]([IsEnabled] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrAccountRule_NextRunDateTime]
    ON [dbo].[AdrAccountRule]([NextRunDateTime] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrAccountRule_AdrAccountId_JobTypeId]
    ON [dbo].[AdrAccountRule]([AdrAccountId] ASC, [JobTypeId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrAccountRule_IsDeleted_IsEnabled_NextRunDateTime]
    ON [dbo].[AdrAccountRule]([IsDeleted] ASC, [IsEnabled] ASC, [NextRunDateTime] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrAccountRule_IsDeleted_JobTypeId_AdrAccountId]
    ON [dbo].[AdrAccountRule]([IsDeleted] ASC, [JobTypeId] ASC, [AdrAccountId] ASC);

