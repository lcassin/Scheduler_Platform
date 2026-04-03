-- ============================================================================
-- Seed Production: User and UserPermission Tables
-- ============================================================================
-- Run this AFTER Client seeding (User has FK to Client).
-- This script extracts all active Users and their Permissions from UAT.
-- ============================================================================
-- INSTRUCTIONS:
-- 1. Run the SELECT queries below against your UAT database to generate
--    INSERT statements for both User and UserPermission tables.
-- 2. Copy ALL generated INSERT statements (both sections).
-- 3. Run the generated INSERT statements against your Production database.
-- ============================================================================

-- ============================================================================
-- STEP 1A: Run this against UAT to generate User INSERT statements
-- ============================================================================

SET NOCOUNT ON;

PRINT '-- ============================================================================';
PRINT '-- Users';
PRINT '-- ============================================================================';

PRINT 'SET IDENTITY_INSERT [dbo].[User] ON;';
PRINT '';

SELECT
    'IF NOT EXISTS (SELECT 1 FROM [dbo].[User] WHERE [UserId] = ' + CAST(u.[UserId] AS NVARCHAR(20)) + ')' + CHAR(13) + CHAR(10) +
    'BEGIN' + CHAR(13) + CHAR(10) +
    '    INSERT INTO [dbo].[User] (' + CHAR(13) + CHAR(10) +
    '        [UserId], [Username], [Email], [FirstName], [LastName], [ClientId],' + CHAR(13) + CHAR(10) +
    '        [IsActive], [ExternalUserId], [ExternalIssuer], [PasswordHash],' + CHAR(13) + CHAR(10) +
    '        [IsSystemAdmin], [LastLoginDateTime], [MustChangePassword], [PasswordChangedDateTime],' + CHAR(13) + CHAR(10) +
    '        [PreferredTimeZone],' + CHAR(13) + CHAR(10) +
    '        [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted]' + CHAR(13) + CHAR(10) +
    '    )' + CHAR(13) + CHAR(10) +
    '    VALUES (' +
        CAST(u.[UserId] AS NVARCHAR(20)) + ', ' +
        'N''' + REPLACE(u.[Username], '''', '''''') + ''', ' +
        'N''' + REPLACE(u.[Email], '''', '''''') + ''', ' +
        'N''' + REPLACE(u.[FirstName], '''', '''''') + ''', ' +
        'N''' + REPLACE(u.[LastName], '''', '''''') + ''', ' +
        CAST(u.[ClientId] AS NVARCHAR(20)) + ', ' +
        CAST(u.[IsActive] AS NVARCHAR(1)) + ', ' +
        CASE WHEN u.[ExternalUserId] IS NULL THEN 'NULL' ELSE 'N''' + REPLACE(u.[ExternalUserId], '''', '''''') + '''' END + ', ' +
        CASE WHEN u.[ExternalIssuer] IS NULL THEN 'NULL' ELSE 'N''' + REPLACE(u.[ExternalIssuer], '''', '''''') + '''' END + ', ' +
        CASE WHEN u.[PasswordHash] IS NULL THEN 'NULL' ELSE 'N''' + REPLACE(u.[PasswordHash], '''', '''''') + '''' END + ', ' +
        CAST(u.[IsSystemAdmin] AS NVARCHAR(1)) + ', ' +
        'NULL, ' + -- Reset LastLoginDateTime for production
        CAST(u.[MustChangePassword] AS NVARCHAR(1)) + ', ' +
        CASE WHEN u.[PasswordChangedDateTime] IS NULL THEN 'NULL' ELSE '''' + CONVERT(NVARCHAR(30), u.[PasswordChangedDateTime], 126) + '''' END + ', ' +
        CASE WHEN u.[PreferredTimeZone] IS NULL THEN 'NULL' ELSE 'N''' + REPLACE(u.[PreferredTimeZone], '''', '''''') + '''' END + ', ' +
        'GETUTCDATE(), ' +
        'GETUTCDATE(), ' +
        'N''ProductionSeed'', ' +
        'N''ProductionSeed'', ' +
        '0' +
    ');' + CHAR(13) + CHAR(10) +
    'END'
FROM [dbo].[User] u
WHERE u.[IsDeleted] = 0
ORDER BY u.[UserId];

PRINT '';
PRINT 'SET IDENTITY_INSERT [dbo].[User] OFF;';

-- ============================================================================
-- STEP 1B: Run this against UAT to generate UserPermission INSERT statements
-- ============================================================================

PRINT '';
PRINT '-- ============================================================================';
PRINT '-- User Permissions';
PRINT '-- ============================================================================';

PRINT 'SET IDENTITY_INSERT [dbo].[UserPermission] ON;';
PRINT '';

SELECT
    'IF NOT EXISTS (SELECT 1 FROM [dbo].[UserPermission] WHERE [UserPermissionId] = ' + CAST(up.[UserPermissionId] AS NVARCHAR(20)) + ')' + CHAR(13) + CHAR(10) +
    'BEGIN' + CHAR(13) + CHAR(10) +
    '    INSERT INTO [dbo].[UserPermission] (' + CHAR(13) + CHAR(10) +
    '        [UserPermissionId], [UserId], [PermissionName], [ResourceType], [ResourceId],' + CHAR(13) + CHAR(10) +
    '        [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute],' + CHAR(13) + CHAR(10) +
    '        [CreatedDateTime], [ModifiedDateTime], [CreatedBy], [ModifiedBy], [IsDeleted]' + CHAR(13) + CHAR(10) +
    '    )' + CHAR(13) + CHAR(10) +
    '    VALUES (' +
        CAST(up.[UserPermissionId] AS NVARCHAR(20)) + ', ' +
        CAST(up.[UserId] AS NVARCHAR(20)) + ', ' +
        'N''' + REPLACE(up.[PermissionName], '''', '''''') + ''', ' +
        CASE WHEN up.[ResourceType] IS NULL THEN 'NULL' ELSE 'N''' + REPLACE(up.[ResourceType], '''', '''''') + '''' END + ', ' +
        CASE WHEN up.[ResourceId] IS NULL THEN 'NULL' ELSE CAST(up.[ResourceId] AS NVARCHAR(20)) END + ', ' +
        CAST(up.[CanCreate] AS NVARCHAR(1)) + ', ' +
        CAST(up.[CanRead] AS NVARCHAR(1)) + ', ' +
        CAST(up.[CanUpdate] AS NVARCHAR(1)) + ', ' +
        CAST(up.[CanDelete] AS NVARCHAR(1)) + ', ' +
        CAST(up.[CanExecute] AS NVARCHAR(1)) + ', ' +
        'GETUTCDATE(), ' +
        'GETUTCDATE(), ' +
        'N''ProductionSeed'', ' +
        'N''ProductionSeed'', ' +
        '0' +
    ');' + CHAR(13) + CHAR(10) +
    'END'
FROM [dbo].[UserPermission] up
    INNER JOIN [dbo].[User] u ON up.[UserId] = u.[UserId]
WHERE up.[IsDeleted] = 0
    AND u.[IsDeleted] = 0
ORDER BY up.[UserId], up.[UserPermissionId];

PRINT '';
PRINT 'SET IDENTITY_INSERT [dbo].[UserPermission] OFF;';

SET NOCOUNT OFF;
