-- 

USE [SchedulerPlatform_Dev];
GO

BEGIN TRANSACTION;

PRINT 'Resetting dev user passwords...';
PRINT '';

IF EXISTS (SELECT 1 FROM [Users] WHERE [Email] = 'admin@cassinfo.com')
BEGIN
    DECLARE @DevAdminPasswordHash NVARCHAR(500) = 'AQAAAAEAACcQAAAAECNH0oZr0igG+UTX7F2NIU3JC0DSysyPWQX+KQoq/fr0XMNeqxNlxbiWtGd2LFMamA==';
    
    UPDATE [Users]
    SET [PasswordHash] = @DevAdminPasswordHash,
        [UpdatedAt] = GETUTCDATE(),
        [UpdatedBy] = 'PasswordReset'
    WHERE [Email] = 'admin@cassinfo.com';
    
    PRINT 'Reset password for admin@cassinfo.com';
END
ELSE
BEGIN
    PRINT 'WARNING: admin@cassinfo.com not found - run SETUP_DEV_USERS.sql first';
END

IF EXISTS (SELECT 1 FROM [Users] WHERE [Email] = 'editor@cassinfo.com')
BEGIN
    DECLARE @DevEditorPasswordHash NVARCHAR(500) = 'AQAAAAEAACcQAAAAEKluqq0CX8KwQmTEFtggSJWW9TXUHHg4q88ROzqeXivcBStj5DcvXZrpizNLxf00tA==';
    
    UPDATE [Users]
    SET [PasswordHash] = @DevEditorPasswordHash,
        [UpdatedAt] = GETUTCDATE(),
        [UpdatedBy] = 'PasswordReset'
    WHERE [Email] = 'editor@cassinfo.com';
    
    PRINT 'Reset password for editor@cassinfo.com';
END
ELSE
BEGIN
    PRINT 'WARNING: editor@cassinfo.com not found - run SETUP_DEV_USERS.sql first';
END

IF EXISTS (SELECT 1 FROM [Users] WHERE [Email] = 'viewer@cassinfo.com')
BEGIN
    DECLARE @DevViewerPasswordHash NVARCHAR(500) = 'AQAAAAEAACcQAAAAEMJ24UU6gnEXVgabJEJuJdqqWFhhlVn3thwWSga2ugpNzsALvI8oKw+8YLzelgfqTA==';
    
    UPDATE [Users]
    SET [PasswordHash] = @DevViewerPasswordHash,
        [UpdatedAt] = GETUTCDATE(),
        [UpdatedBy] = 'PasswordReset'
    WHERE [Email] = 'viewer@cassinfo.com';
    
    PRINT 'Reset password for viewer@cassinfo.com';
END
ELSE
BEGIN
    PRINT 'WARNING: viewer@cassinfo.com not found - run SETUP_DEV_USERS.sql first';
END

COMMIT;
GO

PRINT '';
PRINT '============================================================================';
PRINT 'Password Reset Complete!';
PRINT '============================================================================';
PRINT '';
PRINT 'Dev user passwords have been reset:';
PRINT '  - admin@cassinfo.com   / DevAdmin!2025!!';
PRINT '  - editor@cassinfo.com  / DevEditor!2025!!';
PRINT '  - viewer@cassinfo.com  / DevViewer!2025!!';
PRINT '';
PRINT 'IMPORTANT: Password hashes are placeholders!';
PRINT 'You must generate real BCrypt hashes and update this script before using.';
PRINT '';
PRINT 'To generate password hashes:';
PRINT '  1. Create a small C# console app';
PRINT '  2. Add Microsoft.AspNetCore.Identity package';
PRINT '  3. Use: new PasswordHasher<User>().HashPassword(null, "YourPassword")';
PRINT '  4. Replace PLACEHOLDER_HASH_FOR_* with the generated hash';
PRINT '============================================================================';
