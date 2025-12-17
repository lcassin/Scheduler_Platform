-- ============================================================================
-- Script: 003_BackfillAdrAccountRules.sql
-- Description: Backfills AdrAccountRule table with default rules from existing
--              AdrAccount data. Creates one rule per account for DownloadInvoice
--              job type (JobTypeId = 2). Part of Phase 1 BRD compliance.
-- Author: Devin AI
-- Date: 2025-12-17
-- ============================================================================

-- Backfill default rules from existing AdrAccount data
-- Only creates rules for accounts that don't already have a rule for JobTypeId = 2

DECLARE @InsertedCount INT = 0;

BEGIN TRANSACTION;

BEGIN TRY
    -- Insert default DownloadInvoice rules for accounts without existing rules
    INSERT INTO [dbo].[AdrAccountRule] (
        [AdrAccountId],
        [JobTypeId],
        [RuleName],
        [PeriodType],
        [PeriodDays],
        [DayOfMonth],
        [NextRunDateTime],
        [NextRangeStartDateTime],
        [NextRangeEndDateTime],
        [WindowDaysBefore],
        [WindowDaysAfter],
        [IsEnabled],
        [Priority],
        [IsManuallyOverridden],
        [OverriddenBy],
        [OverriddenDateTime],
        [Notes],
        [CreatedDateTime],
        [CreatedBy],
        [ModifiedDateTime],
        [ModifiedBy],
        [IsDeleted]
    )
    SELECT 
        a.[AdrAccountId],
        2 AS [JobTypeId],  -- 2 = DownloadInvoice/ADR Request
        'Default ADR Request Rule' AS [RuleName],
        a.[PeriodType],
        a.[PeriodDays],
        NULL AS [DayOfMonth],  -- Will be calculated based on historical data
        a.[NextRunDateTime],
        a.[NextRangeStartDateTime],
        a.[NextRangeEndDateTime],
        NULL AS [WindowDaysBefore],  -- Use system defaults
        NULL AS [WindowDaysAfter],   -- Use system defaults
        1 AS [IsEnabled],
        100 AS [Priority],
        a.[IsManuallyOverridden],
        a.[OverriddenBy],
        a.[OverriddenDateTime],
        'Auto-generated default rule from account data during Phase 1 migration' AS [Notes],
        GETUTCDATE() AS [CreatedDateTime],
        'System-Migration' AS [CreatedBy],
        GETUTCDATE() AS [ModifiedDateTime],
        'System-Migration' AS [ModifiedBy],
        a.[IsDeleted]
    FROM [dbo].[AdrAccount] a
    WHERE NOT EXISTS (
        SELECT 1 
        FROM [dbo].[AdrAccountRule] r 
        WHERE r.[AdrAccountId] = a.[AdrAccountId] 
        AND r.[JobTypeId] = 2
        AND r.[IsDeleted] = 0
    );
    
    SET @InsertedCount = @@ROWCOUNT;
    
    COMMIT TRANSACTION;
    
    PRINT 'Successfully created ' + CAST(@InsertedCount AS VARCHAR(10)) + ' default ADR Request rules';
    
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    
    PRINT 'Error during backfill: ' + ERROR_MESSAGE();
    THROW;
END CATCH
GO

-- Verify the backfill
SELECT 
    'Total AdrAccounts' AS [Metric],
    COUNT(*) AS [Count]
FROM [dbo].[AdrAccount]
WHERE [IsDeleted] = 0

UNION ALL

SELECT 
    'Total AdrAccountRules' AS [Metric],
    COUNT(*) AS [Count]
FROM [dbo].[AdrAccountRule]
WHERE [IsDeleted] = 0

UNION ALL

SELECT 
    'Accounts with Rules' AS [Metric],
    COUNT(DISTINCT [AdrAccountId]) AS [Count]
FROM [dbo].[AdrAccountRule]
WHERE [IsDeleted] = 0

UNION ALL

SELECT 
    'Accounts without Rules' AS [Metric],
    COUNT(*) AS [Count]
FROM [dbo].[AdrAccount] a
WHERE a.[IsDeleted] = 0
AND NOT EXISTS (
    SELECT 1 
    FROM [dbo].[AdrAccountRule] r 
    WHERE r.[AdrAccountId] = a.[AdrAccountId] 
    AND r.[IsDeleted] = 0
);
GO

PRINT 'Backfill verification complete';
GO
