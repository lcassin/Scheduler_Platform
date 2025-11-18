
IF NOT EXISTS (SELECT 1 FROM sys.columns 
               WHERE name = N'ExternalClientId' 
                 AND object_id = OBJECT_ID(N'[dbo].[Clients]'))
BEGIN
    PRINT 'Adding ExternalClientId column to Clients table...';
    
    ALTER TABLE [dbo].[Clients] 
      ADD [ExternalClientId] INT NOT NULL 
      CONSTRAINT DF_Clients_ExternalClientId DEFAULT(0);
    
    ALTER TABLE [dbo].[Clients] 
      DROP CONSTRAINT DF_Clients_ExternalClientId;
    
    PRINT 'ExternalClientId column added successfully.';
END
ELSE
BEGIN
    PRINT 'ExternalClientId column already exists.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes 
               WHERE name = N'IX_Clients_ExternalClientId'
                 AND object_id = OBJECT_ID(N'[dbo].[Clients]'))
BEGIN
    PRINT 'Creating index on ExternalClientId...';
    
    CREATE INDEX [IX_Clients_ExternalClientId] 
    ON [dbo].[Clients] ([ExternalClientId]);
    
    PRINT 'Index created successfully.';
END
ELSE
BEGIN
    PRINT 'Index IX_Clients_ExternalClientId already exists.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns 
               WHERE name = N'LastSyncedAt' 
                 AND object_id = OBJECT_ID(N'[dbo].[Clients]'))
BEGIN
    PRINT 'Adding LastSyncedAt column to Clients table...';
    
    ALTER TABLE [dbo].[Clients] ADD [LastSyncedAt] DATETIME2 NULL;
    
    PRINT 'LastSyncedAt column added successfully.';
END
ELSE
BEGIN
    PRINT 'LastSyncedAt column already exists.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes 
               WHERE name = N'IX_Clients_LastSyncedAt'
                 AND object_id = OBJECT_ID(N'[dbo].[Clients]'))
BEGIN
    PRINT 'Creating index on LastSyncedAt...';
    
    CREATE INDEX [IX_Clients_LastSyncedAt] 
    ON [dbo].[Clients] ([LastSyncedAt]);
    
    PRINT 'Index created successfully.';
END
ELSE
BEGIN
    PRINT 'Index IX_Clients_LastSyncedAt already exists.';
END
GO

PRINT '';
PRINT '=== Verification ===';
PRINT 'Columns in Clients table:';
SELECT 
    c.name AS ColumnName,
    t.name AS DataType,
    c.max_length AS MaxLength,
    c.is_nullable AS IsNullable
FROM sys.columns c
JOIN sys.types t ON c.user_type_id = t.user_type_id
WHERE c.object_id = OBJECT_ID(N'[dbo].[Clients]')
  AND c.name IN ('ExternalClientId', 'LastSyncedAt')
ORDER BY c.name;

PRINT '';
PRINT 'Indexes on Clients table:';
SELECT 
    i.name AS IndexName,
    COL_NAME(ic.object_id, ic.column_id) AS ColumnName
FROM sys.indexes i
JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
WHERE i.object_id = OBJECT_ID(N'[dbo].[Clients]')
  AND i.name IN ('IX_Clients_ExternalClientId', 'IX_Clients_LastSyncedAt')
ORDER BY i.name;

PRINT '';
PRINT 'Hotfix completed successfully!';
PRINT 'You can now run the ScheduleSync application.';
