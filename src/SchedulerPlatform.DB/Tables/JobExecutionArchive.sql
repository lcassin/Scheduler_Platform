CREATE TABLE [dbo].[JobExecutionArchive] (
    [JobExecutionArchiveId]  INT            IDENTITY (1, 1) NOT NULL,
    [OriginalJobExecutionId] INT            NOT NULL,
    [ScheduleId]             INT            NOT NULL,
    [StartDateTime]          DATETIME2 (7)  NOT NULL,
    [EndDateTime]            DATETIME2 (7)  NULL,
    [Status]                 INT            NOT NULL,
    [Output]                 NVARCHAR (MAX) NULL,
    [ErrorMessage]           NVARCHAR (MAX) NULL,
    [StackTrace]             NVARCHAR (MAX) NULL,
    [RetryCount]             INT            DEFAULT ((0)) NOT NULL,
    [DurationSeconds]        INT            NULL,
    [TriggeredBy]            NVARCHAR (100) NULL,
    [CancelledBy]            NVARCHAR (100) NULL,
    [CreatedDateTime]        DATETIME2 (7)  NOT NULL,
    [CreatedBy]              NVARCHAR (200) NOT NULL,
    [ModifiedDateTime]       DATETIME2 (7)  NOT NULL,
    [ModifiedBy]             NVARCHAR (200) NOT NULL,
    [ArchivedDateTime]       DATETIME2 (7)  NOT NULL,
    [ArchivedBy]             NVARCHAR (200) NOT NULL,
    CONSTRAINT [PK_JobExecutionArchive] PRIMARY KEY CLUSTERED ([JobExecutionArchiveId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_JobExecutionArchive_OriginalJobExecutionId]
    ON [dbo].[JobExecutionArchive]([OriginalJobExecutionId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_JobExecutionArchive_ScheduleId]
    ON [dbo].[JobExecutionArchive]([ScheduleId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_JobExecutionArchive_ArchivedDateTime]
    ON [dbo].[JobExecutionArchive]([ArchivedDateTime] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_JobExecutionArchive_StartDateTime]
    ON [dbo].[JobExecutionArchive]([StartDateTime] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_JobExecutionArchive_Status]
    ON [dbo].[JobExecutionArchive]([Status] ASC);

