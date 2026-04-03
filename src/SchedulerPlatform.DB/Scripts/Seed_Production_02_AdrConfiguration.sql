-- ============================================================================
-- Seed Production: AdrConfiguration Table
-- ============================================================================
-- Run this AFTER Client seeding. AdrConfiguration is a standalone config
-- table (typically a single row) that controls orchestration behavior.
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
    'SET IDENTITY_INSERT [dbo].[AdrConfiguration] ON;' AS [--SqlStatement]
UNION ALL
SELECT
    'IF NOT EXISTS (SELECT 1 FROM [dbo].[AdrConfiguration] WHERE [AdrConfigurationId] = ' + CAST([AdrConfigurationId] AS NVARCHAR(20)) + ')' + CHAR(13) + CHAR(10) +
    'BEGIN' + CHAR(13) + CHAR(10) +
    '    INSERT INTO [dbo].[AdrConfiguration] (' + CHAR(13) + CHAR(10) +
    '        [AdrConfigurationId], [CredentialCheckLeadDays], [ScrapeRetryDays], [MaxRetries],' + CHAR(13) + CHAR(10) +
    '        [FinalStatusCheckDelayDays], [DailyStatusCheckDelayDays], [MaxParallelRequests], [BatchSize],' + CHAR(13) + CHAR(10) +
    '        [DefaultWindowDaysBefore], [DefaultWindowDaysAfter], [AutoCreateTestLoginRules], [AutoCreateMissingInvoiceAlerts],' + CHAR(13) + CHAR(10) +
    '        [MissingInvoiceAlertEmail], [IsOrchestrationEnabled], [Notes],' + CHAR(13) + CHAR(10) +
    '        [CreatedDateTime], [CreatedBy], [ModifiedDateTime], [ModifiedBy], [IsDeleted],' + CHAR(13) + CHAR(10) +
    '        [JobRetentionMonths], [JobExecutionRetentionMonths], [AuditLogRetentionDays], [IsArchivalEnabled],' + CHAR(13) + CHAR(10) +
    '        [ArchivalBatchSize], [ArchiveRetentionYears], [LogRetentionDays], [MaxOrchestrationDurationMinutes], [DatabaseCommandTimeoutSeconds]' + CHAR(13) + CHAR(10) +
    '    )' + CHAR(13) + CHAR(10) +
    '    VALUES (' +
        CAST([AdrConfigurationId] AS NVARCHAR(20)) + ', ' +
        CAST([CredentialCheckLeadDays] AS NVARCHAR(10)) + ', ' +
        CAST([ScrapeRetryDays] AS NVARCHAR(10)) + ', ' +
        CAST([MaxRetries] AS NVARCHAR(10)) + ', ' +
        CAST([FinalStatusCheckDelayDays] AS NVARCHAR(10)) + ', ' +
        CAST([DailyStatusCheckDelayDays] AS NVARCHAR(10)) + ', ' +
        CAST([MaxParallelRequests] AS NVARCHAR(10)) + ', ' +
        CAST([BatchSize] AS NVARCHAR(10)) + ', ' +
        CAST([DefaultWindowDaysBefore] AS NVARCHAR(10)) + ', ' +
        CAST([DefaultWindowDaysAfter] AS NVARCHAR(10)) + ', ' +
        CAST([AutoCreateTestLoginRules] AS NVARCHAR(1)) + ', ' +
        CAST([AutoCreateMissingInvoiceAlerts] AS NVARCHAR(1)) + ', ' +
        CASE WHEN [MissingInvoiceAlertEmail] IS NULL THEN 'NULL' ELSE 'N''' + REPLACE([MissingInvoiceAlertEmail], '''', '''''') + '''' END + ', ' +
        CAST([IsOrchestrationEnabled] AS NVARCHAR(1)) + ', ' +
        CASE WHEN [Notes] IS NULL THEN 'NULL' ELSE 'N''' + REPLACE([Notes], '''', '''''') + '''' END + ', ' +
        'GETUTCDATE(), ' +
        'N''ProductionSeed'', ' +
        'GETUTCDATE(), ' +
        'N''ProductionSeed'', ' +
        CAST([IsDeleted] AS NVARCHAR(1)) + ', ' +
        CAST([JobRetentionMonths] AS NVARCHAR(10)) + ', ' +
        CAST([JobExecutionRetentionMonths] AS NVARCHAR(10)) + ', ' +
        CAST([AuditLogRetentionDays] AS NVARCHAR(10)) + ', ' +
        CAST([IsArchivalEnabled] AS NVARCHAR(1)) + ', ' +
        CAST([ArchivalBatchSize] AS NVARCHAR(10)) + ', ' +
        CAST([ArchiveRetentionYears] AS NVARCHAR(10)) + ', ' +
        CAST([LogRetentionDays] AS NVARCHAR(10)) + ', ' +
        CAST([MaxOrchestrationDurationMinutes] AS NVARCHAR(10)) + ', ' +
        CAST([DatabaseCommandTimeoutSeconds] AS NVARCHAR(10)) +
    ');' + CHAR(13) + CHAR(10) +
    'END'
FROM [dbo].[AdrConfiguration]
WHERE [IsDeleted] = 0
UNION ALL
SELECT
    'SET IDENTITY_INSERT [dbo].[AdrConfiguration] OFF;'

SET NOCOUNT OFF;
