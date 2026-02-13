/*
 * Migration: 016_AddOrchestrationDurationColumns.sql
 * Description: Add Duration columns to AdrOrchestrationRun table to persist step durations
 * Date: 2026-02-03
 * 
 * This migration adds duration tracking for each orchestration step so that
 * duration chips can be displayed in the UI when viewing historical runs.
 */

-- Check if columns already exist before adding
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AdrOrchestrationRun') AND name = 'SyncDurationSeconds')
BEGIN
    ALTER TABLE [dbo].[AdrOrchestrationRun] ADD [SyncDurationSeconds] FLOAT NULL;
    PRINT 'Added SyncDurationSeconds column';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AdrOrchestrationRun') AND name = 'JobCreationDurationSeconds')
BEGIN
    ALTER TABLE [dbo].[AdrOrchestrationRun] ADD [JobCreationDurationSeconds] FLOAT NULL;
    PRINT 'Added JobCreationDurationSeconds column';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AdrOrchestrationRun') AND name = 'RebillDurationSeconds')
BEGIN
    ALTER TABLE [dbo].[AdrOrchestrationRun] ADD [RebillDurationSeconds] FLOAT NULL;
    PRINT 'Added RebillDurationSeconds column';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AdrOrchestrationRun') AND name = 'ScrapingDurationSeconds')
BEGIN
    ALTER TABLE [dbo].[AdrOrchestrationRun] ADD [ScrapingDurationSeconds] FLOAT NULL;
    PRINT 'Added ScrapingDurationSeconds column';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AdrOrchestrationRun') AND name = 'StatusCheckDurationSeconds')
BEGIN
    ALTER TABLE [dbo].[AdrOrchestrationRun] ADD [StatusCheckDurationSeconds] FLOAT NULL;
    PRINT 'Added StatusCheckDurationSeconds column';
END
GO

PRINT 'Migration 016_AddOrchestrationDurationColumns completed successfully';
