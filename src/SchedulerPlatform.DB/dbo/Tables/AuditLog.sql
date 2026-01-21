CREATE TABLE [dbo].[AuditLog] (
    [AuditLogId]        INT            IDENTITY (1, 1) NOT NULL,
    [EventType]         NVARCHAR (100) NOT NULL,
    [EntityType]        NVARCHAR (100) NOT NULL,
    [EntityId]          INT            NULL,
    [Action]            NVARCHAR (50)  NOT NULL,
    [OldValues]         NVARCHAR (MAX) NULL,
    [NewValues]         NVARCHAR (MAX) NULL,
    [UserName]          NVARCHAR (200) NOT NULL,
    [ClientId]          INT            NULL,
    [IpAddress]         NVARCHAR (50)  NULL,
    [UserAgent]         NVARCHAR (500) NULL,
    [TimestampDateTime] DATETIME2 (7)  NOT NULL,
    [AdditionalData]    NVARCHAR (MAX) NULL,
    [CreatedDateTime]   DATETIME2 (7)  NOT NULL,
    [ModifiedDateTime]  DATETIME2 (7)  NOT NULL,
    [CreatedBy]         NVARCHAR (MAX) NOT NULL,
    [ModifiedBy]        NVARCHAR (MAX) NOT NULL,
    [IsDeleted]         BIT            NOT NULL,
    CONSTRAINT [PK_AuditLog] PRIMARY KEY CLUSTERED ([AuditLogId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_AuditLog_EntityType_EntityId]
    ON [dbo].[AuditLog]([EntityType] ASC, [EntityId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AuditLog_TimestampDateTime]
    ON [dbo].[AuditLog]([TimestampDateTime] ASC);

