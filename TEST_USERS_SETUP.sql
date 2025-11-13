
BEGIN TRANSACTION;

DECLARE @ClientId INT = (SELECT TOP 1 [Id] FROM [Clients] WHERE [ClientCode] = 'INTERNAL');

IF @ClientId IS NULL
BEGIN
    INSERT INTO [Clients] ([ClientName], [ClientCode], [IsActive], [ContactEmail], [CreatedAt], [CreatedBy], [IsDeleted])
    VALUES (N'Internal', N'INTERNAL', 1, N'admin@company.com', GETUTCDATE(), N'System', 0);
    
    SET @ClientId = SCOPE_IDENTITY();
END

IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Email] = 'admin@example.com')
BEGIN
    INSERT INTO [Users] ([Username], [Email], [FirstName], [LastName], [ClientId], [IsActive], [IsSystemAdmin], [CreatedAt], [CreatedBy], [IsDeleted])
    VALUES (N'admin', N'admin@example.com', N'Admin', N'User', @ClientId, 1, 1, GETUTCDATE(), N'System', 0);
    
    DECLARE @AdminId INT = SCOPE_IDENTITY();
    
    INSERT INTO [UserPermissions] ([UserId], [PermissionName], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedAt], [CreatedBy], [IsDeleted])
    VALUES 
        (@AdminId, N'scheduler', 1, 1, 1, 1, 1, GETUTCDATE(), N'System', 0),
        (@AdminId, N'schedules', 1, 1, 1, 1, 1, GETUTCDATE(), N'System', 0),
        (@AdminId, N'jobs', 1, 1, 1, 1, 1, GETUTCDATE(), N'System', 0);
    
    PRINT 'Created test admin user: admin@example.com';
END
ELSE
BEGIN
    PRINT 'Test admin user already exists: admin@example.com';
END

IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Email] = 'client@example.com')
BEGIN
    INSERT INTO [Users] ([Username], [Email], [FirstName], [LastName], [ClientId], [IsActive], [IsSystemAdmin], [CreatedAt], [CreatedBy], [IsDeleted])
    VALUES (N'client1', N'client@example.com', N'Client', N'User', @ClientId, 1, 0, GETUTCDATE(), N'System', 0);
    
    DECLARE @ClientUserId INT = SCOPE_IDENTITY();
    
    INSERT INTO [UserPermissions] ([UserId], [PermissionName], [CanCreate], [CanRead], [CanUpdate], [CanDelete], [CanExecute], [CreatedAt], [CreatedBy], [IsDeleted])
    VALUES 
        (@ClientUserId, N'scheduler', 0, 1, 0, 0, 0, GETUTCDATE(), N'System', 0),
        (@ClientUserId, N'schedules', 0, 1, 0, 0, 0, GETUTCDATE(), N'System', 0),
        (@ClientUserId, N'jobs', 0, 1, 0, 0, 0, GETUTCDATE(), N'System', 0);
    
    PRINT 'Created test client user: client@example.com';
END
ELSE
BEGIN
    PRINT 'Test client user already exists: client@example.com';
END

COMMIT;
GO

PRINT '';
PRINT '==============================================';
PRINT 'Test Users Setup Complete!';
PRINT '==============================================';
PRINT '';
PRINT 'You can now log in with:';
PRINT '  Username: admin';
PRINT '  Password: Admin123!';
PRINT '  (Full admin permissions from database)';
PRINT '';
PRINT '  Username: client1';
PRINT '  Password: Client123!';
PRINT '  (Read-only permissions from database)';
PRINT '';
PRINT 'IMPORTANT: These test users only work in Development/Staging/UAT environments.';
PRINT 'They are automatically disabled in Production for security.';
PRINT '==============================================';
