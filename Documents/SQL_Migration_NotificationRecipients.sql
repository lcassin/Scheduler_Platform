-- Migration Script: Add Email Notification Settings to AdrConfiguration
-- Date: 2026-01-26
-- Description: Adds columns for configurable email notification recipients
--              for 500 errors and orchestration failure notifications.

-- Add Test Mode columns (if not already present)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AdrConfiguration') AND name = 'TestModeEnabled')
BEGIN
    ALTER TABLE [dbo].[AdrConfiguration] ADD [TestModeEnabled] BIT NOT NULL DEFAULT ((0));
    PRINT 'Added TestModeEnabled column';
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AdrConfiguration') AND name = 'TestModeMaxScrapingJobs')
BEGIN
    ALTER TABLE [dbo].[AdrConfiguration] ADD [TestModeMaxScrapingJobs] INT NOT NULL DEFAULT ((50));
    PRINT 'Added TestModeMaxScrapingJobs column';
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AdrConfiguration') AND name = 'TestModeMaxCredentialChecks')
BEGIN
    ALTER TABLE [dbo].[AdrConfiguration] ADD [TestModeMaxCredentialChecks] INT NOT NULL DEFAULT ((50));
    PRINT 'Added TestModeMaxCredentialChecks column';
END

-- Add Error Notification columns
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AdrConfiguration') AND name = 'ErrorNotificationsEnabled')
BEGIN
    ALTER TABLE [dbo].[AdrConfiguration] ADD [ErrorNotificationsEnabled] BIT NOT NULL DEFAULT ((1));
    PRINT 'Added ErrorNotificationsEnabled column';
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AdrConfiguration') AND name = 'ErrorNotificationRecipients')
BEGIN
    ALTER TABLE [dbo].[AdrConfiguration] ADD [ErrorNotificationRecipients] NVARCHAR(500) NULL;
    PRINT 'Added ErrorNotificationRecipients column';
END

-- Add Orchestration Notification columns
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AdrConfiguration') AND name = 'OrchestrationNotificationsEnabled')
BEGIN
    ALTER TABLE [dbo].[AdrConfiguration] ADD [OrchestrationNotificationsEnabled] BIT NOT NULL DEFAULT ((1));
    PRINT 'Added OrchestrationNotificationsEnabled column';
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AdrConfiguration') AND name = 'OrchestrationNotificationRecipients')
BEGIN
    ALTER TABLE [dbo].[AdrConfiguration] ADD [OrchestrationNotificationRecipients] NVARCHAR(500) NULL;
    PRINT 'Added OrchestrationNotificationRecipients column';
END

-- Set default recipients for existing configuration row(s)
-- Update these email addresses as needed for your environment
UPDATE [dbo].[AdrConfiguration]
SET [ErrorNotificationRecipients] = 'lcassin@cassinfo.com;jwilson@cassinfo.com',
    [OrchestrationNotificationRecipients] = 'lcassin@cassinfo.com;jwilson@cassinfo.com;dmiller@cassinfo.com'
WHERE [ErrorNotificationRecipients] IS NULL 
   OR [OrchestrationNotificationRecipients] IS NULL;

PRINT 'Migration completed successfully';
