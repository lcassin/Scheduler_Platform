-- Migration: Add OrchestrationRequestId to AdrJobExecution
-- This column links each execution to the orchestration run that triggered it.

-- Step 1: Add the column (nullable, no backfill yet)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('dbo.AdrJobExecution') 
    AND name = 'OrchestrationRequestId'
)
BEGIN
    ALTER TABLE [dbo].[AdrJobExecution]
    ADD [OrchestrationRequestId] NVARCHAR(450) NULL;
    
    PRINT 'Column OrchestrationRequestId added to AdrJobExecution.';
END
ELSE
BEGIN
    PRINT 'Column OrchestrationRequestId already exists on AdrJobExecution.';
END
GO

-- Step 2: Create filtered index on OrchestrationRequestId
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE object_id = OBJECT_ID('dbo.AdrJobExecution') 
    AND name = 'IX_AdrJobExecution_OrchestrationRequestId'
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrJobExecution_OrchestrationRequestId]
    ON [dbo].[AdrJobExecution]([OrchestrationRequestId] ASC)
    WHERE [OrchestrationRequestId] IS NOT NULL;
    
    PRINT 'Index IX_AdrJobExecution_OrchestrationRequestId created.';
END
ELSE
BEGIN
    PRINT 'Index IX_AdrJobExecution_OrchestrationRequestId already exists.';
END
GO

-- Step 3: Backfill existing executions by matching timestamps to orchestration runs.
-- Each execution's StartDateTime should fall between its orchestration run's StartedDateTime and CompletedDateTime.
UPDATE aje
SET aje.OrchestrationRequestId = aor.RequestId
FROM [dbo].[AdrJobExecution] aje
INNER JOIN [dbo].[AdrOrchestrationRun] aor
    ON aje.StartDateTime >= aor.StartedDateTime
    AND aje.StartDateTime <= ISNULL(aor.CompletedDateTime, aor.StartedDateTime)
    AND aor.CompletedDateTime IS NOT NULL
WHERE aje.OrchestrationRequestId IS NULL;

PRINT 'Backfill complete. Updated rows: ' + CAST(@@ROWCOUNT AS VARCHAR(20));
GO
