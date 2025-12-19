-- Script 011: Add retention columns to AdrConfiguration table
-- These columns support the new data archival and log cleanup features

-- Add ArchiveRetentionYears column (years to keep archived records before permanent deletion)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AdrConfiguration') AND name = 'ArchiveRetentionYears')
BEGIN
    ALTER TABLE AdrConfiguration ADD ArchiveRetentionYears INT NOT NULL DEFAULT 7;
    PRINT 'Added ArchiveRetentionYears column to AdrConfiguration';
END
ELSE
BEGIN
    PRINT 'ArchiveRetentionYears column already exists';
END
GO

-- Add LogRetentionDays column (days to keep log files before deletion)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AdrConfiguration') AND name = 'LogRetentionDays')
BEGIN
    ALTER TABLE AdrConfiguration ADD LogRetentionDays INT NOT NULL DEFAULT 30;
    PRINT 'Added LogRetentionDays column to AdrConfiguration';
END
ELSE
BEGIN
    PRINT 'LogRetentionDays column already exists';
END
GO

-- Update existing records to have default values (in case the defaults didn't apply)
UPDATE AdrConfiguration 
SET ArchiveRetentionYears = 7 
WHERE ArchiveRetentionYears IS NULL OR ArchiveRetentionYears = 0;

UPDATE AdrConfiguration 
SET LogRetentionDays = 30 
WHERE LogRetentionDays IS NULL OR LogRetentionDays = 0;
GO

PRINT 'Script 011 completed successfully';
