-- ============================================================================
-- Seed Production: Client Table
-- ============================================================================
-- Run this script FIRST - other tables depend on Client via foreign keys.
-- This script extracts Client data from UAT and inserts into Production.
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
    'SET IDENTITY_INSERT [dbo].[Client] ON;' AS [--SqlStatement]
UNION ALL
SELECT
    'IF NOT EXISTS (SELECT 1 FROM [dbo].[Client] WHERE [ClientId] = ' + CAST([ClientId] AS NVARCHAR(20)) + ')' + CHAR(13) + CHAR(10) +
    'BEGIN' + CHAR(13) + CHAR(10) +
    '    INSERT INTO [dbo].[Client] ([ClientId], [ExternalClientId], [ClientCode], [ClientName], [IsActive], [ContactEmail], [ContactPhone], [LastSyncedDateTime], [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted])' + CHAR(13) + CHAR(10) +
    '    VALUES (' +
        CAST([ClientId] AS NVARCHAR(20)) + ', ' +
        CAST([ExternalClientId] AS NVARCHAR(20)) + ', ' +
        'N''' + REPLACE([ClientCode], '''', '''''') + ''', ' +
        'N''' + REPLACE([ClientName], '''', '''''') + ''', ' +
        CAST([IsActive] AS NVARCHAR(1)) + ', ' +
        CASE WHEN [ContactEmail] IS NULL THEN 'NULL' ELSE 'N''' + REPLACE([ContactEmail], '''', '''''') + '''' END + ', ' +
        CASE WHEN [ContactPhone] IS NULL THEN 'NULL' ELSE 'N''' + REPLACE([ContactPhone], '''', '''''') + '''' END + ', ' +
        'NULL, ' + -- Reset LastSyncedDateTime for production (will be set on first sync)
        'GETUTCDATE(), ' +
        'GETUTCDATE(), ' +
        'N''ProductionSeed'', ' +
        'N''ProductionSeed'', ' +
        '0' +
    ');' + CHAR(13) + CHAR(10) +
    'END'
FROM [dbo].[Client]
WHERE [IsDeleted] = 0
ORDER BY [ClientId]
UNION ALL
SELECT
    'SET IDENTITY_INSERT [dbo].[Client] OFF;'

SET NOCOUNT OFF;

-- ============================================================================
-- NOTE: The generated output will look like this (example):
-- ============================================================================
-- SET IDENTITY_INSERT [dbo].[Client] ON;
-- IF NOT EXISTS (SELECT 1 FROM [dbo].[Client] WHERE [ClientId] = 1)
-- BEGIN
--     INSERT INTO [dbo].[Client] ([ClientId], [ExternalClientId], ...)
--     VALUES (1, 100, N'CASS', N'Cass Information Systems (Internal Client)', ...);
-- END
-- SET IDENTITY_INSERT [dbo].[Client] OFF;
-- ============================================================================
