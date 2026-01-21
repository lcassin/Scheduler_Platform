CREATE TABLE [dbo].[AdrJobType] (
    [AdrJobTypeId]     INT            IDENTITY (1, 1) NOT NULL,
    [Code]             NVARCHAR (50)  NOT NULL,
    [Name]             NVARCHAR (100) NOT NULL,
    [Description]      NVARCHAR (500) NULL,
    [EndpointUrl]      NVARCHAR (500) NULL,
    [AdrRequestTypeId] INT            NOT NULL,
    [IsActive]         BIT            DEFAULT ((1)) NOT NULL,
    [DisplayOrder]     INT            DEFAULT ((0)) NOT NULL,
    [Notes]            NVARCHAR (MAX) NULL,
    [CreatedDateTime]  DATETIME2 (7)  DEFAULT (getutcdate()) NOT NULL,
    [CreatedBy]        NVARCHAR (200) NULL,
    [ModifiedDateTime] DATETIME2 (7)  DEFAULT (getutcdate()) NOT NULL,
    [ModifiedBy]       NVARCHAR (200) NULL,
    [IsDeleted]        BIT            DEFAULT ((0)) NOT NULL,
    CONSTRAINT [PK_AdrJobType] PRIMARY KEY CLUSTERED ([AdrJobTypeId] ASC)
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_AdrJobType_Code]
    ON [dbo].[AdrJobType]([Code] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJobType_IsActive]
    ON [dbo].[AdrJobType]([IsActive] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_AdrJobType_AdrRequestTypeId]
    ON [dbo].[AdrJobType]([AdrRequestTypeId] ASC);

