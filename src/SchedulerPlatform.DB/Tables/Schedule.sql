CREATE TABLE [dbo].[Schedule] (
    [ScheduleId]        INT             IDENTITY (1, 1) NOT NULL,
    [Name]              NVARCHAR (200)  NOT NULL,
    [Description]       NVARCHAR (1000) NOT NULL,
    [ClientId]          BIGINT          NOT NULL,
    [JobType]           INT             NOT NULL,
    [Frequency]         INT             NOT NULL,
    [CronExpression]    NVARCHAR (100)  NOT NULL,
    [NextRunDateTime]   DATETIME2 (7)   NULL,
    [LastRunDateTime]   DATETIME2 (7)   NULL,
    [IsEnabled]         BIT             NOT NULL,
    [MaxRetries]        INT             NOT NULL,
    [RetryDelayMinutes] INT             NOT NULL,
    [TimeoutMinutes]    INT             NULL,
    [TimeZone]          NVARCHAR (100)  NULL,
    [JobConfiguration]  NVARCHAR (MAX)  NULL,
    [CreatedDateTime]   DATETIME2 (7)   NOT NULL,
    [ModifiedDateTime]  DATETIME2 (7)   NOT NULL,
    [CreatedBy]         NVARCHAR (MAX)  NOT NULL,
    [ModifiedBy]        NVARCHAR (MAX)  NOT NULL,
    [IsDeleted]         BIT             NOT NULL,
    [IsSystemSchedule]  BIT             CONSTRAINT [DF__Schedule__IsSyst__01142BA1] DEFAULT ((0)) NOT NULL,
    CONSTRAINT [PK_Schedule] PRIMARY KEY CLUSTERED ([ScheduleId] ASC),
    CONSTRAINT [FK_Schedule_Client_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [dbo].[Client] ([ClientId])
);


GO
CREATE NONCLUSTERED INDEX [IX_Schedule_ClientId]
    ON [dbo].[Schedule]([ClientId] ASC);

