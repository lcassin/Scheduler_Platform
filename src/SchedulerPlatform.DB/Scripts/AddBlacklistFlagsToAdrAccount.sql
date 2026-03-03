-- =============================================
-- Migration: Add Blacklist Flags to AdrAccount
-- Description: Adds denormalized IsCurrentlyBlacklisted and IsFutureBlacklisted 
--              columns to AdrAccount for fast blacklist filtering.
--              These flags are updated during Account Sync and eliminate the need
--              for expensive blacklist table joins on every query.
-- Safe to re-run: All statements use IF NOT EXISTS guards.
-- =============================================

PRINT '=== Starting Blacklist Flags Migration ===';
PRINT '';

-- =============================================
-- Step 1: Add columns
-- =============================================
PRINT '--- Adding columns ---';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AdrAccount') AND name = 'IsCurrentlyBlacklisted')
BEGIN
    ALTER TABLE [dbo].[AdrAccount] ADD [IsCurrentlyBlacklisted] BIT NOT NULL CONSTRAINT [DF_AdrAccount_IsCurrentlyBlacklisted] DEFAULT (0);
    PRINT 'Added IsCurrentlyBlacklisted column';
END
ELSE PRINT 'IsCurrentlyBlacklisted column already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AdrAccount') AND name = 'IsFutureBlacklisted')
BEGIN
    ALTER TABLE [dbo].[AdrAccount] ADD [IsFutureBlacklisted] BIT NOT NULL CONSTRAINT [DF_AdrAccount_IsFutureBlacklisted] DEFAULT (0);
    PRINT 'Added IsFutureBlacklisted column';
END
ELSE PRINT 'IsFutureBlacklisted column already exists';
GO

-- =============================================
-- Step 2: Add indexes
-- =============================================
PRINT '';
PRINT '--- Adding indexes ---';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccount') AND name = 'IX_AdrAccount_IsCurrentlyBlacklisted')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccount_IsCurrentlyBlacklisted]
    ON [dbo].[AdrAccount]([IsCurrentlyBlacklisted] ASC);
    PRINT 'Created IX_AdrAccount_IsCurrentlyBlacklisted';
END
ELSE PRINT 'IX_AdrAccount_IsCurrentlyBlacklisted already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccount') AND name = 'IX_AdrAccount_IsFutureBlacklisted')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccount_IsFutureBlacklisted]
    ON [dbo].[AdrAccount]([IsFutureBlacklisted] ASC);
    PRINT 'Created IX_AdrAccount_IsFutureBlacklisted';
END
ELSE PRINT 'IX_AdrAccount_IsFutureBlacklisted already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccount') AND name = 'IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_NextRunStatus')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_NextRunStatus]
    ON [dbo].[AdrAccount]([IsDeleted] ASC, [IsCurrentlyBlacklisted] ASC, [NextRunStatus] ASC);
    PRINT 'Created IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_NextRunStatus';
END
ELSE PRINT 'IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_NextRunStatus already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccount') AND name = 'IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_HistoricalBillingStatus')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_HistoricalBillingStatus]
    ON [dbo].[AdrAccount]([IsDeleted] ASC, [IsCurrentlyBlacklisted] ASC, [HistoricalBillingStatus] ASC);
    PRINT 'Created IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_HistoricalBillingStatus';
END
ELSE PRINT 'IX_AdrAccount_IsDeleted_IsCurrentlyBlacklisted_HistoricalBillingStatus already exists';
GO

-- =============================================
-- Step 3: Initial population of flags
-- This sets the flags based on current blacklist entries.
-- After this, the Account Sync process will keep them up to date.
-- =============================================
PRINT '';
PRINT '--- Populating initial flag values ---';

DECLARE @today DATE = CAST(GETUTCDATE() AS DATE);

-- Reset all flags first
UPDATE [dbo].[AdrAccount]
SET [IsCurrentlyBlacklisted] = 0,
    [IsFutureBlacklisted] = 0;
PRINT 'Reset all flags to 0';

-- Set IsCurrentlyBlacklisted for active (non-deleted) accounts that match current blacklist entries
UPDATE a
SET a.[IsCurrentlyBlacklisted] = 1
FROM [dbo].[AdrAccount] a
WHERE a.[IsDeleted] = 0
  AND EXISTS (
    SELECT 1 FROM [dbo].[AdrAccountBlacklist] b
    WHERE b.[IsDeleted] = 0 AND b.[IsActive] = 1
      AND (b.[EffectiveStartDate] IS NULL OR b.[EffectiveStartDate] <= @today)
      AND (b.[EffectiveEndDate] IS NULL OR b.[EffectiveEndDate] >= @today)
      AND (
        (b.[PrimaryVendorCode] IS NOT NULL AND b.[PrimaryVendorCode] <> '' AND b.[PrimaryVendorCode] = a.[PrimaryVendorCode])
        OR (b.[VMAccountId] IS NOT NULL AND b.[VMAccountId] = a.[VMAccountId])
        OR (b.[VMAccountNumber] IS NOT NULL AND b.[VMAccountNumber] <> '' AND b.[VMAccountNumber] = a.[VMAccountNumber])
        OR (b.[CredentialId] IS NOT NULL AND b.[CredentialId] = a.[CredentialId])
      )
  );
PRINT 'Set IsCurrentlyBlacklisted for matching accounts: ' + CAST(@@ROWCOUNT AS VARCHAR(20)) + ' rows updated';

-- Set IsFutureBlacklisted for active (non-deleted) accounts that match future blacklist entries
UPDATE a
SET a.[IsFutureBlacklisted] = 1
FROM [dbo].[AdrAccount] a
WHERE a.[IsDeleted] = 0
  AND EXISTS (
    SELECT 1 FROM [dbo].[AdrAccountBlacklist] b
    WHERE b.[IsDeleted] = 0 AND b.[IsActive] = 1
      AND b.[EffectiveStartDate] IS NOT NULL AND b.[EffectiveStartDate] > @today
      AND (
        (b.[PrimaryVendorCode] IS NOT NULL AND b.[PrimaryVendorCode] <> '' AND b.[PrimaryVendorCode] = a.[PrimaryVendorCode])
        OR (b.[VMAccountId] IS NOT NULL AND b.[VMAccountId] = a.[VMAccountId])
        OR (b.[VMAccountNumber] IS NOT NULL AND b.[VMAccountNumber] <> '' AND b.[VMAccountNumber] = a.[VMAccountNumber])
        OR (b.[CredentialId] IS NOT NULL AND b.[CredentialId] = a.[CredentialId])
      )
  );
PRINT 'Set IsFutureBlacklisted for matching accounts: ' + CAST(@@ROWCOUNT AS VARCHAR(20)) + ' rows updated';

PRINT '';
PRINT '=== Blacklist Flags Migration Complete ===';
GO
