CREATE TABLE [dbo].[JobParameter] (
    [JobParameterId]         INT            IDENTITY (1, 1) NOT NULL,
    [ScheduleId]             INT            NOT NULL,
    [ParameterName]          NVARCHAR (100) NOT NULL,
    [ParameterType]          NVARCHAR (50)  NOT NULL,
    [ParameterValue]         NVARCHAR (MAX) NULL,
    [SourceQuery]            NVARCHAR (MAX) NULL,
    [SourceConnectionString] NVARCHAR (500) NULL,
    [IsDynamic]              BIT            NOT NULL,
    [DisplayOrder]           INT            NOT NULL,
    [CreatedDateTime]        DATETIME2 (7)  NOT NULL,
    [ModifiedDateTime]       DATETIME2 (7)  NOT NULL,
    [CreatedBy]              NVARCHAR (MAX) NOT NULL,
    [ModifiedBy]             NVARCHAR (MAX) NOT NULL,
    [IsDeleted]              BIT            NOT NULL,
    CONSTRAINT [PK_JobParameter] PRIMARY KEY CLUSTERED ([JobParameterId] ASC),
    CONSTRAINT [FK_JobParameter_Schedule_ScheduleId] FOREIGN KEY ([ScheduleId]) REFERENCES [dbo].[Schedule] ([ScheduleId]) ON DELETE CASCADE
);


GO
CREATE NONCLUSTERED INDEX [IX_JobParameter_ScheduleId]
    ON [dbo].[JobParameter]([ScheduleId] ASC);

