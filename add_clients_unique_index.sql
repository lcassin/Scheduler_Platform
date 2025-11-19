
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Clients_ExternalClientId_Unique' AND object_id = OBJECT_ID('dbo.Clients'))
BEGIN
    IF EXISTS (
        SELECT ExternalClientId, COUNT(*) 
        FROM dbo.Clients 
        WHERE IsDeleted = 0 
        GROUP BY ExternalClientId 
        HAVING COUNT(*) > 1
    )
    BEGIN
        PRINT 'WARNING: Duplicate ExternalClientIds found. These must be resolved before creating unique index:'
        SELECT ExternalClientId, COUNT(*) as DuplicateCount, STRING_AGG(CAST(Id AS VARCHAR), ', ') as ClientIds
        FROM dbo.Clients 
        WHERE IsDeleted = 0 
        GROUP BY ExternalClientId 
        HAVING COUNT(*) > 1
        ORDER BY DuplicateCount DESC;
        
        PRINT ''
        PRINT 'To resolve duplicates, you can:'
        PRINT '1. Soft-delete duplicate records: UPDATE Clients SET IsDeleted = 1 WHERE Id IN (...)'
        PRINT '2. Or merge schedules to one client and delete the others'
        PRINT ''
        RAISERROR('Cannot create unique index while duplicates exist', 16, 1)
        RETURN
    END

    CREATE UNIQUE NONCLUSTERED INDEX IX_Clients_ExternalClientId_Unique
    ON dbo.Clients (ExternalClientId)
    WHERE IsDeleted = 0;
    
    PRINT 'Successfully created unique index IX_Clients_ExternalClientId_Unique'
END
ELSE
BEGIN
    PRINT 'Index IX_Clients_ExternalClientId_Unique already exists'
END
GO
