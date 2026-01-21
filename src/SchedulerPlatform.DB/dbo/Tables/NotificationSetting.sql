CREATE TABLE [dbo].[NotificationSetting] (
    [NotificationSettingId]      INT             IDENTITY (1, 1) NOT NULL,
    [ScheduleId]                 INT             NOT NULL,
    [EnableSuccessNotifications] BIT             NOT NULL,
    [EnableFailureNotifications] BIT             NOT NULL,
    [SuccessEmailRecipients]     NVARCHAR (1000) NULL,
    [FailureEmailRecipients]     NVARCHAR (1000) NULL,
    [SuccessEmailSubject]        NVARCHAR (500)  NULL,
    [FailureEmailSubject]        NVARCHAR (500)  NULL,
    [IncludeExecutionDetails]    BIT             NOT NULL,
    [IncludeOutput]              BIT             NOT NULL,
    [CreatedDateTime]            DATETIME2 (7)   NOT NULL,
    [ModifiedDateTime]           DATETIME2 (7)   NOT NULL,
    [CreatedBy]                  NVARCHAR (MAX)  NOT NULL,
    [ModifiedBy]                 NVARCHAR (MAX)  NOT NULL,
    [IsDeleted]                  BIT             NOT NULL,
    CONSTRAINT [PK_NotificationSetting] PRIMARY KEY CLUSTERED ([NotificationSettingId] ASC),
    CONSTRAINT [FK_NotificationSetting_Schedule_ScheduleId] FOREIGN KEY ([ScheduleId]) REFERENCES [dbo].[Schedule] ([ScheduleId]) ON DELETE CASCADE
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_NotificationSetting_ScheduleId]
    ON [dbo].[NotificationSetting]([ScheduleId] ASC);

