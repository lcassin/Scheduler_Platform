CREATE TABLE [dbo].[PasswordHistory] (
    [PasswordHistoryId] INT            IDENTITY (1, 1) NOT NULL,
    [UserId]            INT            NOT NULL,
    [PasswordHash]      NVARCHAR (500) NOT NULL,
    [ChangedDateTime]   DATETIME2 (7)  NOT NULL,
    [CreatedDateTime]   DATETIME2 (7)  NOT NULL,
    [ModifiedDateTime]  DATETIME2 (7)  NOT NULL,
    [CreatedBy]         NVARCHAR (MAX) NOT NULL,
    [ModifiedBy]        NVARCHAR (MAX) NOT NULL,
    [IsDeleted]         BIT            NOT NULL,
    CONSTRAINT [PK_PasswordHistory] PRIMARY KEY CLUSTERED ([PasswordHistoryId] ASC),
    CONSTRAINT [FK_PasswordHistory_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([UserId]) ON DELETE CASCADE
);


GO
CREATE NONCLUSTERED INDEX [IX_PasswordHistory_UserId_ChangedDateTime]
    ON [dbo].[PasswordHistory]([UserId] ASC, [ChangedDateTime] ASC);

