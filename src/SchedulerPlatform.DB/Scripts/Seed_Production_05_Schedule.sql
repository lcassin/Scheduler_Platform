-- ============================================================================
-- Seed Production: Schedule Table and Quartz Records
-- ============================================================================
-- Run this AFTER Client seeding (Schedule has FK to Client).
--
-- NOTE: System schedules (IsSystemSchedule = 1) like "System Maintenance",
-- "ADR Full Cycle", and "ADR Status Check" are NOT included here because
-- they are automatically created by the SystemScheduleSeeder on API startup.
-- This script only seeds non-system (user-created) schedules.
--
-- The Quartz records (QRTZ_JOB_DETAILS, QRTZ_TRIGGERS, QRTZ_CRON_TRIGGERS)
-- are generated to match each schedule so they are immediately operational.
-- The naming convention follows the application code:
--   Job Name:     Job_{ScheduleId}
--   Trigger Name: Trigger_{ScheduleId}
--   Group Name:   Group_{ClientId}
--   Sched Name:   QuartzScheduler (Quartz.NET default)
-- ============================================================================
-- INSTRUCTIONS:
-- 1. Run the SELECT queries below against your UAT database to generate
--    INSERT statements for Schedule, QRTZ_JOB_DETAILS, QRTZ_TRIGGERS,
--    and QRTZ_CRON_TRIGGERS.
-- 2. Copy ALL generated INSERT statements (all sections).
-- 3. Run the generated INSERT statements against your Production database.
-- 4. After running, restart the API application so Quartz picks up the
--    seeded triggers.
-- ============================================================================

-- ============================================================================
-- STEP 1A: Run this against UAT to generate Schedule INSERT statements
-- (Excludes system schedules - those are auto-created on startup)
-- ============================================================================

SET NOCOUNT ON;

PRINT '-- ============================================================================';
PRINT '-- Schedules (non-system only)';
PRINT '-- ============================================================================';

PRINT 'SET IDENTITY_INSERT [dbo].[Schedule] ON;';
PRINT '';

SELECT
    'IF NOT EXISTS (SELECT 1 FROM [dbo].[Schedule] WHERE [ScheduleId] = ' + CAST(s.[ScheduleId] AS NVARCHAR(20)) + ')' + CHAR(13) + CHAR(10) +
    'BEGIN' + CHAR(13) + CHAR(10) +
    '    INSERT INTO [dbo].[Schedule] (' + CHAR(13) + CHAR(10) +
    '        [ScheduleId], [Name], [Description], [ClientId], [JobType], [Frequency],' + CHAR(13) + CHAR(10) +
    '        [CronExpression], [NextRunDateTime], [LastRunDateTime], [IsEnabled],' + CHAR(13) + CHAR(10) +
    '        [MaxRetries], [RetryDelayMinutes], [TimeoutMinutes], [TimeZone],' + CHAR(13) + CHAR(10) +
    '        [JobConfiguration], [CreatedDateTime], [ModifiedDateTime],' + CHAR(13) + CHAR(10) +
    '        [CreatedBy], [ModifiedBy], [IsDeleted], [IsSystemSchedule]' + CHAR(13) + CHAR(10) +
    '    )' + CHAR(13) + CHAR(10) +
    '    VALUES (' +
        CAST(s.[ScheduleId] AS NVARCHAR(20)) + ', ' +
        'N''' + REPLACE(s.[Name], '''', '''''') + ''', ' +
        'N''' + REPLACE(s.[Description], '''', '''''') + ''', ' +
        CAST(s.[ClientId] AS NVARCHAR(20)) + ', ' +
        CAST(s.[JobType] AS NVARCHAR(10)) + ', ' +
        CAST(s.[Frequency] AS NVARCHAR(10)) + ', ' +
        'N''' + REPLACE(s.[CronExpression], '''', '''''') + ''', ' +
        'NULL, ' + -- NextRunDateTime will be calculated by Quartz on first fire
        'NULL, ' + -- LastRunDateTime reset for production
        CAST(s.[IsEnabled] AS NVARCHAR(1)) + ', ' +
        CAST(s.[MaxRetries] AS NVARCHAR(10)) + ', ' +
        CAST(s.[RetryDelayMinutes] AS NVARCHAR(10)) + ', ' +
        CASE WHEN s.[TimeoutMinutes] IS NULL THEN 'NULL' ELSE CAST(s.[TimeoutMinutes] AS NVARCHAR(10)) END + ', ' +
        CASE WHEN s.[TimeZone] IS NULL THEN 'NULL' ELSE 'N''' + REPLACE(s.[TimeZone], '''', '''''') + '''' END + ', ' +
        CASE WHEN s.[JobConfiguration] IS NULL THEN 'NULL' ELSE 'N''' + REPLACE(s.[JobConfiguration], '''', '''''') + '''' END + ', ' +
        'GETUTCDATE(), ' +
        'GETUTCDATE(), ' +
        'N''ProductionSeed'', ' +
        'N''ProductionSeed'', ' +
        '0, ' +
        CAST(s.[IsSystemSchedule] AS NVARCHAR(1)) +
    ');' + CHAR(13) + CHAR(10) +
    'END'
FROM [dbo].[Schedule] s
WHERE s.[IsDeleted] = 0
    AND s.[IsSystemSchedule] = 0  -- Exclude system schedules (auto-created on startup)
ORDER BY s.[ScheduleId];

PRINT '';
PRINT 'SET IDENTITY_INSERT [dbo].[Schedule] OFF;';

-- ============================================================================
-- STEP 1B: Run this against UAT to generate QRTZ_JOB_DETAILS INSERT statements
-- Only for enabled, non-system schedules
-- ============================================================================

PRINT '';
PRINT '-- ============================================================================';
PRINT '-- Quartz Job Details';
PRINT '-- ============================================================================';

-- Map JobType enum to .NET class names used by the application
-- JobType: 1=Process, 2=StoredProcedure, 3=ApiCall, 4=Maintenance
SELECT
    'IF NOT EXISTS (SELECT 1 FROM [dbo].[QRTZ_JOB_DETAILS] WHERE [SCHED_NAME] = N''QuartzScheduler'' AND [JOB_NAME] = N''Job_' + CAST(s.[ScheduleId] AS NVARCHAR(20)) + ''' AND [JOB_GROUP] = N''Group_' + CAST(s.[ClientId] AS NVARCHAR(20)) + ''')' + CHAR(13) + CHAR(10) +
    'BEGIN' + CHAR(13) + CHAR(10) +
    '    INSERT INTO [dbo].[QRTZ_JOB_DETAILS] ([SCHED_NAME], [JOB_NAME], [JOB_GROUP], [DESCRIPTION], [JOB_CLASS_NAME], [IS_DURABLE], [IS_NONCONCURRENT], [IS_UPDATE_DATA], [REQUESTS_RECOVERY], [JOB_DATA])' + CHAR(13) + CHAR(10) +
    '    VALUES (' +
        'N''QuartzScheduler'', ' +
        'N''Job_' + CAST(s.[ScheduleId] AS NVARCHAR(20)) + ''', ' +
        'N''Group_' + CAST(s.[ClientId] AS NVARCHAR(20)) + ''', ' +
        'NULL, ' +
        'N''' +
            CASE s.[JobType]
                WHEN 1 THEN 'SchedulerPlatform.Jobs.Jobs.ProcessJob, SchedulerPlatform.Jobs'
                WHEN 2 THEN 'SchedulerPlatform.Jobs.Jobs.StoredProcedureJob, SchedulerPlatform.Jobs'
                WHEN 3 THEN 'SchedulerPlatform.Jobs.Jobs.ApiCallJob, SchedulerPlatform.Jobs'
                WHEN 4 THEN 'SchedulerPlatform.Jobs.Jobs.MaintenanceJob, SchedulerPlatform.Jobs'
            END + ''', ' +
        '1, ' + -- IS_DURABLE (StoreDurably)
        '0, ' + -- IS_NONCONCURRENT
        '0, ' + -- IS_UPDATE_DATA
        '0, ' + -- REQUESTS_RECOVERY
        'NULL' + -- JOB_DATA (job data is passed via trigger, not stored on job detail when using properties)
    ');' + CHAR(13) + CHAR(10) +
    'END' AS [--SqlStatement]
FROM [dbo].[Schedule] s
WHERE s.[IsDeleted] = 0
    AND s.[IsSystemSchedule] = 0
    AND s.[IsEnabled] = 1
ORDER BY s.[ScheduleId];

-- ============================================================================
-- STEP 1C: Run this against UAT to generate QRTZ_TRIGGERS INSERT statements
-- ============================================================================

PRINT '';
PRINT '-- ============================================================================';
PRINT '-- Quartz Triggers';
PRINT '-- ============================================================================';

SELECT
    'IF NOT EXISTS (SELECT 1 FROM [dbo].[QRTZ_TRIGGERS] WHERE [SCHED_NAME] = N''QuartzScheduler'' AND [TRIGGER_NAME] = N''Trigger_' + CAST(s.[ScheduleId] AS NVARCHAR(20)) + ''' AND [TRIGGER_GROUP] = N''Group_' + CAST(s.[ClientId] AS NVARCHAR(20)) + ''')' + CHAR(13) + CHAR(10) +
    'BEGIN' + CHAR(13) + CHAR(10) +
    '    INSERT INTO [dbo].[QRTZ_TRIGGERS] (' + CHAR(13) + CHAR(10) +
    '        [SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP], [JOB_NAME], [JOB_GROUP],' + CHAR(13) + CHAR(10) +
    '        [DESCRIPTION], [NEXT_FIRE_TIME], [PREV_FIRE_TIME], [PRIORITY], [TRIGGER_STATE],' + CHAR(13) + CHAR(10) +
    '        [TRIGGER_TYPE], [START_TIME], [END_TIME], [CALENDAR_NAME], [MISFIRE_INSTR], [JOB_DATA]' + CHAR(13) + CHAR(10) +
    '    )' + CHAR(13) + CHAR(10) +
    '    VALUES (' +
        'N''QuartzScheduler'', ' +
        'N''Trigger_' + CAST(s.[ScheduleId] AS NVARCHAR(20)) + ''', ' +
        'N''Group_' + CAST(s.[ClientId] AS NVARCHAR(20)) + ''', ' +
        'N''Job_' + CAST(s.[ScheduleId] AS NVARCHAR(20)) + ''', ' +
        'N''Group_' + CAST(s.[ClientId] AS NVARCHAR(20)) + ''', ' +
        'NULL, ' + -- DESCRIPTION
        'NULL, ' + -- NEXT_FIRE_TIME (Quartz will calculate on scheduler start)
        'NULL, ' + -- PREV_FIRE_TIME
        '5, ' +    -- PRIORITY (default)
        'N''WAITING'', ' + -- TRIGGER_STATE
        'N''CRON'', ' +    -- TRIGGER_TYPE
        'CAST(DATEDIFF_BIG(MILLISECOND, ''1970-01-01'', GETUTCDATE()) AS BIGINT), ' + -- START_TIME (epoch ms)
        'NULL, ' + -- END_TIME
        'NULL, ' + -- CALENDAR_NAME
        '2, ' +    -- MISFIRE_INSTR (2 = DoNothing, matches WithMisfireHandlingInstructionDoNothing)
        'NULL' +   -- JOB_DATA
    ');' + CHAR(13) + CHAR(10) +
    'END' AS [--SqlStatement]
FROM [dbo].[Schedule] s
WHERE s.[IsDeleted] = 0
    AND s.[IsSystemSchedule] = 0
    AND s.[IsEnabled] = 1
ORDER BY s.[ScheduleId];

-- ============================================================================
-- STEP 1D: Run this against UAT to generate QRTZ_CRON_TRIGGERS INSERT statements
-- ============================================================================

PRINT '';
PRINT '-- ============================================================================';
PRINT '-- Quartz Cron Triggers';
PRINT '-- ============================================================================';

SELECT
    'IF NOT EXISTS (SELECT 1 FROM [dbo].[QRTZ_CRON_TRIGGERS] WHERE [SCHED_NAME] = N''QuartzScheduler'' AND [TRIGGER_NAME] = N''Trigger_' + CAST(s.[ScheduleId] AS NVARCHAR(20)) + ''' AND [TRIGGER_GROUP] = N''Group_' + CAST(s.[ClientId] AS NVARCHAR(20)) + ''')' + CHAR(13) + CHAR(10) +
    'BEGIN' + CHAR(13) + CHAR(10) +
    '    INSERT INTO [dbo].[QRTZ_CRON_TRIGGERS] ([SCHED_NAME], [TRIGGER_NAME], [TRIGGER_GROUP], [CRON_EXPRESSION], [TIME_ZONE_ID])' + CHAR(13) + CHAR(10) +
    '    VALUES (' +
        'N''QuartzScheduler'', ' +
        'N''Trigger_' + CAST(s.[ScheduleId] AS NVARCHAR(20)) + ''', ' +
        'N''Group_' + CAST(s.[ClientId] AS NVARCHAR(20)) + ''', ' +
        'N''' + REPLACE(s.[CronExpression], '''', '''''') + ''', ' +
        CASE WHEN s.[TimeZone] IS NULL THEN 'N''UTC''' ELSE 'N''' + REPLACE(s.[TimeZone], '''', '''''') + '''' END +
    ');' + CHAR(13) + CHAR(10) +
    'END' AS [--SqlStatement]
FROM [dbo].[Schedule] s
WHERE s.[IsDeleted] = 0
    AND s.[IsSystemSchedule] = 0
    AND s.[IsEnabled] = 1
ORDER BY s.[ScheduleId];

SET NOCOUNT OFF;
