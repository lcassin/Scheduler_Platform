-- ============================================================================
-- Seed Production: NotificationSetting Table
-- ============================================================================
-- Run this AFTER Schedule seeding (NotificationSetting has FK to Schedule).
-- Seeds notification settings for each schedule.
-- ============================================================================
-- INSTRUCTIONS:
-- 1. Run the SELECT query below against your UAT database to generate
--    INSERT statements.
-- 2. Copy the generated INSERT statements.
-- 3. Run the generated INSERT statements against your Production database.
-- ============================================================================

-- ============================================================================
-- STEP 1: Run this against UAT to generate INSERT statements
-- Only includes notifications for non-deleted, non-system schedules
-- (System schedule notifications will be re-created by the app if needed)
-- ============================================================================

SET NOCOUNT ON;

PRINT 'SET IDENTITY_INSERT [dbo].[NotificationSetting] ON;';
PRINT '';

SELECT
    'IF NOT EXISTS (SELECT 1 FROM [dbo].[NotificationSetting] WHERE [NotificationSettingId] = ' + CAST(ns.[NotificationSettingId] AS NVARCHAR(20)) + ')' + CHAR(13) + CHAR(10) +
    'BEGIN' + CHAR(13) + CHAR(10) +
    '    INSERT INTO [dbo].[NotificationSetting] (' + CHAR(13) + CHAR(10) +
    '        [NotificationSettingId], [ScheduleId],' + CHAR(13) + CHAR(10) +
    '        [EnableSuccessNotifications], [EnableFailureNotifications],' + CHAR(13) + CHAR(10) +
    '        [SuccessEmailRecipients], [FailureEmailRecipients],' + CHAR(13) + CHAR(10) +
    '        [SuccessEmailSubject], [FailureEmailSubject],' + CHAR(13) + CHAR(10) +
    '        [IncludeExecutionDetails], [IncludeOutput],' + CHAR(13) + CHAR(10) +
    '        [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted]' + CHAR(13) + CHAR(10) +
    '    )' + CHAR(13) + CHAR(10) +
    '    VALUES (' +
        CAST(ns.[NotificationSettingId] AS NVARCHAR(20)) + ', ' +
        CAST(ns.[ScheduleId] AS NVARCHAR(20)) + ', ' +
        CAST(ns.[EnableSuccessNotifications] AS NVARCHAR(1)) + ', ' +
        CAST(ns.[EnableFailureNotifications] AS NVARCHAR(1)) + ', ' +
        CASE WHEN ns.[SuccessEmailRecipients] IS NULL THEN 'NULL' ELSE 'N''' + REPLACE(ns.[SuccessEmailRecipients], '''', '''''') + '''' END + ', ' +
        CASE WHEN ns.[FailureEmailRecipients] IS NULL THEN 'NULL' ELSE 'N''' + REPLACE(ns.[FailureEmailRecipients], '''', '''''') + '''' END + ', ' +
        CASE WHEN ns.[SuccessEmailSubject] IS NULL THEN 'NULL' ELSE 'N''' + REPLACE(ns.[SuccessEmailSubject], '''', '''''') + '''' END + ', ' +
        CASE WHEN ns.[FailureEmailSubject] IS NULL THEN 'NULL' ELSE 'N''' + REPLACE(ns.[FailureEmailSubject], '''', '''''') + '''' END + ', ' +
        CAST(ns.[IncludeExecutionDetails] AS NVARCHAR(1)) + ', ' +
        CAST(ns.[IncludeOutput] AS NVARCHAR(1)) + ', ' +
        'GETUTCDATE(), ' +
        'GETUTCDATE(), ' +
        'N''ProductionSeed'', ' +
        'N''ProductionSeed'', ' +
        '0' +
    ');' + CHAR(13) + CHAR(10) +
    'END'
FROM [dbo].[NotificationSetting] ns
    INNER JOIN [dbo].[Schedule] s ON ns.[ScheduleId] = s.[ScheduleId]
WHERE ns.[IsDeleted] = 0
    AND s.[IsDeleted] = 0
    AND s.[IsSystemSchedule] = 0  -- Exclude system schedules (auto-created on startup, IDs may differ in Production)
ORDER BY ns.[ScheduleId], ns.[NotificationSettingId];

PRINT '';
PRINT 'SET IDENTITY_INSERT [dbo].[NotificationSetting] OFF;';

SET NOCOUNT OFF;
