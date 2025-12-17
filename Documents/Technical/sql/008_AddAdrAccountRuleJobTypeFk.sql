-- =============================================
-- Script: 008_AddAdrAccountRuleJobTypeFk.sql
-- Description: Adds foreign key constraint from AdrAccountRule.JobTypeId to AdrJobType.AdrJobTypeId
--              This links rules to their job types in the new AdrJobType table.
-- Phase: 3 - Job Types
-- Run Order: After script 007 (AdrJobType table must exist with default rows)
-- =============================================

-- Add foreign key constraint (NO ACTION to avoid cascade path conflicts)
-- Using NO ACTION preserves audit trail - prevents accidental deletion of job types that have rules
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_AdrAccountRule_AdrJobType')
BEGIN
    ALTER TABLE [dbo].[AdrAccountRule]
    ADD CONSTRAINT [FK_AdrAccountRule_AdrJobType] 
    FOREIGN KEY ([JobTypeId])
    REFERENCES [dbo].[AdrJobType] ([AdrJobTypeId])
    ON DELETE NO ACTION;
    
    PRINT 'Added FK_AdrAccountRule_AdrJobType constraint';
END
ELSE
BEGIN
    PRINT 'FK_AdrAccountRule_AdrJobType constraint already exists';
END
GO

-- Verification: Check that all existing rules have valid JobTypeId values
SELECT 'Rules with invalid JobTypeId' AS [Check], COUNT(*) AS [Count]
FROM [dbo].[AdrAccountRule] r
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[AdrJobType] jt WHERE jt.AdrJobTypeId = r.JobTypeId);

-- Show distribution of rules by job type
SELECT 
    jt.Code AS [JobTypeCode],
    jt.Name AS [JobTypeName],
    COUNT(r.AdrAccountRuleId) AS [RuleCount]
FROM [dbo].[AdrJobType] jt
LEFT JOIN [dbo].[AdrAccountRule] r ON r.JobTypeId = jt.AdrJobTypeId AND r.IsDeleted = 0
GROUP BY jt.AdrJobTypeId, jt.Code, jt.Name
ORDER BY jt.AdrJobTypeId;
GO
