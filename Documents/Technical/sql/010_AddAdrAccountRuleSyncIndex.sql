-- ============================================================================
-- Script: 010_AddAdrAccountRuleSyncIndex.sql
-- Description: Adds optimized composite index for AdrAccountRule sync query
--              to fix timeout issues during ADR account synchronization.
-- Author: Devin AI
-- Date: 2026-01-08
-- ============================================================================
-- The sync query filters on: AdrAccountId IN (...) AND IsDeleted = 0 AND JobTypeId = 2
-- This composite index puts the equality filters first for optimal performance.
-- ============================================================================

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AdrAccountRule_IsDeleted_JobTypeId_AdrAccountId' AND object_id = OBJECT_ID('dbo.AdrAccountRule'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountRule_IsDeleted_JobTypeId_AdrAccountId] 
    ON [dbo].[AdrAccountRule] ([IsDeleted], [JobTypeId], [AdrAccountId]);
    PRINT 'Created index IX_AdrAccountRule_IsDeleted_JobTypeId_AdrAccountId';
END
ELSE
BEGIN
    PRINT 'Index IX_AdrAccountRule_IsDeleted_JobTypeId_AdrAccountId already exists';
END
GO

PRINT 'AdrAccountRule sync index creation complete';
GO
