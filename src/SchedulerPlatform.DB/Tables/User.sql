CREATE TABLE [dbo].[User] (
    [UserId]                  INT            IDENTITY (1, 1) NOT NULL,
    [Username]                NVARCHAR (100) NOT NULL,
    [Email]                   NVARCHAR (255) NOT NULL,
    [FirstName]               NVARCHAR (100) NOT NULL,
    [LastName]                NVARCHAR (100) NOT NULL,
    [ClientId]                BIGINT         NOT NULL,
    [IsActive]                BIT            NOT NULL,
    [ExternalUserId]          NVARCHAR (255) NULL,
    [ExternalIssuer]          NVARCHAR (500) NULL,
    [PasswordHash]            NVARCHAR (500) NULL,
    [IsSystemAdmin]           BIT            NOT NULL,
    [LastLoginDateTime]       DATETIME2 (7)  NULL,
    [MustChangePassword]      BIT            NOT NULL,
    [PasswordChangedDateTime] DATETIME2 (7)  NULL,
    [CreatedDateTime]         DATETIME2 (7)  NOT NULL,
    [ModifiedDateTime]        DATETIME2 (7)  NOT NULL,
    [CreatedBy]               NVARCHAR (MAX) NOT NULL,
    [ModifiedBy]              NVARCHAR (MAX) NOT NULL,
    [IsDeleted]               BIT            NOT NULL,
    CONSTRAINT [PK_User] PRIMARY KEY CLUSTERED ([UserId] ASC),
    CONSTRAINT [FK_User_Client_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Client] ([ClientId])
);


GO
CREATE NONCLUSTERED INDEX [IX_User_ClientId]
    ON [dbo].[User]([ClientId] ASC);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_User_Email]
    ON [dbo].[User]([Email] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_User_ExternalIssuer_ExternalUserId]
    ON [dbo].[User]([ExternalIssuer] ASC, [ExternalUserId] ASC);

