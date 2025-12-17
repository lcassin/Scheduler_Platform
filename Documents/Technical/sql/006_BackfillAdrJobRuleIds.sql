-- ============================================================================
-- Script: 006_BackfillAdrJobRuleIds.sql
-- Description: Backfills AdrAccountRuleId on existing AdrJob records by matching
--              jobs to their corresponding account rules. Since Phase 1 created
--              one rule per account (JobTypeId=2 for DownloadInvoice), we match
--              based on AdrAccountId.
-- Author: Devin AI
-- Date: 2025-12-17
-- ============================================================================

-- First, verify we have exactly one rule per account (sanity check)
PRINT 'Checking for accounts with multiple rules...';
SELECT AdrAccountId, COUNT(*) as RuleCount
FROM AdrAccountRule 
WHERE IsDeleted = 0 AND JobTypeId = 2
GROUP BY AdrAccountId 
HAVING COUNT(*) > 1;

-- If the above returns rows, investigate before proceeding
-- For now, we assume one rule per account (JobTypeId=2)

-- Count jobs that need backfilling
DECLARE @TotalToUpdate INT;
SELECT @TotalToUpdate = COUNT(*)
FROM AdrJob j
WHERE j.AdrAccountRuleId IS NULL
  AND j.IsDeleted = 0
  AND EXISTS (
      SELECT 1 FROM AdrAccountRule r 
      WHERE r.AdrAccountId = j.AdrAccountId 
        AND r.IsDeleted = 0 
        AND r.JobTypeId = 2
  );

PRINT 'Total jobs to backfill: ' + CAST(@TotalToUpdate AS VARCHAR(20));
GO

-- Backfill in batches to avoid lock escalation on large tables
DECLARE @BatchSize INT = 5000;
DECLARE @RowsUpdated INT = 1;
DECLARE @TotalUpdated INT = 0;

PRINT 'Starting batched backfill of AdrAccountRuleId...';

WHILE @RowsUpdated > 0
BEGIN
    UPDATE TOP (@BatchSize) j
    SET j.AdrAccountRuleId = r.AdrAccountRuleId,
        j.ModifiedDateTime = GETUTCDATE(),
        j.ModifiedBy = 'System - RuleId Backfill'
    FROM AdrJob j
    INNER JOIN AdrAccountRule r ON j.AdrAccountId = r.AdrAccountId
    WHERE j.AdrAccountRuleId IS NULL
      AND j.IsDeleted = 0
      AND r.IsDeleted = 0
      AND r.JobTypeId = 2;
    
    SET @RowsUpdated = @@ROWCOUNT;
    SET @TotalUpdated = @TotalUpdated + @RowsUpdated;
    
    IF @RowsUpdated > 0
    BEGIN
        PRINT 'Updated ' + CAST(@RowsUpdated AS VARCHAR(20)) + ' rows (Total: ' + CAST(@TotalUpdated AS VARCHAR(20)) + ')';
    END
END

PRINT 'Backfill complete. Total jobs updated: ' + CAST(@TotalUpdated AS VARCHAR(20));
GO

-- Verification: Count jobs still without rules (should be 0 or only orphaned jobs)
PRINT 'Verification - Jobs without AdrAccountRuleId:';
SELECT 
    COUNT(*) as JobsWithoutRuleId,
    SUM(CASE WHEN r.AdrAccountRuleId IS NULL THEN 1 ELSE 0 END) as OrphanedJobs
FROM AdrJob j
LEFT JOIN AdrAccountRule r ON j.AdrAccountId = r.AdrAccountId AND r.IsDeleted = 0 AND r.JobTypeId = 2
WHERE j.AdrAccountRuleId IS NULL
  AND j.IsDeleted = 0;

-- Show sample of any remaining jobs without rules (orphaned - no matching account rule)
PRINT 'Sample of orphaned jobs (if any):';
SELECT TOP 10 
    j.AdrJobId, 
    j.AdrAccountId, 
    j.VMAccountId, 
    j.VendorCode,
    j.Status,
    j.CreatedDateTime
FROM AdrJob j
LEFT JOIN AdrAccountRule r ON j.AdrAccountId = r.AdrAccountId AND r.IsDeleted = 0 AND r.JobTypeId = 2
WHERE j.AdrAccountRuleId IS NULL
  AND j.IsDeleted = 0
  AND r.AdrAccountRuleId IS NULL
ORDER BY j.CreatedDateTime DESC;
GO

PRINT 'Script 006_BackfillAdrJobRuleIds.sql complete';
GO
