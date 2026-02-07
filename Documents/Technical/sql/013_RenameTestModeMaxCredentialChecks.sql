/*------------------------
-- Migration: Rename TestModeMaxCredentialChecks to TestModeMaxRebillJobs
-- Description: Renames the test mode column from credential checks to rebill jobs
--              since credential verification has been replaced by rebill processing
-- Date: 2026-02-06
------------------------*/

-- Check if the old column exists and new column doesn't exist
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AdrConfiguration') AND name = 'TestModeMaxCredentialChecks')
   AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AdrConfiguration') AND name = 'TestModeMaxRebillJobs')
BEGIN
    -- Rename the column
    EXEC sp_rename 'dbo.AdrConfiguration.TestModeMaxCredentialChecks', 'TestModeMaxRebillJobs', 'COLUMN';
    
    PRINT 'Column TestModeMaxCredentialChecks renamed to TestModeMaxRebillJobs';
END
ELSE IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AdrConfiguration') AND name = 'TestModeMaxRebillJobs')
BEGIN
    PRINT 'Column TestModeMaxRebillJobs already exists - migration already applied';
END
ELSE
BEGIN
    PRINT 'Column TestModeMaxCredentialChecks not found - cannot rename';
END

PRINT 'Migration 013_RenameTestModeMaxCredentialChecks completed';
