CREATE TABLE [dbo].[PowerBiReport] (
    [PowerBiReportId]  INT            IDENTITY (1, 1) NOT NULL,
    [Name]             NVARCHAR (200) NOT NULL,
    [Url]              NVARCHAR (2000) NOT NULL,
    [Description]      NVARCHAR (500) NULL,
    [Category]         NVARCHAR (50)  DEFAULT ('ADR') NOT NULL,
    [DisplayOrder]     INT            DEFAULT ((0)) NOT NULL,
    [IsActive]         BIT            DEFAULT ((1)) NOT NULL,
    [OpenInNewTab]     BIT            DEFAULT ((1)) NOT NULL,
    [CreatedDateTime]  DATETIME2 (7)  DEFAULT (getutcdate()) NOT NULL,
    [CreatedBy]        NVARCHAR (200) NOT NULL,
    [ModifiedDateTime] DATETIME2 (7)  DEFAULT (getutcdate()) NOT NULL,
    [ModifiedBy]       NVARCHAR (200) NOT NULL,
    [IsDeleted]        BIT            DEFAULT ((0)) NOT NULL,
    CONSTRAINT [PK_PowerBiReport] PRIMARY KEY CLUSTERED ([PowerBiReportId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_PowerBiReport_Category]
    ON [dbo].[PowerBiReport]([Category] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_PowerBiReport_IsActive]
    ON [dbo].[PowerBiReport]([IsActive] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_PowerBiReport_Category_DisplayOrder]
    ON [dbo].[PowerBiReport]([Category] ASC, [DisplayOrder] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_PowerBiReport_IsDeleted_IsActive_Category_DisplayOrder]
    ON [dbo].[PowerBiReport]([IsDeleted] ASC, [IsActive] ASC, [Category] ASC, [DisplayOrder] ASC);


GO
-- Seed initial report
INSERT INTO [dbo].[PowerBiReport] ([Name], [Url], [Description], [Category], [DisplayOrder], [IsActive], [OpenInNewTab], [CreatedBy], [ModifiedBy])
VALUES (
    'Historical Load and Analytics',
    'https://app.powerbi.com/groups/me/reports/0881d2d5-3bfb-4f0f-bb3d-bec6fbb79434/d7368166922d4aa4d88c?ctid=08717c9a-7042-4ddf-b86a-e0a500d32cde&experience=power-bi',
    'ADR historical load and analytics report',
    'ADR',
    1,
    1,
    1,
    'System',
    'System'
);
