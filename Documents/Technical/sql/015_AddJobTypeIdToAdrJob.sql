-- Migration: Add AdrJobTypeId column to AdrJob table
-- This allows distinguishing between different job types (1 = Credential Check, 2 = Download Invoice, 3 = Rebill)
-- Rebill jobs are persistent per-account and reused for all rebill executions

-- Add AdrJobTypeId column (nullable for backward compatibility with existing jobs)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AdrJob') AND name = 'AdrJobTypeId')
BEGIN
    ALTER TABLE AdrJob ADD AdrJobTypeId INT NULL;
    
    -- Add foreign key constraint with NO ACTION to avoid cascade path issues
    ALTER TABLE AdrJob ADD CONSTRAINT FK_AdrJob_AdrJobType 
        FOREIGN KEY (AdrJobTypeId) REFERENCES AdrJobType(AdrJobTypeId) ON DELETE NO ACTION;
    
    -- Add index for querying jobs by type
    CREATE NONCLUSTERED INDEX IX_AdrJob_AdrJobTypeId ON AdrJob(AdrJobTypeId) WHERE AdrJobTypeId IS NOT NULL;
    
    PRINT 'Added AdrJobTypeId column to AdrJob table';
END
ELSE
BEGIN
    PRINT 'AdrJobTypeId column already exists on AdrJob table';
END
GO

-- Update existing jobs to have AdrJobTypeId = 2 (Download Invoice) as the default
-- since most existing jobs are scraping jobs
UPDATE AdrJob SET AdrJobTypeId = 2 WHERE AdrJobTypeId IS NULL;
PRINT 'Updated existing jobs to AdrJobTypeId = 2 (Download Invoice)';
GO
