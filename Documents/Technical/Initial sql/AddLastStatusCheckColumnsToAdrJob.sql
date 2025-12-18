-- Migration: Add LastStatusCheckResponse and LastStatusCheckDateTime columns to AdrJob table
-- Purpose: Store raw API response from status checks for debugging purposes
-- Date: 2025-12-15

-- Add LastStatusCheckResponse column (stores truncated raw JSON response)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AdrJob') AND name = 'LastStatusCheckResponse')
BEGIN
    ALTER TABLE [dbo].[AdrJob]
    ADD [LastStatusCheckResponse] NVARCHAR(MAX) NULL;
    
    PRINT 'Added LastStatusCheckResponse column to AdrJob table';
END
ELSE
BEGIN
    PRINT 'LastStatusCheckResponse column already exists';
END
GO

-- Add LastStatusCheckDateTime column (timestamp of last status check)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AdrJob') AND name = 'LastStatusCheckDateTime')
BEGIN
    ALTER TABLE [dbo].[AdrJob]
    ADD [LastStatusCheckDateTime] DATETIME2 NULL;
    
    PRINT 'Added LastStatusCheckDateTime column to AdrJob table';
END
ELSE
BEGIN
    PRINT 'LastStatusCheckDateTime column already exists';
END
GO

-- Verify columns were added
SELECT 
    c.name AS ColumnName,
    t.name AS DataType,
    c.max_length AS MaxLength,
    c.is_nullable AS IsNullable
FROM sys.columns c
JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID('AdrJob')
AND c.name IN ('LastStatusCheckResponse', 'LastStatusCheckDateTime');
GO
