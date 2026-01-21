CREATE TABLE [dbo].[UserPermission] (
    [UserPermissionId] INT            IDENTITY (1, 1) NOT NULL,
    [UserId]           INT            NOT NULL,
    [PermissionName]   NVARCHAR (100) NOT NULL,
    [ResourceType]     NVARCHAR (50)  NULL,
    [ResourceId]       INT            NULL,
    [CanCreate]        BIT            NOT NULL,
    [CanRead]          BIT            NOT NULL,
    [CanUpdate]        BIT            NOT NULL,
    [CanDelete]        BIT            NOT NULL,
    [CanExecute]       BIT            NOT NULL,
    [CreatedDateTime]  DATETIME2 (7)  NOT NULL,
    [ModifiedDateTime] DATETIME2 (7)  NOT NULL,
    [CreatedBy]        NVARCHAR (MAX) NOT NULL,
    [ModifiedBy]       NVARCHAR (MAX) NOT NULL,
    [IsDeleted]        BIT            NOT NULL,
    CONSTRAINT [PK_UserPermission] PRIMARY KEY CLUSTERED ([UserPermissionId] ASC),
    CONSTRAINT [FK_UserPermission_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([UserId]) ON DELETE CASCADE
);


GO
CREATE NONCLUSTERED INDEX [IX_UserPermission_UserId]
    ON [dbo].[UserPermission]([UserId] ASC);

