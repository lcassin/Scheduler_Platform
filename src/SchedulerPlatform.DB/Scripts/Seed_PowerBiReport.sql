-- Seed initial Power BI report data
-- Run this script after deploying the PowerBiReport table schema

-- Check if the report already exists before inserting
IF NOT EXISTS (SELECT 1 FROM [dbo].[PowerBiReport] WHERE [Name] = 'Historical Load and Analytics')
BEGIN
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
    PRINT 'Inserted Historical Load and Analytics report';
END
ELSE
BEGIN
    PRINT 'Historical Load and Analytics report already exists - skipping insert';
END
