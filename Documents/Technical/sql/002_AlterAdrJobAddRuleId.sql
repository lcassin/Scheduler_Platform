-- ============================================================================
-- Script: 002_AlterAdrJobAddRuleId.sql
-- Description: Adds AdrAccountRuleId column to AdrJob table to track which
--              rule created each job. Part of Phase 1 BRD compliance.
-- Author: Devin AI
-- Date: 2025-12-17
-- ============================================================================

-- Add AdrAccountRuleId column to AdrJob table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AdrJob]') AND name = 'AdrAccountRuleId')
BEGIN
    ALTER TABLE [dbo].[AdrJob]
    ADD [AdrAccountRuleId] INT NULL;
    
    PRINT 'Added column AdrAccountRuleId to AdrJob table';
END
ELSE
BEGIN
    PRINT 'Column AdrAccountRuleId already exists on AdrJob table';
END
GO

-- Add foreign key constraint (NO ACTION to avoid cascade path conflicts)
-- Using NO ACTION preserves audit trail - prevents accidental deletion of rules that have jobs
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_AdrJob_AdrAccountRule')
BEGIN
    ALTER TABLE [dbo].[AdrJob]
    ADD CONSTRAINT [FK_AdrJob_AdrAccountRule] 
    FOREIGN KEY ([AdrAccountRuleId])
    REFERENCES [dbo].[AdrAccountRule] ([AdrAccountRuleId])
    ON DELETE NO ACTION;
    
    PRINT 'Added foreign key FK_AdrJob_AdrAccountRule';
END
ELSE
BEGIN
    PRINT 'Foreign key FK_AdrJob_AdrAccountRule already exists';
END
GO

-- Add index on AdrAccountRuleId
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AdrJob_AdrAccountRuleId' AND object_id = OBJECT_ID('dbo.AdrJob'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrJob_AdrAccountRuleId] 
    ON [dbo].[AdrJob] ([AdrAccountRuleId]);
    
    PRINT 'Created index IX_AdrJob_AdrAccountRuleId';
END
ELSE
BEGIN
    PRINT 'Index IX_AdrJob_AdrAccountRuleId already exists';
END
GO

PRINT 'AdrJob table alteration complete';
GO
