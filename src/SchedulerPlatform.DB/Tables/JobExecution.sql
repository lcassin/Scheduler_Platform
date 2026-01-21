CREATE TABLE [dbo].[JobExecution] (
    [JobExecutionId]   INT            IDENTITY (1, 1) NOT NULL,
    [ScheduleId]       INT            NOT NULL,
    [StartDateTime]    DATETIME2 (7)  NOT NULL,
    [EndDateTime]      DATETIME2 (7)  NULL,
    [Status]           INT            NOT NULL,
    [Output]           NVARCHAR (MAX) NULL,
    [ErrorMessage]     NVARCHAR (MAX) NULL,
    [StackTrace]       NVARCHAR (MAX) NULL,
    [RetryCount]       INT            NOT NULL,
    [DurationSeconds]  INT            NULL,
    [TriggeredBy]      NVARCHAR (100) NULL,
    [CancelledBy]      NVARCHAR (100) NULL,
    [CreatedDateTime]  DATETIME2 (7)  NOT NULL,
    [ModifiedDateTime] DATETIME2 (7)  NOT NULL,
    [CreatedBy]        NVARCHAR (MAX) NOT NULL,
    [ModifiedBy]       NVARCHAR (MAX) NOT NULL,
    [IsDeleted]        BIT            NOT NULL,
    CONSTRAINT [PK_JobExecution] PRIMARY KEY CLUSTERED ([JobExecutionId] ASC),
    CONSTRAINT [FK_JobExecution_Schedule_ScheduleId] FOREIGN KEY ([ScheduleId]) REFERENCES [dbo].[Schedule] ([ScheduleId]) ON DELETE CASCADE
);


GO
CREATE NONCLUSTERED INDEX [IX_JobExecution_ScheduleId]
    ON [dbo].[JobExecution]([ScheduleId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_JobExecution_StartDateTime]
    ON [dbo].[JobExecution]([StartDateTime] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_JobExecution_Status]
    ON [dbo].[JobExecution]([Status] ASC);

