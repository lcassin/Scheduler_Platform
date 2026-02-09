-- Migration: Remove FinalStatusCheckDelayDays column from AdrConfiguration table
-- This column is no longer used - the "One Last Check" functionality has been removed
-- Date: 2026-02-06

-- First, drop the default constraint on the column (constraint name is auto-generated)
DECLARE @ConstraintName NVARCHAR(256);

SELECT @ConstraintName = dc.name
FROM sys.default_constraints dc
INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE dc.parent_object_id = OBJECT_ID('dbo.AdrConfiguration')
AND c.name = 'FinalStatusCheckDelayDays';

IF @ConstraintName IS NOT NULL
BEGIN
    EXEC('ALTER TABLE [dbo].[AdrConfiguration] DROP CONSTRAINT [' + @ConstraintName + ']');
    PRINT 'Default constraint ' + @ConstraintName + ' dropped from FinalStatusCheckDelayDays column';
END

-- Now drop the column if it exists
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'AdrConfiguration' 
    AND COLUMN_NAME = 'FinalStatusCheckDelayDays'
)
BEGIN
    ALTER TABLE [dbo].[AdrConfiguration] DROP COLUMN [FinalStatusCheckDelayDays];
    PRINT 'Column FinalStatusCheckDelayDays dropped from AdrConfiguration table';
END
ELSE
BEGIN
    PRINT 'Column FinalStatusCheckDelayDays does not exist in AdrConfiguration table - no action needed';
END
GO
