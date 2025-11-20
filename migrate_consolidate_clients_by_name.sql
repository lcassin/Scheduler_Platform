-- 
--

SET NOCOUNT ON;
GO

PRINT '========================================';
PRINT 'Step 1: Analyzing duplicate clients...';
PRINT '========================================';
PRINT '';

IF OBJECT_ID('tempdb..#ClientConsolidation') IS NOT NULL
    DROP TABLE #ClientConsolidation;

CREATE TABLE #ClientConsolidation (
    OldClientId BIGINT,
    NewClientId BIGINT,
    ClientName NVARCHAR(255),
    IsCanonical BIT
);

INSERT INTO #ClientConsolidation (OldClientId, NewClientId, ClientName, IsCanonical)
SELECT 
    c.Id as OldClientId,
    canonical.CanonicalId as NewClientId,
    c.ClientName,
    CASE WHEN c.Id = canonical.CanonicalId THEN 1 ELSE 0 END as IsCanonical
FROM dbo.Clients c
INNER JOIN (
    SELECT 
        ClientName,
        MAX(Id) as CanonicalId  -- Choose most recent Id as canonical
    FROM dbo.Clients
    WHERE IsDeleted = 0
    GROUP BY ClientName
) canonical ON c.ClientName = canonical.ClientName
WHERE c.IsDeleted = 0;

DECLARE @TotalClients INT = (SELECT COUNT(DISTINCT OldClientId) FROM #ClientConsolidation);
DECLARE @UniqueNames INT = (SELECT COUNT(DISTINCT ClientName) FROM #ClientConsolidation);
DECLARE @DuplicateClients INT = @TotalClients - @UniqueNames;

PRINT 'Total client records: ' + CAST(@TotalClients AS VARCHAR);
PRINT 'Unique client names: ' + CAST(@UniqueNames AS VARCHAR);
PRINT 'Duplicate records to consolidate: ' + CAST(@DuplicateClients AS VARCHAR);
PRINT '';

PRINT 'Top 10 clients with most duplicates:';
SELECT TOP 10 
    ClientName,
    COUNT(*) as DuplicateCount,
    STRING_AGG(CAST(OldClientId AS VARCHAR), ', ') as ClientIds
FROM #ClientConsolidation
GROUP BY ClientName
HAVING COUNT(*) > 1
ORDER BY COUNT(*) DESC;
PRINT '';

PRINT '========================================';
PRINT 'Step 2: Checking for schedule collisions...';
PRINT '========================================';
PRINT '';

IF OBJECT_ID('tempdb..#ScheduleCollisions') IS NOT NULL
    DROP TABLE #ScheduleCollisions;

CREATE TABLE #ScheduleCollisions (
    ClientName NVARCHAR(255),
    ScheduleName NVARCHAR(255),
    CollisionCount INT,
    ScheduleIds NVARCHAR(MAX)
);

INSERT INTO #ScheduleCollisions
SELECT 
    cc.ClientName,
    s.Name as ScheduleName,
    COUNT(*) as CollisionCount,
    STRING_AGG(CAST(s.Id AS VARCHAR), ', ') as ScheduleIds
FROM dbo.Schedules s
INNER JOIN #ClientConsolidation cc ON s.ClientId = cc.OldClientId
WHERE s.IsDeleted = 0
GROUP BY cc.ClientName, s.Name
HAVING COUNT(*) > 1;

DECLARE @CollisionCount INT = (SELECT COUNT(*) FROM #ScheduleCollisions);

IF @CollisionCount > 0
BEGIN
    PRINT 'WARNING: Found ' + CAST(@CollisionCount AS VARCHAR) + ' schedule name collisions!';
    PRINT 'These schedules have the same ClientName + ScheduleName and will need to be merged:';
    PRINT '';
    SELECT TOP 20 * FROM #ScheduleCollisions ORDER BY CollisionCount DESC;
    PRINT '';
    PRINT 'Strategy: Keep the schedule with most recent UpdatedAt, soft-delete others';
    PRINT '';
END
ELSE
BEGIN
    PRINT 'No schedule collisions found - safe to proceed!';
    PRINT '';
END

PRINT '========================================';
PRINT 'Step 3: Updating schedules to canonical Client.Id...';
PRINT '========================================';
PRINT '';

DECLARE @BatchSize INT = 10000;
DECLARE @TotalSchedules INT;
DECLARE @UpdatedSchedules INT = 0;
DECLARE @LastId BIGINT = 0;
DECLARE @StartTime DATETIME = GETDATE();

SELECT @TotalSchedules = COUNT(*)
FROM dbo.Schedules s
INNER JOIN #ClientConsolidation cc ON s.ClientId = cc.OldClientId
WHERE s.IsDeleted = 0 AND cc.IsCanonical = 0;

PRINT 'Total schedules to update: ' + CAST(@TotalSchedules AS VARCHAR);
PRINT 'Batch size: ' + CAST(@BatchSize AS VARCHAR);
PRINT '';

WHILE @UpdatedSchedules < @TotalSchedules
BEGIN
    UPDATE TOP (@BatchSize) s
    SET 
        s.ClientId = cc.NewClientId,
        s.UpdatedAt = GETDATE(),
        s.UpdatedBy = 'ClientConsolidation'
    FROM dbo.Schedules s
    INNER JOIN #ClientConsolidation cc ON s.ClientId = cc.OldClientId
    WHERE s.IsDeleted = 0 
        AND cc.IsCanonical = 0
        AND s.Id > @LastId;
    
    DECLARE @RowsUpdated INT = @@ROWCOUNT;
    SET @UpdatedSchedules = @UpdatedSchedules + @RowsUpdated;
    
    SELECT @LastId = MAX(s.Id)
    FROM dbo.Schedules s
    INNER JOIN #ClientConsolidation cc ON s.ClientId = cc.OldClientId
    WHERE s.IsDeleted = 0 AND cc.IsCanonical = 0 AND s.Id <= @LastId + @BatchSize;
    
    DECLARE @ElapsedSeconds INT = DATEDIFF(SECOND, @StartTime, GETDATE());
    DECLARE @PercentComplete DECIMAL(5,2) = (@UpdatedSchedules * 100.0) / @TotalSchedules;
    DECLARE @SchedulesPerSecond DECIMAL(10,2) = CASE WHEN @ElapsedSeconds > 0 THEN @UpdatedSchedules * 1.0 / @ElapsedSeconds ELSE 0 END;
    DECLARE @EstimatedSecondsRemaining INT = CASE WHEN @SchedulesPerSecond > 0 THEN (@TotalSchedules - @UpdatedSchedules) / @SchedulesPerSecond ELSE 0 END;
    
    PRINT 'Progress: ' + CAST(@UpdatedSchedules AS VARCHAR) + '/' + CAST(@TotalSchedules AS VARCHAR) + 
          ' (' + CAST(@PercentComplete AS VARCHAR) + '%) | ' +
          'Rate: ' + CAST(@SchedulesPerSecond AS VARCHAR) + ' schedules/sec | ' +
          'ETA: ' + CAST(@EstimatedSecondsRemaining / 60 AS VARCHAR) + ' min';
    
    IF @RowsUpdated = 0
        BREAK;
END

PRINT '';
PRINT 'Schedules updated: ' + CAST(@UpdatedSchedules AS VARCHAR);
PRINT 'Duration: ' + CAST(DATEDIFF(SECOND, @StartTime, GETDATE()) AS VARCHAR) + ' seconds';
PRINT '';

IF @CollisionCount > 0
BEGIN
    PRINT '========================================';
    PRINT 'Step 4: Resolving schedule collisions...';
    PRINT '========================================';
    PRINT '';
    
    DECLARE @ResolvedCollisions INT = 0;
    
    UPDATE s
    SET 
        s.IsDeleted = 1,
        s.UpdatedAt = GETDATE(),
        s.UpdatedBy = 'ClientConsolidation'
    FROM dbo.Schedules s
    INNER JOIN (
        SELECT 
            s2.ClientId,
            s2.Name,
            s2.Id
        FROM dbo.Schedules s2
        INNER JOIN (
            SELECT 
                ClientId,
                Name,
                MAX(UpdatedAt) as MaxUpdatedAt
            FROM dbo.Schedules
            WHERE IsDeleted = 0
            GROUP BY ClientId, Name
            HAVING COUNT(*) > 1
        ) latest ON s2.ClientId = latest.ClientId 
            AND s2.Name = latest.Name 
            AND s2.UpdatedAt < latest.MaxUpdatedAt
        WHERE s2.IsDeleted = 0
    ) duplicates ON s.Id = duplicates.Id;
    
    SET @ResolvedCollisions = @@ROWCOUNT;
    
    PRINT 'Resolved collisions (soft-deleted duplicate schedules): ' + CAST(@ResolvedCollisions AS VARCHAR);
    PRINT '';
END

PRINT '========================================';
PRINT 'Step 5: Soft-deleting duplicate clients...';
PRINT '========================================';
PRINT '';

UPDATE c
SET 
    c.IsDeleted = 1,
    c.UpdatedAt = GETDATE(),
    c.UpdatedBy = 'ClientConsolidation'
FROM dbo.Clients c
INNER JOIN #ClientConsolidation cc ON c.Id = cc.OldClientId
WHERE cc.IsCanonical = 0;

DECLARE @DeletedClients INT = @@ROWCOUNT;

PRINT 'Soft-deleted duplicate clients: ' + CAST(@DeletedClients AS VARCHAR);
PRINT '';

PRINT '========================================';
PRINT 'Step 6: Adding unique index on ClientName...';
PRINT '========================================';
PRINT '';

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Clients_ClientName_Unique' AND object_id = OBJECT_ID('dbo.Clients'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX IX_Clients_ClientName_Unique
    ON dbo.Clients (ClientName)
    WHERE IsDeleted = 0;
    
    PRINT 'Successfully created unique index IX_Clients_ClientName_Unique';
END
ELSE
BEGIN
    PRINT 'Index IX_Clients_ClientName_Unique already exists';
END
PRINT '';

PRINT '========================================';
PRINT 'CONSOLIDATION COMPLETE!';
PRINT '========================================';
PRINT '';
PRINT 'Summary:';
PRINT '  - Consolidated ' + CAST(@DuplicateClients AS VARCHAR) + ' duplicate clients';
PRINT '  - Updated ' + CAST(@UpdatedSchedules AS VARCHAR) + ' schedules';
IF @CollisionCount > 0
    PRINT '  - Resolved ' + CAST(@ResolvedCollisions AS VARCHAR) + ' schedule collisions';
PRINT '  - Soft-deleted ' + CAST(@DeletedClients AS VARCHAR) + ' duplicate client records';
PRINT '  - Added unique index on ClientName';
PRINT '';
PRINT 'Next steps:';
PRINT '  1. Update sync logic to use ClientName instead of ExternalClientId';
PRINT '  2. Remove ExternalClientId column from Clients table';
PRINT '  3. Test schedule execution';
PRINT '';

DROP TABLE #ClientConsolidation;
DROP TABLE #ScheduleCollisions;

PRINT 'Done!';
GO
