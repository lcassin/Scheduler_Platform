-- =============================================
-- Migration: Update Existing Indexes for Blacklist Flags
-- Description: Replaces existing composite indexes with versions that include
--              IsCurrentlyBlacklisted for optimal query performance.
--              Also adds new indexes for common query patterns with blacklist filtering.
--              Run AFTER AddBlacklistFlagsToAdrAccount.sql and AddPerformanceIndexes.sql.
-- Safe to re-run: All statements use IF NOT EXISTS / IF EXISTS guards.
-- =============================================

PRINT '=== Starting Index Update for Blacklist Flags ===';
PRINT '';

-- =============================================
-- Step 1: Replace AdrAccount composite indexes
-- These existing indexes are used by queries that NOW also filter by IsCurrentlyBlacklisted.
-- Without IsCurrentlyBlacklisted in the index, SQL Server does key lookups for every row.
-- =============================================
PRINT '--- Replacing AdrAccount composite indexes ---';

-- 1a. Replace (IsDeleted, NextRunStatus, NextRunDateTime) 
--     with (IsDeleted, IsCurrentlyBlacklisted, NextRunStatus, NextRunDateTime)
-- Used by: Accounts page with status filter + default sort + blacklist filter
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccount') AND name = 'IX_AdrAccount_IsDeleted_NextRunStatus_NextRunDateTime')
BEGIN
    DROP INDEX [IX_AdrAccount_IsDeleted_NextRunStatus_NextRunDateTime] ON [dbo].[AdrAccount];
    PRINT 'Dropped IX_AdrAccount_IsDeleted_NextRunStatus_NextRunDateTime';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccount') AND name = 'IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_NextRunStatus_NextRunDateTime')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_NextRunStatus_NextRunDateTime]
    ON [dbo].[AdrAccount]([IsDeleted] ASC, [IsCurrentlyBlacklisted] ASC, [NextRunStatus] ASC, [NextRunDateTime] ASC);
    PRINT 'Created IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_NextRunStatus_NextRunDateTime';
END
ELSE PRINT 'IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_NextRunStatus_NextRunDateTime already exists';
GO

-- 1b. Replace (IsDeleted, ClientId, NextRunStatus)
--     with (IsDeleted, IsCurrentlyBlacklisted, ClientId, NextRunStatus)
-- Used by: Client-filtered queries with blacklist filter (Dashboard with clientId)
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccount') AND name = 'IX_AdrAccount_IsDeleted_ClientId_NextRunStatus')
BEGIN
    DROP INDEX [IX_AdrAccount_IsDeleted_ClientId_NextRunStatus] ON [dbo].[AdrAccount];
    PRINT 'Dropped IX_AdrAccount_IsDeleted_ClientId_NextRunStatus';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccount') AND name = 'IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_ClientId_NextRunStatus')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_ClientId_NextRunStatus]
    ON [dbo].[AdrAccount]([IsDeleted] ASC, [IsCurrentlyBlacklisted] ASC, [ClientId] ASC, [NextRunStatus] ASC);
    PRINT 'Created IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_ClientId_NextRunStatus';
END
ELSE PRINT 'IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_ClientId_NextRunStatus already exists';
GO

-- 1c. Replace (IsDeleted, HistoricalBillingStatus)
--     with (IsDeleted, IsCurrentlyBlacklisted, HistoricalBillingStatus)
-- Used by: Missing Invoices page + Dashboard Missing count
-- Note: We already have IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_HistoricalBillingStatus 
--       from AddBlacklistFlagsToAdrAccount.sql, so just drop the old one if it exists
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccount') AND name = 'IX_AdrAccount_IsDeleted_HistoricalBillingStatus')
BEGIN
    DROP INDEX [IX_AdrAccount_IsDeleted_HistoricalBillingStatus] ON [dbo].[AdrAccount];
    PRINT 'Dropped IX_AdrAccount_IsDeleted_HistoricalBillingStatus (superseded by IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_HistoricalBillingStatus)';
END
ELSE PRINT 'IX_AdrAccount_IsDeleted_HistoricalBillingStatus already dropped or never existed';
GO

-- =============================================
-- Step 2: Add new AdrAccount composite indexes
-- These support common query patterns with blacklist filtering
-- =============================================
PRINT '';
PRINT '--- Adding new AdrAccount composite indexes ---';

-- 2a. (IsDeleted, IsCurrentlyBlacklisted, NextRunDateTime)
-- Used by: Accounts page default sort with blacklist filter (most common query)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccount') AND name = 'IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_NextRunDateTime')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_NextRunDateTime]
    ON [dbo].[AdrAccount]([IsDeleted] ASC, [IsCurrentlyBlacklisted] ASC, [NextRunDateTime] ASC);
    PRINT 'Created IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_NextRunDateTime';
END
ELSE PRINT 'IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_NextRunDateTime already exists';
GO

-- 2b. (IsDeleted, IsCurrentlyBlacklisted, PrimaryVendorCode)
-- Used by: Accounts page sorted by vendor code with blacklist filter
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccount') AND name = 'IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_PrimaryVendorCode')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_PrimaryVendorCode]
    ON [dbo].[AdrAccount]([IsDeleted] ASC, [IsCurrentlyBlacklisted] ASC, [PrimaryVendorCode] ASC);
    PRINT 'Created IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_PrimaryVendorCode';
END
ELSE PRINT 'IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_PrimaryVendorCode already exists';
GO

-- 2c. (IsDeleted, IsCurrentlyBlacklisted, CredentialId)
-- Used by: Accounts page filtered by credential with blacklist filter
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccount') AND name = 'IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_CredentialId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_CredentialId]
    ON [dbo].[AdrAccount]([IsDeleted] ASC, [IsCurrentlyBlacklisted] ASC, [CredentialId] ASC);
    PRINT 'Created IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_CredentialId';
END
ELSE PRINT 'IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_CredentialId already exists';
GO

-- =============================================
-- Step 3: Update AdrJob indexes for blacklist JOIN queries
-- Jobs page now JOINs to AdrAccount to filter by IsCurrentlyBlacklisted.
-- The join uses AdrJob.AdrAccountId → AdrAccount.Id (PK).
-- =============================================
PRINT '';
PRINT '--- Updating AdrJob indexes ---';

-- 3a. Replace (IsDeleted, Status) with (IsDeleted, Status, AdrAccountId)
-- Covers: Jobs page WHERE IsDeleted=0 AND Status=X with JOIN to AdrAccount
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrJob') AND name = 'IX_AdrJob_IsDeleted_Status')
BEGIN
    DROP INDEX [IX_AdrJob_IsDeleted_Status] ON [dbo].[AdrJob];
    PRINT 'Dropped IX_AdrJob_IsDeleted_Status';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrJob') AND name = 'IX_AdrJob_IsDeleted_Status_AdrAccountId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrJob_IsDeleted_Status_AdrAccountId]
    ON [dbo].[AdrJob]([IsDeleted] ASC, [Status] ASC, [AdrAccountId] ASC);
    PRINT 'Created IX_AdrJob_IsDeleted_Status_AdrAccountId';
END
ELSE PRINT 'IX_AdrJob_IsDeleted_Status_AdrAccountId already exists';
GO

-- 3b. Add (IsDeleted, AdrAccountId, Status) - different key order for JOIN-first queries
-- Covers: Jobs page filtered by account with status filter
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrJob') AND name = 'IX_AdrJob_IsDeleted_AdrAccountId_Status')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrJob_IsDeleted_AdrAccountId_Status]
    ON [dbo].[AdrJob]([IsDeleted] ASC, [AdrAccountId] ASC, [Status] ASC);
    PRINT 'Created IX_AdrJob_IsDeleted_AdrAccountId_Status';
END
ELSE PRINT 'IX_AdrJob_IsDeleted_AdrAccountId_Status already exists';
GO

-- =============================================
-- Step 4: Update AdrAccountRule indexes for blacklist JOIN queries
-- Rules page JOINs to AdrAccount to filter by IsCurrentlyBlacklisted.
-- =============================================
PRINT '';
PRINT '--- Updating AdrAccountRule indexes ---';

-- 4a. Add composite index for Rules page with blacklist filter JOIN
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccountRule') AND name = 'IX_AdrAccountRule_IsDeleted_AdrAccountId_JobTypeId_IsEnabled')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountRule_IsDeleted_AdrAccountId_JobTypeId_IsEnabled]
    ON [dbo].[AdrAccountRule]([IsDeleted] ASC, [AdrAccountId] ASC, [JobTypeId] ASC, [IsEnabled] ASC);
    PRINT 'Created IX_AdrAccountRule_IsDeleted_AdrAccountId_JobTypeId_IsEnabled';
END
ELSE PRINT 'IX_AdrAccountRule_IsDeleted_AdrAccountId_JobTypeId_IsEnabled already exists';
GO

-- =============================================
-- Step 5: Drop redundant standalone indexes
-- These are now superseded by the composite indexes above
-- =============================================
PRINT '';
PRINT '--- Cleaning up redundant indexes ---';

-- The standalone IsCurrentlyBlacklisted index is superseded by all the composites
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccount') AND name = 'IX_AdrAccount_IsCurrentlyBlacklisted')
BEGIN
    DROP INDEX [IX_AdrAccount_IsCurrentlyBlacklisted] ON [dbo].[AdrAccount];
    PRINT 'Dropped IX_AdrAccount_IsCurrentlyBlacklisted (superseded by composite indexes)';
END
ELSE PRINT 'IX_AdrAccount_IsCurrentlyBlacklisted already dropped or never existed';
GO

-- The old (IsDeleted, IsCurrentlyBlacklisted, NextRunStatus) is superseded by the 4-column version
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccount') AND name = 'IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_NextRunStatus')
BEGIN
    DROP INDEX [IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_NextRunStatus] ON [dbo].[AdrAccount];
    PRINT 'Dropped IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_NextRunStatus (superseded by 4-column version with NextRunDateTime)';
END
ELSE PRINT 'IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_NextRunStatus already dropped or never existed';
GO

-- =============================================
-- Step 6: Update statistics
-- After adding/dropping indexes and populating new columns,
-- statistics may be stale. This helps SQL Server choose optimal plans.
-- =============================================
PRINT '';
PRINT '--- Updating statistics ---';

UPDATE STATISTICS [dbo].[AdrAccount];
PRINT 'Updated AdrAccount statistics';

UPDATE STATISTICS [dbo].[AdrJob];
PRINT 'Updated AdrJob statistics';

UPDATE STATISTICS [dbo].[AdrAccountRule];
PRINT 'Updated AdrAccountRule statistics';

UPDATE STATISTICS [dbo].[AdrAccountBlacklist];
PRINT 'Updated AdrAccountBlacklist statistics';

PRINT '';
PRINT '=== Index Update for Blacklist Flags Complete ===';
GO
