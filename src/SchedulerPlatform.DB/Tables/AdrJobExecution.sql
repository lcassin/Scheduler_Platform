CREATE TABLE [dbo].[AdrJobExecution] (
    [AdrJobExecutionId]    INT            IDENTITY (1, 1) NOT NULL,
    [AdrJobId]             INT            NOT NULL,
    [AdrRequestTypeId]     INT            NOT NULL,
    [StartDateTime]        DATETIME2 (7)  NOT NULL,
    [EndDateTime]          DATETIME2 (7)  NULL,
    [AdrStatusId]          INT            NULL,
    [AdrStatusDescription] NVARCHAR (100) NULL,
    [IsError]              BIT            NOT NULL,
    [IsFinal]              BIT            NOT NULL,
    [AdrIndexId]           BIGINT         NULL,
    [HttpStatusCode]       INT            NULL,
    [IsSuccess]            BIT            NOT NULL,
    [ErrorMessage]         NVARCHAR (MAX) NULL,
    [ApiResponse]          NVARCHAR (MAX) NULL,
    [RequestPayload]       NVARCHAR (MAX) NULL,
    [CreatedDateTime]      DATETIME2 (7)  NOT NULL,
    [ModifiedDateTime]     DATETIME2 (7)  NOT NULL,
    [CreatedBy]            NVARCHAR (MAX) NOT NULL,
    [ModifiedBy]           NVARCHAR (MAX) NOT NULL,
    [IsDeleted]            BIT            NOT NULL,
    CONSTRAINT [PK_AdrJobExecution] PRIMARY KEY CLUSTERED ([AdrJobExecutionId] ASC),
    CONSTRAINT [FK_AdrJobExecution_AdrJob_AdrJobId] FOREIGN KEY ([AdrJobId]) REFERENCES [dbo].[AdrJob] ([AdrJobId]) ON DELETE CASCADE
);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJobExecution_AdrJobId]
    ON [dbo].[AdrJobExecution]([AdrJobId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJobExecution_AdrRequestTypeId]
    ON [dbo].[AdrJobExecution]([AdrRequestTypeId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJobExecution_IsSuccess]
    ON [dbo].[AdrJobExecution]([IsSuccess] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJobExecution_StartDateTime]
    ON [dbo].[AdrJobExecution]([StartDateTime] ASC);

