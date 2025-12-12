-- Script to add unique constraint on AdrJob to prevent duplicate jobs
-- for the same account and billing period
-- Run this script AFTER checking for existing duplicates

-- First, check for any existing duplicates that would violate the constraint
-- Run this query first and resolve any duplicates before applying the constraint:
/*
SELECT AdrAccountId, BillingPeriodStartDateTime, BillingPeriodEndDateTime, COUNT(*) AS DuplicateCount
FROM AdrJob
WHERE IsDeleted = 0
GROUP BY AdrAccountId, BillingPeriodStartDateTime, BillingPeriodEndDateTime
HAVING COUNT(*) > 1;
*/

-- If no duplicates exist, create the unique filtered index
-- This prevents duplicate jobs for the same account and billing period
-- The WHERE IsDeleted = 0 filter allows soft-deleted records to exist as duplicates
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE name = 'UX_AdrJob_Account_BillingPeriod' 
    AND object_id = OBJECT_ID('AdrJob')
)
BEGIN
    CREATE UNIQUE INDEX UX_AdrJob_Account_BillingPeriod
    ON AdrJob (AdrAccountId, BillingPeriodStartDateTime, BillingPeriodEndDateTime)
    WHERE IsDeleted = 0;
    
    PRINT 'Created unique index UX_AdrJob_Account_BillingPeriod on AdrJob';
END
ELSE
BEGIN
    PRINT 'Index UX_AdrJob_Account_BillingPeriod already exists';
END
GO
