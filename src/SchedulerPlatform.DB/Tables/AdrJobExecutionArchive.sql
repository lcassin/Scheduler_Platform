CREATE TABLE [dbo].[AdrJobExecutionArchive] (
    [AdrJobExecutionArchiveId]  INT            IDENTITY (1, 1) NOT NULL,
    [OriginalAdrJobExecutionId] INT            NOT NULL,
    [AdrJobId]                  INT            NOT NULL,
    [AdrRequestTypeId]          INT            NOT NULL,
    [StartDateTime]             DATETIME2 (7)  NOT NULL,
    [EndDateTime]               DATETIME2 (7)  NULL,
    [AdrStatusId]               INT            NULL,
    [AdrStatusDescription]      NVARCHAR (100) NULL,
    [IsError]                   BIT            DEFAULT ((0)) NOT NULL,
    [IsFinal]                   BIT            DEFAULT ((0)) NOT NULL,
    [AdrIndexId]                BIGINT         NULL,
    [HttpStatusCode]            INT            NULL,
    [IsSuccess]                 BIT            DEFAULT ((0)) NOT NULL,
    [ErrorMessage]              NVARCHAR (MAX) NULL,
    [ApiResponse]               NVARCHAR (MAX) NULL,
    [RequestPayload]            NVARCHAR (MAX) NULL,
    [CreatedDateTime]           DATETIME2 (7)  NOT NULL,
    [CreatedBy]                 NVARCHAR (200) NOT NULL,
    [ModifiedDateTime]          DATETIME2 (7)  NOT NULL,
    [ModifiedBy]                NVARCHAR (200) NOT NULL,
    [ArchivedDateTime]          DATETIME2 (7)  NOT NULL,
    [ArchivedBy]                NVARCHAR (200) NOT NULL,
    CONSTRAINT [PK_AdrJobExecutionArchive] PRIMARY KEY CLUSTERED ([AdrJobExecutionArchiveId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJobExecutionArchive_OriginalAdrJobExecutionId]
    ON [dbo].[AdrJobExecutionArchive]([OriginalAdrJobExecutionId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJobExecutionArchive_AdrJobId]
    ON [dbo].[AdrJobExecutionArchive]([AdrJobId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJobExecutionArchive_ArchivedDateTime]
    ON [dbo].[AdrJobExecutionArchive]([ArchivedDateTime] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJobExecutionArchive_StartDateTime]
    ON [dbo].[AdrJobExecutionArchive]([StartDateTime] ASC);

