-- Migration: Add JobTypeId column to AdrJob table
-- This allows distinguishing between different job types (1 = Credential Check, 2 = Download Invoice, 3 = Rebill)
-- Rebill jobs are persistent per-account and reused for all rebill executions

-- Add JobTypeId column (nullable for backward compatibility with existing jobs)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AdrJob') AND name = 'JobTypeId')
BEGIN
    ALTER TABLE AdrJob ADD JobTypeId INT NULL;
    
    -- Add foreign key constraint with NO ACTION to avoid cascade path issues
    ALTER TABLE AdrJob ADD CONSTRAINT FK_AdrJob_AdrJobType 
        FOREIGN KEY (JobTypeId) REFERENCES AdrJobType(Id) ON DELETE NO ACTION;
    
    -- Add index for querying jobs by type
    CREATE NONCLUSTERED INDEX IX_AdrJob_JobTypeId ON AdrJob(JobTypeId) WHERE JobTypeId IS NOT NULL;
    
    PRINT 'Added JobTypeId column to AdrJob table';
END
ELSE
BEGIN
    PRINT 'JobTypeId column already exists on AdrJob table';
END
GO

-- Update existing jobs to have JobTypeId = 2 (Download Invoice) as the default
-- since most existing jobs are scraping jobs
UPDATE AdrJob SET JobTypeId = 2 WHERE JobTypeId IS NULL;
PRINT 'Updated existing jobs to JobTypeId = 2 (Download Invoice)';
GO
