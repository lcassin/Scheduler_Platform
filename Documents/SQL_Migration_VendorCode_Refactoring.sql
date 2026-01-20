-- SQL Migration Script: Rename VendorCode to PrimaryVendorCode and Add MasterVendorCode
-- This script should be run against the SchedulerPlatform database
-- Date: 2026-01-20
-- Description: Renames VendorCode column to PrimaryVendorCode and adds MasterVendorCode column
--              to AdrAccount, AdrJob, AdrJobArchive, and AdrAccountBlacklist tables

-- =====================================================
-- STEP 1: AdrAccount Table
-- =====================================================
-- Rename VendorCode to PrimaryVendorCode
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AdrAccount') AND name = 'VendorCode')
BEGIN
    EXEC sp_rename 'AdrAccount.VendorCode', 'PrimaryVendorCode', 'COLUMN';
    PRINT 'Renamed AdrAccount.VendorCode to PrimaryVendorCode';
END
GO

-- Add MasterVendorCode column
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AdrAccount') AND name = 'MasterVendorCode')
BEGIN
    ALTER TABLE AdrAccount ADD MasterVendorCode NVARCHAR(50) NULL;
    PRINT 'Added MasterVendorCode column to AdrAccount';
END
GO

-- Create index on MasterVendorCode for AdrAccount
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('AdrAccount') AND name = 'IX_AdrAccount_MasterVendorCode')
BEGIN
    CREATE NONCLUSTERED INDEX IX_AdrAccount_MasterVendorCode ON AdrAccount (MasterVendorCode);
    PRINT 'Created index IX_AdrAccount_MasterVendorCode';
END
GO

-- =====================================================
-- STEP 2: AdrJob Table
-- =====================================================
-- Rename VendorCode to PrimaryVendorCode
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AdrJob') AND name = 'VendorCode')
BEGIN
    EXEC sp_rename 'AdrJob.VendorCode', 'PrimaryVendorCode', 'COLUMN';
    PRINT 'Renamed AdrJob.VendorCode to PrimaryVendorCode';
END
GO

-- Add MasterVendorCode column
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AdrJob') AND name = 'MasterVendorCode')
BEGIN
    ALTER TABLE AdrJob ADD MasterVendorCode NVARCHAR(50) NULL;
    PRINT 'Added MasterVendorCode column to AdrJob';
END
GO

-- Create index on MasterVendorCode for AdrJob
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('AdrJob') AND name = 'IX_AdrJob_MasterVendorCode')
BEGIN
    CREATE NONCLUSTERED INDEX IX_AdrJob_MasterVendorCode ON AdrJob (MasterVendorCode);
    PRINT 'Created index IX_AdrJob_MasterVendorCode';
END
GO

-- =====================================================
-- STEP 3: AdrJobArchive Table
-- =====================================================
-- Rename VendorCode to PrimaryVendorCode
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AdrJobArchive') AND name = 'VendorCode')
BEGIN
    EXEC sp_rename 'AdrJobArchive.VendorCode', 'PrimaryVendorCode', 'COLUMN';
    PRINT 'Renamed AdrJobArchive.VendorCode to PrimaryVendorCode';
END
GO

-- Add MasterVendorCode column
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AdrJobArchive') AND name = 'MasterVendorCode')
BEGIN
    ALTER TABLE AdrJobArchive ADD MasterVendorCode NVARCHAR(50) NULL;
    PRINT 'Added MasterVendorCode column to AdrJobArchive';
END
GO

-- =====================================================
-- STEP 4: AdrAccountBlacklist Table
-- =====================================================
-- Rename VendorCode to PrimaryVendorCode
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AdrAccountBlacklist') AND name = 'VendorCode')
BEGIN
    EXEC sp_rename 'AdrAccountBlacklist.VendorCode', 'PrimaryVendorCode', 'COLUMN';
    PRINT 'Renamed AdrAccountBlacklist.VendorCode to PrimaryVendorCode';
END
GO

-- Add MasterVendorCode column
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AdrAccountBlacklist') AND name = 'MasterVendorCode')
BEGIN
    ALTER TABLE AdrAccountBlacklist ADD MasterVendorCode NVARCHAR(50) NULL;
    PRINT 'Added MasterVendorCode column to AdrAccountBlacklist';
END
GO

-- Create index on MasterVendorCode for AdrAccountBlacklist
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('AdrAccountBlacklist') AND name = 'IX_AdrAccountBlacklist_MasterVendorCode')
BEGIN
    CREATE NONCLUSTERED INDEX IX_AdrAccountBlacklist_MasterVendorCode ON AdrAccountBlacklist (MasterVendorCode);
    PRINT 'Created index IX_AdrAccountBlacklist_MasterVendorCode';
END
GO

-- =====================================================
-- VERIFICATION: Check that all columns were renamed/added
-- =====================================================
SELECT 
    t.name AS TableName,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.max_length AS MaxLength,
    c.is_nullable AS IsNullable
FROM sys.tables t
INNER JOIN sys.columns c ON t.object_id = c.object_id
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE t.name IN ('AdrAccount', 'AdrJob', 'AdrJobArchive', 'AdrAccountBlacklist')
AND c.name IN ('PrimaryVendorCode', 'MasterVendorCode')
ORDER BY t.name, c.name;

PRINT 'Migration complete. Please verify the results above.';
GO
