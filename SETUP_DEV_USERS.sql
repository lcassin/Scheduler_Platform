-- 
-- 

USE [SchedulerPlatform];
GO

BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM [Clients] WHERE [ClientCode] = 'INTERNAL')
BEGIN
    INSERT INTO [Clients] ([ClientName], [ClientCode], [IsActive], [ContactEmail], [CreatedAt], [CreatedBy], [IsDeleted])
    VALUES (N'Internal', N'INTERNAL', 1, N'admin@cassinfo.com', GETUTCDATE(), N'System', 0);
    PRINT 'Created INTERNAL client';
END

DECLARE @ClientId INT = (SELECT [Id] FROM [Clients] WHERE [ClientCode] = 'INTERNAL');

IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Email] = 'dev-admin@cassinfo.com')
BEGIN
    DECLARE @DevAdminPasswordHash NVARCHAR(500) = 'PLACEHOLDER_HASH_FOR_DevAdmin!2025!!';
    
    INSERT INTO [Users] ([Username], [Email], [FirstName], [LastName], [ClientId], [IsActive], [IsSystemAdmin], [PasswordHash], [CreatedAt], [CreatedBy], [IsDeleted])
    VALUES (N'dev-admin', N'dev-admin@cassinfo.com', N'Dev', N'Admin', @ClientId, 1, 1, @DevAdminPasswordHash, GETUTCDATE(), N'DevSetup', 0);
    
    DECLARE @DevAdminId INT = SCOPE_IDENTITY();
    
    INSERT INTO [UserPermissions] ([UserId], [PermissionName], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedAt], [CreatedBy], [IsDeleted])
    VALUES 
        (@DevAdminId, N'scheduler', 1, 1, 1, 1, 1, GETUTCDATE(), N'DevSetup', 0),
        (@DevAdminId, N'schedules', 1, 1, 1, 1, 1, GETUTCDATE(), N'DevSetup', 0),
        (@DevAdminId, N'jobs', 1, 1, 1, 1, 1, GETUTCDATE(), N'DevSetup', 0);
    
    PRINT 'Created dev-admin@cassinfo.com (Super Admin)';
END
ELSE
BEGIN
    PRINT 'dev-admin@cassinfo.com already exists - skipping';
END

IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Email] = 'dev-editor@cassinfo.com')
BEGIN
    DECLARE @DevEditorPasswordHash NVARCHAR(500) = 'PLACEHOLDER_HASH_FOR_DevEditor!2025!!';
    
    INSERT INTO [Users] ([Username], [Email], [FirstName], [LastName], [ClientId], [IsActive], [IsSystemAdmin], [PasswordHash], [CreatedAt], [CreatedBy], [IsDeleted])
    VALUES (N'dev-editor', N'dev-editor@cassinfo.com', N'Dev', N'Editor', @ClientId, 1, 0, @DevEditorPasswordHash, GETUTCDATE(), N'DevSetup', 0);
    
    DECLARE @DevEditorId INT = SCOPE_IDENTITY();
    
    INSERT INTO [UserPermissions] ([UserId], [PermissionName], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedAt], [CreatedBy], [IsDeleted])
    VALUES 
        (@DevEditorId, N'scheduler', 0, 1, 0, 0, 0, GETUTCDATE(), N'DevSetup', 0),
        (@DevEditorId, N'schedules', 1, 1, 1, 1, 1, GETUTCDATE(), N'DevSetup', 0),
        (@DevEditorId, N'jobs', 0, 1, 0, 0, 0, GETUTCDATE(), N'DevSetup', 0);
    
    PRINT 'Created dev-editor@cassinfo.com (Editor)';
END
ELSE
BEGIN
    PRINT 'dev-editor@cassinfo.com already exists - skipping';
END

IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Email] = 'dev-viewer@cassinfo.com')
BEGIN
    DECLARE @DevViewerPasswordHash NVARCHAR(500) = 'PLACEHOLDER_HASH_FOR_DevViewer!2025!!';
    
    INSERT INTO [Users] ([Username], [Email], [FirstName], [LastName], [ClientId], [IsActive], [IsSystemAdmin], [PasswordHash], [CreatedAt], [CreatedBy], [IsDeleted])
    VALUES (N'dev-viewer', N'dev-viewer@cassinfo.com', N'Dev', N'Viewer', @ClientId, 1, 0, @DevViewerPasswordHash, GETUTCDATE(), N'DevSetup', 0);
    
    DECLARE @DevViewerId INT = SCOPE_IDENTITY();
    
    INSERT INTO [UserPermissions] ([UserId], [PermissionName], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedAt], [CreatedBy], [IsDeleted])
    VALUES 
        (@DevViewerId, N'scheduler', 0, 1, 0, 0, 0, GETUTCDATE(), N'DevSetup', 0),
        (@DevViewerId, N'schedules', 0, 1, 0, 0, 0, GETUTCDATE(), N'DevSetup', 0),
        (@DevViewerId, N'jobs', 0, 1, 0, 0, 0, GETUTCDATE(), N'DevSetup', 0);
    
    PRINT 'Created dev-viewer@cassinfo.com (Viewer)';
END
ELSE
BEGIN
    PRINT 'dev-viewer@cassinfo.com already exists - skipping';
END

COMMIT;
GO

PRINT '';
PRINT '============================================================================';
PRINT 'Dev User Setup Complete!';
PRINT '============================================================================';
PRINT '';
PRINT 'Created dev users (if they did not already exist):';
PRINT '  - dev-admin@cassinfo.com   / DevAdmin!2025!!   (Super Admin - Full Access)';
PRINT '  - dev-editor@cassinfo.com  / DevEditor!2025!!  (Editor - Can CRUD Schedules)';
PRINT '  - dev-viewer@cassinfo.com  / DevViewer!2025!!  (Viewer - Read Only)';
PRINT '';
PRINT 'IMPORTANT: Password hashes are placeholders!';
PRINT 'You must generate real BCrypt hashes and update this script before using.';
PRINT '';
PRINT 'To generate password hashes:';
PRINT '  1. Create a small C# console app';
PRINT '  2. Add Microsoft.AspNetCore.Identity package';
PRINT '  3. Use: new PasswordHasher<User>().HashPassword(null, "YourPassword")';
PRINT '  4. Replace PLACEHOLDER_HASH_FOR_* with the generated hash';
PRINT '';
PRINT 'Database Refresh Workflow:';
PRINT '  1. Restore production backup to DEV/UAT database';
PRINT '  2. Run this script: SETUP_DEV_USERS.sql';
PRINT '  3. Optionally run: RESET_DEV_PASSWORDS.sql (if passwords need reset)';
PRINT '============================================================================';
