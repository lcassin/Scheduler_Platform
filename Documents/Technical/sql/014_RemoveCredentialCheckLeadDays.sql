/*------------------------
-- Migration: Remove CredentialCheckLeadDays column from AdrConfiguration
-- Date: 2026-02-07
-- Description: Removes the CredentialCheckLeadDays column as it is no longer needed.
--              Jobs are now created when NextRunDate <= today (the day scraping should start)
--              instead of being created early for credential verification.
------------------------*/

-- Check if the column exists before attempting to drop
IF EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[AdrConfiguration]') 
    AND name = 'CredentialCheckLeadDays'
)
BEGIN
    -- First, drop the default constraint if it exists
    DECLARE @ConstraintName NVARCHAR(200)
    SELECT @ConstraintName = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[AdrConfiguration]')
    AND c.name = 'CredentialCheckLeadDays'
    
    IF @ConstraintName IS NOT NULL
    BEGIN
        EXEC('ALTER TABLE [dbo].[AdrConfiguration] DROP CONSTRAINT [' + @ConstraintName + ']')
        PRINT 'Dropped default constraint: ' + @ConstraintName
    END
    
    -- Now drop the column
    ALTER TABLE [dbo].[AdrConfiguration] DROP COLUMN [CredentialCheckLeadDays]
    PRINT 'Successfully dropped CredentialCheckLeadDays column from AdrConfiguration table'
END
ELSE
BEGIN
    PRINT 'Column CredentialCheckLeadDays does not exist in AdrConfiguration table - no action needed'
END
GO

PRINT 'Migration 014_RemoveCredentialCheckLeadDays completed successfully'
GO
