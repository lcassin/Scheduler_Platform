CREATE TABLE [dbo].[Client] (
    [ClientId]           BIGINT         IDENTITY (1, 1) NOT NULL,
    [ExternalClientId]   INT            NOT NULL,
    [ClientCode]         NVARCHAR (MAX) NOT NULL,
    [ClientName]         NVARCHAR (200) NOT NULL,
    [IsActive]           BIT            NOT NULL,
    [ContactEmail]       NVARCHAR (255) NULL,
    [ContactPhone]       NVARCHAR (50)  NULL,
    [LastSyncedDateTime] DATETIME2 (7)  NULL,
    [CreatedDateTime]    DATETIME2 (7)  NOT NULL,
    [ModifiedDateTime]   DATETIME2 (7)  NOT NULL,
    [CreatedBy]          NVARCHAR (MAX) NOT NULL,
    [ModifiedBy]         NVARCHAR (MAX) NOT NULL,
    [IsDeleted]          BIT            NOT NULL,
    CONSTRAINT [PK_Client] PRIMARY KEY CLUSTERED ([ClientId] ASC)
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_Client_ExternalClientId]
    ON [dbo].[Client]([ExternalClientId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_Client_LastSyncedDateTime]
    ON [dbo].[Client]([LastSyncedDateTime] ASC);

