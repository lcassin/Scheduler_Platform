CREATE TABLE [dbo].[AuditLogArchive] (
    [AuditLogArchiveId]  INT            IDENTITY (1, 1) NOT NULL,
    [OriginalAuditLogId] INT            NOT NULL,
    [EventType]          NVARCHAR (100) NOT NULL,
    [EntityType]         NVARCHAR (100) NOT NULL,
    [EntityId]           INT            NULL,
    [Action]             NVARCHAR (50)  NOT NULL,
    [OldValues]          NVARCHAR (MAX) NULL,
    [NewValues]          NVARCHAR (MAX) NULL,
    [UserName]           NVARCHAR (200) NOT NULL,
    [ClientId]           INT            NULL,
    [IpAddress]          NVARCHAR (50)  NULL,
    [UserAgent]          NVARCHAR (500) NULL,
    [TimestampDateTime]  DATETIME2 (7)  NOT NULL,
    [AdditionalData]     NVARCHAR (MAX) NULL,
    [CreatedDateTime]    DATETIME2 (7)  NOT NULL,
    [CreatedBy]          NVARCHAR (200) NOT NULL,
    [ModifiedDateTime]   DATETIME2 (7)  NOT NULL,
    [ModifiedBy]         NVARCHAR (200) NOT NULL,
    [ArchivedDateTime]   DATETIME2 (7)  NOT NULL,
    [ArchivedBy]         NVARCHAR (200) NOT NULL,
    CONSTRAINT [PK_AuditLogArchive] PRIMARY KEY CLUSTERED ([AuditLogArchiveId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_AuditLogArchive_OriginalAuditLogId]
    ON [dbo].[AuditLogArchive]([OriginalAuditLogId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AuditLogArchive_ArchivedDateTime]
    ON [dbo].[AuditLogArchive]([ArchivedDateTime] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AuditLogArchive_TimestampDateTime]
    ON [dbo].[AuditLogArchive]([TimestampDateTime] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AuditLogArchive_EntityType_EntityId]
    ON [dbo].[AuditLogArchive]([EntityType] ASC, [EntityId] ASC);

