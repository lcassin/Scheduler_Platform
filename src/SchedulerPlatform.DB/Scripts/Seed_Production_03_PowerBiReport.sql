-- ============================================================================
-- Seed Production: PowerBiReport Table
-- ============================================================================
-- Run this AFTER Client seeding (no FK dependency, but keeps order logical).
-- Seeds Power BI report links into Production.
-- ============================================================================
-- INSTRUCTIONS:
-- 1. Run the SELECT query below against your UAT database to generate
--    INSERT statements.
-- 2. Copy the generated INSERT statements.
-- 3. Run the generated INSERT statements against your Production database.
-- ============================================================================

-- ============================================================================
-- STEP 1: Run this against UAT to generate INSERT statements
-- ============================================================================

SET NOCOUNT ON;

SELECT
    'SET IDENTITY_INSERT [dbo].[PowerBiReport] ON;' AS [--SqlStatement]
UNION ALL
SELECT
    'IF NOT EXISTS (SELECT 1 FROM [dbo].[PowerBiReport] WHERE [PowerBiReportId] = ' + CAST([PowerBiReportId] AS NVARCHAR(20)) + ')' + CHAR(13) + CHAR(10) +
    'BEGIN' + CHAR(13) + CHAR(10) +
    '    INSERT INTO [dbo].[PowerBiReport] ([PowerBiReportId], [Name], [Url], [Description], [Category], [DisplayOrder], [IsActive], [OpenInNewTab], [CreatedDateTime], [CreatedBy], [ModifiedDateTime], [ModifiedBy], [IsDeleted])' + CHAR(13) + CHAR(10) +
    '    VALUES (' +
        CAST([PowerBiReportId] AS NVARCHAR(20)) + ', ' +
        'N''' + REPLACE([Name], '''', '''''') + ''', ' +
        'N''' + REPLACE([Url], '''', '''''') + ''', ' +
        CASE WHEN [Description] IS NULL THEN 'NULL' ELSE 'N''' + REPLACE([Description], '''', '''''') + '''' END + ', ' +
        'N''' + REPLACE([Category], '''', '''''') + ''', ' +
        CAST([DisplayOrder] AS NVARCHAR(10)) + ', ' +
        CAST([IsActive] AS NVARCHAR(1)) + ', ' +
        CAST([OpenInNewTab] AS NVARCHAR(1)) + ', ' +
        'GETUTCDATE(), ' +
        'N''ProductionSeed'', ' +
        'GETUTCDATE(), ' +
        'N''ProductionSeed'', ' +
        '0' +
    ');' + CHAR(13) + CHAR(10) +
    'END'
FROM [dbo].[PowerBiReport]
WHERE [IsDeleted] = 0
ORDER BY [DisplayOrder]
UNION ALL
SELECT
    'SET IDENTITY_INSERT [dbo].[PowerBiReport] OFF;'

SET NOCOUNT OFF;
