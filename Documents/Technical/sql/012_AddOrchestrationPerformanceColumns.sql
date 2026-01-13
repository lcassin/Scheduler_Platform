-- Migration: Add Orchestration Performance Settings to AdrConfiguration
-- Purpose: Add configurable timeout settings for long-running orchestration operations
-- Date: 2026-01-13

-- Add MaxOrchestrationDurationMinutes column
-- This controls how long an orchestration can run before being marked as failed
-- Default: 240 minutes (4 hours) to accommodate large datasets and slow API responses
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AdrConfiguration') AND name = 'MaxOrchestrationDurationMinutes')
BEGIN
    ALTER TABLE AdrConfiguration
    ADD MaxOrchestrationDurationMinutes INT NOT NULL DEFAULT 240;
    
    PRINT 'Added MaxOrchestrationDurationMinutes column to AdrConfiguration';
END
ELSE
BEGIN
    PRINT 'MaxOrchestrationDurationMinutes column already exists';
END
GO

-- Add DatabaseCommandTimeoutSeconds column
-- This controls the timeout for long-running database operations during orchestration
-- Default: 600 seconds (10 minutes) to handle large batch operations
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AdrConfiguration') AND name = 'DatabaseCommandTimeoutSeconds')
BEGIN
    ALTER TABLE AdrConfiguration
    ADD DatabaseCommandTimeoutSeconds INT NOT NULL DEFAULT 600;
    
    PRINT 'Added DatabaseCommandTimeoutSeconds column to AdrConfiguration';
END
ELSE
BEGIN
    PRINT 'DatabaseCommandTimeoutSeconds column already exists';
END
GO

-- Update existing records to have the default values (if any exist with NULL)
UPDATE AdrConfiguration
SET MaxOrchestrationDurationMinutes = 240
WHERE MaxOrchestrationDurationMinutes IS NULL;

UPDATE AdrConfiguration
SET DatabaseCommandTimeoutSeconds = 600
WHERE DatabaseCommandTimeoutSeconds IS NULL;
GO

PRINT 'Migration 012_AddOrchestrationPerformanceColumns completed successfully';
