-- Migration: Remove FinalStatusCheckDelayDays column from AdrConfiguration table
-- This column is no longer used - the "One Last Check" functionality has been removed
-- Date: 2026-02-06

-- Check if the column exists before dropping
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
