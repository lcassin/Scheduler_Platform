CREATE TABLE [dbo].[AdrAccountBlacklist] (
    [AdrAccountBlacklistId] INT            IDENTITY (1, 1) NOT NULL,
    [VendorCode]            NVARCHAR (128) NULL,
    [VMAccountId]           BIGINT         NULL,
    [VMAccountNumber]       NVARCHAR (128) NULL,
    [CredentialId]          INT            NULL,
    [ExclusionType]         NVARCHAR (20)  DEFAULT ('All') NOT NULL,
    [Reason]                NVARCHAR (500) NOT NULL,
    [IsActive]              BIT            DEFAULT ((1)) NOT NULL,
    [EffectiveStartDate]    DATETIME2 (7)  NULL,
    [EffectiveEndDate]      DATETIME2 (7)  NULL,
    [BlacklistedBy]         NVARCHAR (200) NULL,
    [BlacklistedDateTime]   DATETIME2 (7)  NULL,
    [Notes]                 NVARCHAR (MAX) NULL,
    [CreatedDateTime]       DATETIME2 (7)  DEFAULT (getutcdate()) NOT NULL,
    [CreatedBy]             NVARCHAR (200) DEFAULT ('System') NOT NULL,
    [ModifiedDateTime]      DATETIME2 (7)  DEFAULT (getutcdate()) NOT NULL,
    [ModifiedBy]            NVARCHAR (200) DEFAULT ('System') NOT NULL,
    [IsDeleted]             BIT            DEFAULT ((0)) NOT NULL,
    CONSTRAINT [PK_AdrAccountBlacklist] PRIMARY KEY CLUSTERED ([AdrAccountBlacklistId] ASC),
    CONSTRAINT [CK_AdrAccountBlacklist_ExclusionType] CHECK ([ExclusionType]='Download' OR [ExclusionType]='CredentialCheck' OR [ExclusionType]='All')
);


GO
CREATE NONCLUSTERED INDEX [IX_AdrAccountBlacklist_VendorCode]
    ON [dbo].[AdrAccountBlacklist]([VendorCode] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrAccountBlacklist_VMAccountId]
    ON [dbo].[AdrAccountBlacklist]([VMAccountId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrAccountBlacklist_VMAccountNumber]
    ON [dbo].[AdrAccountBlacklist]([VMAccountNumber] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrAccountBlacklist_CredentialId]
    ON [dbo].[AdrAccountBlacklist]([CredentialId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrAccountBlacklist_IsActive]
    ON [dbo].[AdrAccountBlacklist]([IsActive] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrAccountBlacklist_IsDeleted_IsActive]
    ON [dbo].[AdrAccountBlacklist]([IsDeleted] ASC, [IsActive] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrAccountBlacklist_VendorCode_VMAccountId_CredentialId]
    ON [dbo].[AdrAccountBlacklist]([VendorCode] ASC, [VMAccountId] ASC, [CredentialId] ASC);

