IF NOT EXISTS (SELECT 1 FROM Clients WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT Clients ON;
    INSERT INTO Clients (Id, ClientName, ClientCode, IsActive, ContactEmail, CreatedAt, CreatedBy, IsDeleted)
    VALUES (1, 'Cass Information Systems', 'INTERNAL', 1, 'admin@cassinfo.com', GETUTCDATE(), 'System', 0);
    SET IDENTITY_INSERT Clients OFF;
END
ELSE
BEGIN 
    UPDATE Clients SET ClientName='Cass Information Systems',
    ClientCode='INTERNAL', IsActive=1, ContactEmail='lcassin@cassinfo.com',IsDeleted=0
    WHERE Id=1
END
GO

IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'superadmin@cassinfo.com')
BEGIN
    INSERT INTO Users (Username, Email, FirstName, LastName, ClientId, IsActive, IsSystemAdmin, CreatedAt, CreatedBy, IsDeleted)
    VALUES ('superadmin', 'superadmin@cassinfo.com', 'Super', 'Admin', 1, 1, 1, GETUTCDATE(), 'System', 0);
    
    DECLARE @SuperAdminId INT = SCOPE_IDENTITY();
    
    INSERT INTO UserPermissions (UserId, PermissionName, CanCreate, CanRead, CanUpdate, CanDelete, CanExecute, CreatedAt, CreatedBy, IsDeleted)
    VALUES 
        (@SuperAdminId, 'scheduler', 1, 1, 1, 1, 1, GETUTCDATE(), 'System', 0),
        (@SuperAdminId, 'schedules', 1, 1, 1, 1, 1, GETUTCDATE(), 'System', 0),
        (@SuperAdminId, 'jobs', 1, 1, 1, 1, 1, GETUTCDATE(), 'System', 0);
END
GO

IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'admin@cassinfo.com')
BEGIN
    INSERT INTO Users (Username, Email, FirstName, LastName, ClientId, IsActive, IsSystemAdmin, CreatedAt, CreatedBy, IsDeleted)
    VALUES ('admin', 'admin@cassinfo.com', 'Default', 'Admin', 1, 1, 0, GETUTCDATE(), 'System', 0);
    
    DECLARE @AdminId INT = SCOPE_IDENTITY();
    
    INSERT INTO UserPermissions (UserId, PermissionName, CanCreate, CanRead, CanUpdate, CanDelete, CanExecute, CreatedAt, CreatedBy, IsDeleted)
    VALUES 
        (@AdminId, 'scheduler', 1, 1, 1, 1, 1, GETUTCDATE(), 'System', 0),
        (@AdminId, 'schedules', 1, 1, 1, 1, 1, GETUTCDATE(), 'System', 0),
        (@AdminId, 'jobs', 1, 1, 1, 1, 1, GETUTCDATE(), 'System', 0);
END
GO

IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'viewer@cassinfo.com')
BEGIN
    INSERT INTO Users (Username, Email, FirstName, LastName, ClientId, IsActive, IsSystemAdmin, CreatedAt, CreatedBy, IsDeleted)
    VALUES ('viewer', 'viewer@cassinfo.com', 'View', 'Only', 1, 1, 0, GETUTCDATE(), 'System', 0);
    
    DECLARE @ViewerId INT = SCOPE_IDENTITY();
    
    INSERT INTO UserPermissions (UserId, PermissionName, CanCreate, CanRead, CanUpdate, CanDelete, CanExecute, CreatedAt, CreatedBy, IsDeleted)
    VALUES 
        (@ViewerId, 'scheduler', 0, 1, 0, 0, 0, GETUTCDATE(), 'System', 0),
        (@ViewerId, 'schedules', 0, 1, 0, 0, 0, GETUTCDATE(), 'System', 0),
        (@ViewerId, 'jobs', 0, 1, 0, 0, 0, GETUTCDATE(), 'System', 0);
END
GO

IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'editor@cassinfo.com')
BEGIN
    INSERT INTO Users (Username, Email, FirstName, LastName, ClientId, IsActive, IsSystemAdmin, CreatedAt, CreatedBy, IsDeleted)
    VALUES ('editor', 'editor@cassinfo.com', 'Schedule', 'Editor', 1, 1, 0, GETUTCDATE(), 'System', 0);
    
    DECLARE @EditorId INT = SCOPE_IDENTITY();
    
    INSERT INTO UserPermissions (UserId, PermissionName, CanCreate, CanRead, CanUpdate, CanDelete, CanExecute, CreatedAt, CreatedBy, IsDeleted)
    VALUES 
        (@EditorId, 'scheduler', 0, 1, 0, 0, 0, GETUTCDATE(), 'System', 0),
        (@EditorId, 'schedules', 1, 1, 1, 1, 1, GETUTCDATE(), 'System', 0),
        (@EditorId, 'jobs', 0, 1, 0, 0, 0, GETUTCDATE(), 'System', 0);
END
GO

PRINT 'Database seed completed successfully!';
PRINT 'Created users:';
PRINT '  - superadmin@cassinfo.com (Super Admin - cannot be modified via UI)';
PRINT '  - admin@cassinfo.com (Default Admin)';
PRINT '  - viewer@cassinfo.com (Read-only access)';
PRINT '  - editor@cassinfo.com (Can create/edit/delete schedules)';
PRINT '';
PRINT 'Permission Templates:';
PRINT '  - Viewer: scheduler:read, schedules:read, jobs:read';
PRINT '  - Editor: scheduler:read, schedules:*, jobs:read';
PRINT '  - Admin: All permissions';
PRINT '  - Super Admin: All permissions + users:manage (IsSystemAdmin=true)';

SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Users'
AND COLUMN_NAME IN ('ExternalIssuer', 'PasswordHash', 'IsSystemAdmin', 'LastLoginAt')
ORDER BY COLUMN_NAME;


