-- =============================================
-- Script: 010_AddAdrJobExportIndexes.sql
-- Description: Add indexes to AdrJob table to improve export query performance
-- 
-- The export query for ADR accounts needs to look up:
-- 1. The latest job status per account (by BillingPeriodStartDateTime DESC)
-- 2. The last completed datetime per account (by ScrapingCompletedDateTime DESC)
--
-- These indexes support the correlated subqueries in the export endpoint.
-- =============================================

USE [SchedulerPlatform]
GO

PRINT 'Adding performance indexes to AdrJob table for export queries...'
GO

-- Index for ScrapingCompletedDateTime lookups
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AdrJob_ScrapingCompletedDateTime' AND object_id = OBJECT_ID('AdrJob'))
BEGIN
    PRINT 'Creating index IX_AdrJob_ScrapingCompletedDateTime...'
    CREATE NONCLUSTERED INDEX [IX_AdrJob_ScrapingCompletedDateTime] 
    ON [dbo].[AdrJob] ([ScrapingCompletedDateTime])
    PRINT 'Index IX_AdrJob_ScrapingCompletedDateTime created successfully.'
END
ELSE
BEGIN
    PRINT 'Index IX_AdrJob_ScrapingCompletedDateTime already exists.'
END
GO

-- Composite index for export query: lookup latest completed job per account
-- This supports the query pattern: WHERE IsDeleted = 0 AND AdrAccountId IN (...) ORDER BY ScrapingCompletedDateTime DESC
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AdrJob_IsDeleted_AdrAccountId_ScrapingCompletedDateTime' AND object_id = OBJECT_ID('AdrJob'))
BEGIN
    PRINT 'Creating index IX_AdrJob_IsDeleted_AdrAccountId_ScrapingCompletedDateTime...'
    CREATE NONCLUSTERED INDEX [IX_AdrJob_IsDeleted_AdrAccountId_ScrapingCompletedDateTime] 
    ON [dbo].[AdrJob] ([IsDeleted], [AdrAccountId], [ScrapingCompletedDateTime] DESC)
    INCLUDE ([Status])
    PRINT 'Index IX_AdrJob_IsDeleted_AdrAccountId_ScrapingCompletedDateTime created successfully.'
END
ELSE
BEGIN
    PRINT 'Index IX_AdrJob_IsDeleted_AdrAccountId_ScrapingCompletedDateTime already exists.'
END
GO

-- Update the existing composite index to include Status column for covering the export query
-- This avoids key lookups when getting the latest job status per account
-- First check if the index exists and if it already has the Status column included
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AdrJob_IsDeleted_AdrAccountId_BillingPeriodStartDateTime' AND object_id = OBJECT_ID('AdrJob'))
BEGIN
    -- Check if Status is already included
    IF NOT EXISTS (
        SELECT 1 
        FROM sys.indexes i
        INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
        INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        WHERE i.name = 'IX_AdrJob_IsDeleted_AdrAccountId_BillingPeriodStartDateTime' 
        AND i.object_id = OBJECT_ID('AdrJob')
        AND c.name = 'Status'
        AND ic.is_included_column = 1
    )
    BEGIN
        PRINT 'Dropping and recreating IX_AdrJob_IsDeleted_AdrAccountId_BillingPeriodStartDateTime with Status included...'
        DROP INDEX [IX_AdrJob_IsDeleted_AdrAccountId_BillingPeriodStartDateTime] ON [dbo].[AdrJob]
        
        CREATE NONCLUSTERED INDEX [IX_AdrJob_IsDeleted_AdrAccountId_BillingPeriodStartDateTime] 
        ON [dbo].[AdrJob] ([IsDeleted], [AdrAccountId], [BillingPeriodStartDateTime] DESC)
        INCLUDE ([Status])
        PRINT 'Index IX_AdrJob_IsDeleted_AdrAccountId_BillingPeriodStartDateTime recreated with Status included.'
    END
    ELSE
    BEGIN
        PRINT 'Index IX_AdrJob_IsDeleted_AdrAccountId_BillingPeriodStartDateTime already has Status included.'
    END
END
ELSE
BEGIN
    PRINT 'Creating index IX_AdrJob_IsDeleted_AdrAccountId_BillingPeriodStartDateTime with Status included...'
    CREATE NONCLUSTERED INDEX [IX_AdrJob_IsDeleted_AdrAccountId_BillingPeriodStartDateTime] 
    ON [dbo].[AdrJob] ([IsDeleted], [AdrAccountId], [BillingPeriodStartDateTime] DESC)
    INCLUDE ([Status])
    PRINT 'Index IX_AdrJob_IsDeleted_AdrAccountId_BillingPeriodStartDateTime created successfully.'
END
GO

PRINT ''
PRINT '=========================================='
PRINT 'Index creation complete!'
PRINT ''
PRINT 'Verification - Current indexes on AdrJob table:'
SELECT 
    i.name AS IndexName,
    i.type_desc AS IndexType,
    STUFF((
        SELECT ', ' + c.name + CASE WHEN ic.is_descending_key = 1 THEN ' DESC' ELSE '' END
        FROM sys.index_columns ic
        INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
        ORDER BY ic.key_ordinal
        FOR XML PATH('')
    ), 1, 2, '') AS KeyColumns,
    STUFF((
        SELECT ', ' + c.name
        FROM sys.index_columns ic
        INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 1
        ORDER BY ic.index_column_id
        FOR XML PATH('')
    ), 1, 2, '') AS IncludedColumns
FROM sys.indexes i
WHERE i.object_id = OBJECT_ID('AdrJob')
AND i.name IS NOT NULL
ORDER BY i.name
GO
