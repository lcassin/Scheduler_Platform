-- =============================================
-- Migration: Add Performance Indexes
-- Description: Creates missing indexes for blacklist matching, paged queries, 
--              and common filter/sort patterns across all tables.
-- Safe to re-run: All statements use IF NOT EXISTS guards.
-- =============================================

PRINT '=== Starting Performance Index Migration ===';
PRINT '';

-- =============================================
-- AdrAccountBlacklist indexes
-- Critical for blacklist matching subqueries used in Accounts, Jobs, Rules, Dashboard
-- =============================================
PRINT '--- AdrAccountBlacklist ---';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccountBlacklist') AND name = 'IX_AdrAccountBlacklist_PrimaryVendorCode')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountBlacklist_PrimaryVendorCode]
    ON [dbo].[AdrAccountBlacklist]([PrimaryVendorCode] ASC);
    PRINT 'Created IX_AdrAccountBlacklist_PrimaryVendorCode';
END
ELSE PRINT 'IX_AdrAccountBlacklist_PrimaryVendorCode already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccountBlacklist') AND name = 'IX_AdrAccountBlacklist_MasterVendorCode')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountBlacklist_MasterVendorCode]
    ON [dbo].[AdrAccountBlacklist]([MasterVendorCode] ASC);
    PRINT 'Created IX_AdrAccountBlacklist_MasterVendorCode';
END
ELSE PRINT 'IX_AdrAccountBlacklist_MasterVendorCode already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccountBlacklist') AND name = 'IX_AdrAccountBlacklist_VMAccountId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountBlacklist_VMAccountId]
    ON [dbo].[AdrAccountBlacklist]([VMAccountId] ASC);
    PRINT 'Created IX_AdrAccountBlacklist_VMAccountId';
END
ELSE PRINT 'IX_AdrAccountBlacklist_VMAccountId already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccountBlacklist') AND name = 'IX_AdrAccountBlacklist_VMAccountNumber')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountBlacklist_VMAccountNumber]
    ON [dbo].[AdrAccountBlacklist]([VMAccountNumber] ASC);
    PRINT 'Created IX_AdrAccountBlacklist_VMAccountNumber';
END
ELSE PRINT 'IX_AdrAccountBlacklist_VMAccountNumber already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccountBlacklist') AND name = 'IX_AdrAccountBlacklist_CredentialId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountBlacklist_CredentialId]
    ON [dbo].[AdrAccountBlacklist]([CredentialId] ASC);
    PRINT 'Created IX_AdrAccountBlacklist_CredentialId';
END
ELSE PRINT 'IX_AdrAccountBlacklist_CredentialId already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccountBlacklist') AND name = 'IX_AdrAccountBlacklist_IsActive')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountBlacklist_IsActive]
    ON [dbo].[AdrAccountBlacklist]([IsActive] ASC);
    PRINT 'Created IX_AdrAccountBlacklist_IsActive';
END
ELSE PRINT 'IX_AdrAccountBlacklist_IsActive already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccountBlacklist') AND name = 'IX_AdrAccountBlacklist_IsDeleted_IsActive')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountBlacklist_IsDeleted_IsActive]
    ON [dbo].[AdrAccountBlacklist]([IsDeleted] ASC, [IsActive] ASC);
    PRINT 'Created IX_AdrAccountBlacklist_IsDeleted_IsActive';
END
ELSE PRINT 'IX_AdrAccountBlacklist_IsDeleted_IsActive already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccountBlacklist') AND name = 'IX_AdrAccountBlacklist_PrimaryVendorCode_VMAccountId_CredentialId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountBlacklist_PrimaryVendorCode_VMAccountId_CredentialId]
    ON [dbo].[AdrAccountBlacklist]([PrimaryVendorCode] ASC, [VMAccountId] ASC, [CredentialId] ASC);
    PRINT 'Created IX_AdrAccountBlacklist_PrimaryVendorCode_VMAccountId_CredentialId';
END
ELSE PRINT 'IX_AdrAccountBlacklist_PrimaryVendorCode_VMAccountId_CredentialId already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccountBlacklist') AND name = 'IX_AdrAccountBlacklist_MasterVendorCode_VMAccountId_CredentialId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountBlacklist_MasterVendorCode_VMAccountId_CredentialId]
    ON [dbo].[AdrAccountBlacklist]([MasterVendorCode] ASC, [VMAccountId] ASC, [CredentialId] ASC);
    PRINT 'Created IX_AdrAccountBlacklist_MasterVendorCode_VMAccountId_CredentialId';
END
ELSE PRINT 'IX_AdrAccountBlacklist_MasterVendorCode_VMAccountId_CredentialId already exists';
GO

-- =============================================
-- AdrAccount performance indexes
-- Speeds up paged queries with filter/sort on NextRunDateTime, VendorCode, etc.
-- Composite indexes optimize WHERE IsDeleted=0 AND NextRunStatus=X queries
-- =============================================
PRINT '';
PRINT '--- AdrAccount ---';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccount') AND name = 'IX_AdrAccount_NextRunDateTime')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccount_NextRunDateTime]
    ON [dbo].[AdrAccount]([NextRunDateTime] ASC);
    PRINT 'Created IX_AdrAccount_NextRunDateTime';
END
ELSE PRINT 'IX_AdrAccount_NextRunDateTime already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccount') AND name = 'IX_AdrAccount_InterfaceAccountId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccount_InterfaceAccountId]
    ON [dbo].[AdrAccount]([InterfaceAccountId] ASC);
    PRINT 'Created IX_AdrAccount_InterfaceAccountId';
END
ELSE PRINT 'IX_AdrAccount_InterfaceAccountId already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccount') AND name = 'IX_AdrAccount_PrimaryVendorCode')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccount_PrimaryVendorCode]
    ON [dbo].[AdrAccount]([PrimaryVendorCode] ASC);
    PRINT 'Created IX_AdrAccount_PrimaryVendorCode';
END
ELSE PRINT 'IX_AdrAccount_PrimaryVendorCode already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccount') AND name = 'IX_AdrAccount_MasterVendorCode')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccount_MasterVendorCode]
    ON [dbo].[AdrAccount]([MasterVendorCode] ASC);
    PRINT 'Created IX_AdrAccount_MasterVendorCode';
END
ELSE PRINT 'IX_AdrAccount_MasterVendorCode already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccount') AND name = 'IX_AdrAccount_IsDeleted_NextRunStatus_NextRunDateTime')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccount_IsDeleted_NextRunStatus_NextRunDateTime]
    ON [dbo].[AdrAccount]([IsDeleted] ASC, [NextRunStatus] ASC, [NextRunDateTime] ASC);
    PRINT 'Created IX_AdrAccount_IsDeleted_NextRunStatus_NextRunDateTime';
END
ELSE PRINT 'IX_AdrAccount_IsDeleted_NextRunStatus_NextRunDateTime already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccount') AND name = 'IX_AdrAccount_IsDeleted_HistoricalBillingStatus')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccount_IsDeleted_HistoricalBillingStatus]
    ON [dbo].[AdrAccount]([IsDeleted] ASC, [HistoricalBillingStatus] ASC);
    PRINT 'Created IX_AdrAccount_IsDeleted_HistoricalBillingStatus';
END
ELSE PRINT 'IX_AdrAccount_IsDeleted_HistoricalBillingStatus already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccount') AND name = 'IX_AdrAccount_IsDeleted_ClientId_NextRunStatus')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccount_IsDeleted_ClientId_NextRunStatus]
    ON [dbo].[AdrAccount]([IsDeleted] ASC, [ClientId] ASC, [NextRunStatus] ASC);
    PRINT 'Created IX_AdrAccount_IsDeleted_ClientId_NextRunStatus';
END
ELSE PRINT 'IX_AdrAccount_IsDeleted_ClientId_NextRunStatus already exists';
GO

-- =============================================
-- AdrJob performance indexes
-- Speeds up paged job queries with filter/sort on Status, VendorCode, etc.
-- Composite indexes optimize WHERE IsDeleted=0 AND Status=X queries
-- =============================================
PRINT '';
PRINT '--- AdrJob ---';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrJob') AND name = 'IX_AdrJob_AdrAccountRuleId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrJob_AdrAccountRuleId]
    ON [dbo].[AdrJob]([AdrAccountRuleId] ASC);
    PRINT 'Created IX_AdrJob_AdrAccountRuleId';
END
ELSE PRINT 'IX_AdrJob_AdrAccountRuleId already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrJob') AND name = 'IX_AdrJob_AdrJobTypeId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrJob_AdrJobTypeId]
    ON [dbo].[AdrJob]([AdrJobTypeId] ASC);
    PRINT 'Created IX_AdrJob_AdrJobTypeId';
END
ELSE PRINT 'IX_AdrJob_AdrJobTypeId already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrJob') AND name = 'IX_AdrJob_VMAccountNumber')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrJob_VMAccountNumber]
    ON [dbo].[AdrJob]([VMAccountNumber] ASC);
    PRINT 'Created IX_AdrJob_VMAccountNumber';
END
ELSE PRINT 'IX_AdrJob_VMAccountNumber already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrJob') AND name = 'IX_AdrJob_PrimaryVendorCode')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrJob_PrimaryVendorCode]
    ON [dbo].[AdrJob]([PrimaryVendorCode] ASC);
    PRINT 'Created IX_AdrJob_PrimaryVendorCode';
END
ELSE PRINT 'IX_AdrJob_PrimaryVendorCode already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrJob') AND name = 'IX_AdrJob_MasterVendorCode')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrJob_MasterVendorCode]
    ON [dbo].[AdrJob]([MasterVendorCode] ASC);
    PRINT 'Created IX_AdrJob_MasterVendorCode';
END
ELSE PRINT 'IX_AdrJob_MasterVendorCode already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrJob') AND name = 'IX_AdrJob_ModifiedDateTime')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrJob_ModifiedDateTime]
    ON [dbo].[AdrJob]([ModifiedDateTime] ASC);
    PRINT 'Created IX_AdrJob_ModifiedDateTime';
END
ELSE PRINT 'IX_AdrJob_ModifiedDateTime already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrJob') AND name = 'IX_AdrJob_ScrapingCompletedDateTime')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrJob_ScrapingCompletedDateTime]
    ON [dbo].[AdrJob]([ScrapingCompletedDateTime] ASC);
    PRINT 'Created IX_AdrJob_ScrapingCompletedDateTime';
END
ELSE PRINT 'IX_AdrJob_ScrapingCompletedDateTime already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrJob') AND name = 'IX_AdrJob_IsDeleted_Status')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrJob_IsDeleted_Status]
    ON [dbo].[AdrJob]([IsDeleted] ASC, [Status] ASC);
    PRINT 'Created IX_AdrJob_IsDeleted_Status';
END
ELSE PRINT 'IX_AdrJob_IsDeleted_Status already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrJob') AND name = 'IX_AdrJob_IsDeleted_Status_BillingPeriodStartDateTime')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrJob_IsDeleted_Status_BillingPeriodStartDateTime]
    ON [dbo].[AdrJob]([IsDeleted] ASC, [Status] ASC, [BillingPeriodStartDateTime] ASC);
    PRINT 'Created IX_AdrJob_IsDeleted_Status_BillingPeriodStartDateTime';
END
ELSE PRINT 'IX_AdrJob_IsDeleted_Status_BillingPeriodStartDateTime already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrJob') AND name = 'IX_AdrJob_IsDeleted_AdrAccountId_BillingPeriodStartDateTime')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrJob_IsDeleted_AdrAccountId_BillingPeriodStartDateTime]
    ON [dbo].[AdrJob]([IsDeleted] ASC, [AdrAccountId] ASC, [BillingPeriodStartDateTime] ASC);
    PRINT 'Created IX_AdrJob_IsDeleted_AdrAccountId_BillingPeriodStartDateTime';
END
ELSE PRINT 'IX_AdrJob_IsDeleted_AdrAccountId_BillingPeriodStartDateTime already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrJob') AND name = 'IX_AdrJob_IsDeleted_AdrAccountId_ScrapingCompletedDateTime')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrJob_IsDeleted_AdrAccountId_ScrapingCompletedDateTime]
    ON [dbo].[AdrJob]([IsDeleted] ASC, [AdrAccountId] ASC, [ScrapingCompletedDateTime] ASC);
    PRINT 'Created IX_AdrJob_IsDeleted_AdrAccountId_ScrapingCompletedDateTime';
END
ELSE PRINT 'IX_AdrJob_IsDeleted_AdrAccountId_ScrapingCompletedDateTime already exists';
GO

-- =============================================
-- AdrJobExecution index
-- =============================================
PRINT '';
PRINT '--- AdrJobExecution ---';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrJobExecution') AND name = 'IX_AdrJobExecution_OrchestrationRequestId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrJobExecution_OrchestrationRequestId]
    ON [dbo].[AdrJobExecution]([OrchestrationRequestId] ASC)
    WHERE [OrchestrationRequestId] IS NOT NULL;
    PRINT 'Created IX_AdrJobExecution_OrchestrationRequestId';
END
ELSE PRINT 'IX_AdrJobExecution_OrchestrationRequestId already exists';
GO

-- =============================================
-- AdrAccountRule indexes
-- Speeds up rule queries with filter on AdrAccountId, JobTypeId, IsEnabled
-- =============================================
PRINT '';
PRINT '--- AdrAccountRule ---';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccountRule') AND name = 'IX_AdrAccountRule_AdrAccountId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountRule_AdrAccountId]
    ON [dbo].[AdrAccountRule]([AdrAccountId] ASC);
    PRINT 'Created IX_AdrAccountRule_AdrAccountId';
END
ELSE PRINT 'IX_AdrAccountRule_AdrAccountId already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccountRule') AND name = 'IX_AdrAccountRule_JobTypeId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountRule_JobTypeId]
    ON [dbo].[AdrAccountRule]([JobTypeId] ASC);
    PRINT 'Created IX_AdrAccountRule_JobTypeId';
END
ELSE PRINT 'IX_AdrAccountRule_JobTypeId already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccountRule') AND name = 'IX_AdrAccountRule_IsEnabled')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountRule_IsEnabled]
    ON [dbo].[AdrAccountRule]([IsEnabled] ASC);
    PRINT 'Created IX_AdrAccountRule_IsEnabled';
END
ELSE PRINT 'IX_AdrAccountRule_IsEnabled already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccountRule') AND name = 'IX_AdrAccountRule_NextRunDateTime')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountRule_NextRunDateTime]
    ON [dbo].[AdrAccountRule]([NextRunDateTime] ASC);
    PRINT 'Created IX_AdrAccountRule_NextRunDateTime';
END
ELSE PRINT 'IX_AdrAccountRule_NextRunDateTime already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccountRule') AND name = 'IX_AdrAccountRule_AdrAccountId_JobTypeId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountRule_AdrAccountId_JobTypeId]
    ON [dbo].[AdrAccountRule]([AdrAccountId] ASC, [JobTypeId] ASC);
    PRINT 'Created IX_AdrAccountRule_AdrAccountId_JobTypeId';
END
ELSE PRINT 'IX_AdrAccountRule_AdrAccountId_JobTypeId already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccountRule') AND name = 'IX_AdrAccountRule_IsDeleted_IsEnabled_NextRunDateTime')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountRule_IsDeleted_IsEnabled_NextRunDateTime]
    ON [dbo].[AdrAccountRule]([IsDeleted] ASC, [IsEnabled] ASC, [NextRunDateTime] ASC);
    PRINT 'Created IX_AdrAccountRule_IsDeleted_IsEnabled_NextRunDateTime';
END
ELSE PRINT 'IX_AdrAccountRule_IsDeleted_IsEnabled_NextRunDateTime already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrAccountRule') AND name = 'IX_AdrAccountRule_IsDeleted_JobTypeId_AdrAccountId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrAccountRule_IsDeleted_JobTypeId_AdrAccountId]
    ON [dbo].[AdrAccountRule]([IsDeleted] ASC, [JobTypeId] ASC, [AdrAccountId] ASC);
    PRINT 'Created IX_AdrAccountRule_IsDeleted_JobTypeId_AdrAccountId';
END
ELSE PRINT 'IX_AdrAccountRule_IsDeleted_JobTypeId_AdrAccountId already exists';
GO

-- =============================================
-- AdrOrchestrationRun indexes
-- =============================================
PRINT '';
PRINT '--- AdrOrchestrationRun ---';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrOrchestrationRun') AND name = 'IX_AdrOrchestrationRun_RequestId')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_AdrOrchestrationRun_RequestId]
    ON [dbo].[AdrOrchestrationRun]([RequestId] ASC);
    PRINT 'Created IX_AdrOrchestrationRun_RequestId (unique)';
END
ELSE PRINT 'IX_AdrOrchestrationRun_RequestId already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrOrchestrationRun') AND name = 'IX_AdrOrchestrationRun_RequestedDateTime')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrOrchestrationRun_RequestedDateTime]
    ON [dbo].[AdrOrchestrationRun]([RequestedDateTime] DESC);
    PRINT 'Created IX_AdrOrchestrationRun_RequestedDateTime';
END
ELSE PRINT 'IX_AdrOrchestrationRun_RequestedDateTime already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrOrchestrationRun') AND name = 'IX_AdrOrchestrationRun_Status')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrOrchestrationRun_Status]
    ON [dbo].[AdrOrchestrationRun]([Status] ASC);
    PRINT 'Created IX_AdrOrchestrationRun_Status';
END
ELSE PRINT 'IX_AdrOrchestrationRun_Status already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrOrchestrationRun') AND name = 'IX_AdrOrchestrationRun_Status_RequestedDateTime')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrOrchestrationRun_Status_RequestedDateTime]
    ON [dbo].[AdrOrchestrationRun]([Status] ASC, [RequestedDateTime] DESC);
    PRINT 'Created IX_AdrOrchestrationRun_Status_RequestedDateTime';
END
ELSE PRINT 'IX_AdrOrchestrationRun_Status_RequestedDateTime already exists';
GO

-- =============================================
-- AdrJobType indexes
-- =============================================
PRINT '';
PRINT '--- AdrJobType ---';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrJobType') AND name = 'IX_AdrJobType_Code')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_AdrJobType_Code]
    ON [dbo].[AdrJobType]([Code] ASC);
    PRINT 'Created IX_AdrJobType_Code (unique)';
END
ELSE PRINT 'IX_AdrJobType_Code already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrJobType') AND name = 'IX_AdrJobType_IsActive')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrJobType_IsActive]
    ON [dbo].[AdrJobType]([IsActive] ASC);
    PRINT 'Created IX_AdrJobType_IsActive';
END
ELSE PRINT 'IX_AdrJobType_IsActive already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrJobType') AND name = 'IX_AdrJobType_AdrRequestTypeId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrJobType_AdrRequestTypeId]
    ON [dbo].[AdrJobType]([AdrRequestTypeId] ASC);
    PRINT 'Created IX_AdrJobType_AdrRequestTypeId';
END
ELSE PRINT 'IX_AdrJobType_AdrRequestTypeId already exists';
GO

-- =============================================
-- PowerBiReport indexes
-- =============================================
PRINT '';
PRINT '--- PowerBiReport ---';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.PowerBiReport') AND name = 'IX_PowerBiReport_Category')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PowerBiReport_Category]
    ON [dbo].[PowerBiReport]([Category] ASC);
    PRINT 'Created IX_PowerBiReport_Category';
END
ELSE PRINT 'IX_PowerBiReport_Category already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.PowerBiReport') AND name = 'IX_PowerBiReport_IsActive')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PowerBiReport_IsActive]
    ON [dbo].[PowerBiReport]([IsActive] ASC);
    PRINT 'Created IX_PowerBiReport_IsActive';
END
ELSE PRINT 'IX_PowerBiReport_IsActive already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.PowerBiReport') AND name = 'IX_PowerBiReport_Category_DisplayOrder')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PowerBiReport_Category_DisplayOrder]
    ON [dbo].[PowerBiReport]([Category] ASC, [DisplayOrder] ASC);
    PRINT 'Created IX_PowerBiReport_Category_DisplayOrder';
END
ELSE PRINT 'IX_PowerBiReport_Category_DisplayOrder already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.PowerBiReport') AND name = 'IX_PowerBiReport_IsDeleted_IsActive_Category_DisplayOrder')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_PowerBiReport_IsDeleted_IsActive_Category_DisplayOrder]
    ON [dbo].[PowerBiReport]([IsDeleted] ASC, [IsActive] ASC, [Category] ASC, [DisplayOrder] ASC);
    PRINT 'Created IX_PowerBiReport_IsDeleted_IsActive_Category_DisplayOrder';
END
ELSE PRINT 'IX_PowerBiReport_IsDeleted_IsActive_Category_DisplayOrder already exists';
GO

-- =============================================
-- Archive table indexes
-- =============================================
PRINT '';
PRINT '--- AdrJobArchive ---';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrJobArchive') AND name = 'IX_AdrJobArchive_OriginalAdrJobId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrJobArchive_OriginalAdrJobId]
    ON [dbo].[AdrJobArchive]([OriginalAdrJobId] ASC);
    PRINT 'Created IX_AdrJobArchive_OriginalAdrJobId';
END
ELSE PRINT 'IX_AdrJobArchive_OriginalAdrJobId already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrJobArchive') AND name = 'IX_AdrJobArchive_AdrAccountId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrJobArchive_AdrAccountId]
    ON [dbo].[AdrJobArchive]([AdrAccountId] ASC);
    PRINT 'Created IX_AdrJobArchive_AdrAccountId';
END
ELSE PRINT 'IX_AdrJobArchive_AdrAccountId already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrJobArchive') AND name = 'IX_AdrJobArchive_VMAccountId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrJobArchive_VMAccountId]
    ON [dbo].[AdrJobArchive]([VMAccountId] ASC);
    PRINT 'Created IX_AdrJobArchive_VMAccountId';
END
ELSE PRINT 'IX_AdrJobArchive_VMAccountId already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrJobArchive') AND name = 'IX_AdrJobArchive_ArchivedDateTime')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrJobArchive_ArchivedDateTime]
    ON [dbo].[AdrJobArchive]([ArchivedDateTime] ASC);
    PRINT 'Created IX_AdrJobArchive_ArchivedDateTime';
END
ELSE PRINT 'IX_AdrJobArchive_ArchivedDateTime already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrJobArchive') AND name = 'IX_AdrJobArchive_BillingPeriodStartDateTime')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrJobArchive_BillingPeriodStartDateTime]
    ON [dbo].[AdrJobArchive]([BillingPeriodStartDateTime] ASC);
    PRINT 'Created IX_AdrJobArchive_BillingPeriodStartDateTime';
END
ELSE PRINT 'IX_AdrJobArchive_BillingPeriodStartDateTime already exists';
GO

PRINT '';
PRINT '--- AdrJobExecutionArchive ---';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrJobExecutionArchive') AND name = 'IX_AdrJobExecutionArchive_OriginalAdrJobExecutionId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrJobExecutionArchive_OriginalAdrJobExecutionId]
    ON [dbo].[AdrJobExecutionArchive]([OriginalAdrJobExecutionId] ASC);
    PRINT 'Created IX_AdrJobExecutionArchive_OriginalAdrJobExecutionId';
END
ELSE PRINT 'IX_AdrJobExecutionArchive_OriginalAdrJobExecutionId already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrJobExecutionArchive') AND name = 'IX_AdrJobExecutionArchive_AdrJobId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrJobExecutionArchive_AdrJobId]
    ON [dbo].[AdrJobExecutionArchive]([AdrJobId] ASC);
    PRINT 'Created IX_AdrJobExecutionArchive_AdrJobId';
END
ELSE PRINT 'IX_AdrJobExecutionArchive_AdrJobId already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrJobExecutionArchive') AND name = 'IX_AdrJobExecutionArchive_ArchivedDateTime')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrJobExecutionArchive_ArchivedDateTime]
    ON [dbo].[AdrJobExecutionArchive]([ArchivedDateTime] ASC);
    PRINT 'Created IX_AdrJobExecutionArchive_ArchivedDateTime';
END
ELSE PRINT 'IX_AdrJobExecutionArchive_ArchivedDateTime already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AdrJobExecutionArchive') AND name = 'IX_AdrJobExecutionArchive_StartDateTime')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AdrJobExecutionArchive_StartDateTime]
    ON [dbo].[AdrJobExecutionArchive]([StartDateTime] ASC);
    PRINT 'Created IX_AdrJobExecutionArchive_StartDateTime';
END
ELSE PRINT 'IX_AdrJobExecutionArchive_StartDateTime already exists';
GO

PRINT '';
PRINT '--- AuditLogArchive ---';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AuditLogArchive') AND name = 'IX_AuditLogArchive_OriginalAuditLogId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AuditLogArchive_OriginalAuditLogId]
    ON [dbo].[AuditLogArchive]([OriginalAuditLogId] ASC);
    PRINT 'Created IX_AuditLogArchive_OriginalAuditLogId';
END
ELSE PRINT 'IX_AuditLogArchive_OriginalAuditLogId already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AuditLogArchive') AND name = 'IX_AuditLogArchive_ArchivedDateTime')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AuditLogArchive_ArchivedDateTime]
    ON [dbo].[AuditLogArchive]([ArchivedDateTime] ASC);
    PRINT 'Created IX_AuditLogArchive_ArchivedDateTime';
END
ELSE PRINT 'IX_AuditLogArchive_ArchivedDateTime already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AuditLogArchive') AND name = 'IX_AuditLogArchive_TimestampDateTime')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AuditLogArchive_TimestampDateTime]
    ON [dbo].[AuditLogArchive]([TimestampDateTime] ASC);
    PRINT 'Created IX_AuditLogArchive_TimestampDateTime';
END
ELSE PRINT 'IX_AuditLogArchive_TimestampDateTime already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AuditLogArchive') AND name = 'IX_AuditLogArchive_EntityType_EntityId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AuditLogArchive_EntityType_EntityId]
    ON [dbo].[AuditLogArchive]([EntityType] ASC, [EntityId] ASC);
    PRINT 'Created IX_AuditLogArchive_EntityType_EntityId';
END
ELSE PRINT 'IX_AuditLogArchive_EntityType_EntityId already exists';
GO

PRINT '';
PRINT '--- JobExecutionArchive ---';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.JobExecutionArchive') AND name = 'IX_JobExecutionArchive_OriginalJobExecutionId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_JobExecutionArchive_OriginalJobExecutionId]
    ON [dbo].[JobExecutionArchive]([OriginalJobExecutionId] ASC);
    PRINT 'Created IX_JobExecutionArchive_OriginalJobExecutionId';
END
ELSE PRINT 'IX_JobExecutionArchive_OriginalJobExecutionId already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.JobExecutionArchive') AND name = 'IX_JobExecutionArchive_ScheduleId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_JobExecutionArchive_ScheduleId]
    ON [dbo].[JobExecutionArchive]([ScheduleId] ASC);
    PRINT 'Created IX_JobExecutionArchive_ScheduleId';
END
ELSE PRINT 'IX_JobExecutionArchive_ScheduleId already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.JobExecutionArchive') AND name = 'IX_JobExecutionArchive_ArchivedDateTime')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_JobExecutionArchive_ArchivedDateTime]
    ON [dbo].[JobExecutionArchive]([ArchivedDateTime] ASC);
    PRINT 'Created IX_JobExecutionArchive_ArchivedDateTime';
END
ELSE PRINT 'IX_JobExecutionArchive_ArchivedDateTime already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.JobExecutionArchive') AND name = 'IX_JobExecutionArchive_StartDateTime')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_JobExecutionArchive_StartDateTime]
    ON [dbo].[JobExecutionArchive]([StartDateTime] ASC);
    PRINT 'Created IX_JobExecutionArchive_StartDateTime';
END
ELSE PRINT 'IX_JobExecutionArchive_StartDateTime already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.JobExecutionArchive') AND name = 'IX_JobExecutionArchive_Status')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_JobExecutionArchive_Status]
    ON [dbo].[JobExecutionArchive]([Status] ASC);
    PRINT 'Created IX_JobExecutionArchive_Status';
END
ELSE PRINT 'IX_JobExecutionArchive_Status already exists';
GO

PRINT '';
PRINT '=== Performance Index Migration Complete ===';
GO
