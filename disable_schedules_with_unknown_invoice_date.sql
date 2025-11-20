--
--

SET NOCOUNT ON;
GO

DECLARE @DefaultDate DATETIME2(7) = '0001-01-01T00:00:00';
DECLARE @UpdatedBy NVARCHAR(100) = 'DisableUnknownInvoiceDateScript';
DECLARE @UpdatedAt DATETIME2(7) = SYSUTCDATETIME();
DECLARE @RowsAffected INT = 0;
DECLARE @SourceCount INT = 0;
DECLARE @DryRun BIT = 1;  -- Set to 0 to actually perform the update

PRINT '========================================';
PRINT 'Disable Schedules with Unknown Invoice Date';
PRINT 'Started at: ' + CONVERT(VARCHAR(30), GETDATE(), 120);
IF @DryRun = 1
    PRINT '*** DRY RUN MODE - No changes will be made ***';
ELSE
    PRINT '*** LIVE MODE - Changes will be committed ***';
PRINT '========================================';
PRINT '';

SELECT @SourceCount = COUNT(*)
FROM dbo.ScheduleSyncSources
WHERE IsDeleted = 0
    AND LastInvoiceDate = @DefaultDate;

PRINT 'Found ' + CAST(@SourceCount AS VARCHAR(10)) + ' ScheduleSyncSources records with unknown LastInvoiceDate';
PRINT '';

PRINT 'Step 1: Building target schedule names from sources with unknown dates...';
PRINT '';

IF OBJECT_ID('tempdb..#Targets') IS NOT NULL
    DROP TABLE #Targets;

SELECT DISTINCT
    CanonicalName = CONCAT(
        CASE 
            WHEN sss.VendorName IS NULL 
                THEN CONCAT('Vendor', sss.ExternalVendorId)
            ELSE LTRIM(RTRIM(sss.VendorName))
        END,
        '_',
        LTRIM(RTRIM(sss.AccountNumber))
    ),
    ClientName = LTRIM(RTRIM(sss.ClientName))
INTO #Targets
FROM dbo.ScheduleSyncSources sss
WHERE sss.IsDeleted = 0
    AND sss.ClientName IS NOT NULL
    AND sss.LastInvoiceDate = @DefaultDate;

CREATE INDEX IX_Targets_Name_Client ON #Targets (CanonicalName, ClientName);

SELECT @RowsAffected = COUNT(*) FROM #Targets;
PRINT 'Built ' + CAST(@RowsAffected AS VARCHAR(10)) + ' unique target schedule names';
PRINT '';

PRINT 'Step 2: Identifying schedules to disable via exact name match...';
PRINT '';

IF OBJECT_ID('tempdb..#SchedulesToDisable') IS NOT NULL
    DROP TABLE #SchedulesToDisable;

SELECT DISTINCT
    s.Id AS ScheduleId,
    s.Name AS ScheduleName,
    s.ClientId,
    c.ClientName,
    s.IsEnabled AS CurrentIsEnabled,
    s.NextRunTime AS CurrentNextRunTime
INTO #SchedulesToDisable
FROM dbo.Schedules s
INNER JOIN dbo.Clients c ON c.Id = s.ClientId
INNER JOIN #Targets t ON t.CanonicalName = s.Name AND t.ClientName = c.ClientName
WHERE s.IsDeleted = 0
    AND s.CreatedBy = 'ScheduleSync';

SELECT @RowsAffected = COUNT(*) FROM #SchedulesToDisable;

PRINT 'Found ' + CAST(@RowsAffected AS VARCHAR(10)) + ' schedules to disable';
PRINT '';

PRINT 'Breakdown:';
SELECT 
    CASE WHEN CurrentIsEnabled = 1 THEN 'Currently Enabled' ELSE 'Already Disabled' END AS Status,
    COUNT(*) AS Count
FROM #SchedulesToDisable
GROUP BY CurrentIsEnabled;
PRINT '';

PRINT 'Sample of schedules to be disabled (first 10):';
SELECT TOP 10
    ScheduleId,
    ScheduleName,
    ClientName,
    CurrentIsEnabled,
    CurrentNextRunTime
FROM #SchedulesToDisable
ORDER BY ScheduleId;
PRINT '';

PRINT '========================================';
IF @DryRun = 1
BEGIN
    PRINT '*** DRY RUN COMPLETE - No changes made ***';
    PRINT 'To perform the actual update:';
    PRINT '1. Review the sample schedules above';
    PRINT '2. Set @DryRun = 0 at the top of the script';
    PRINT '3. Run the script again';
    PRINT '========================================';
    PRINT '';
    GOTO SkipUpdate;
END
ELSE
BEGIN
    PRINT 'Ready to disable ' + CAST(@RowsAffected AS VARCHAR(10)) + ' schedules';
    PRINT 'Press Ctrl+C to cancel, or continue to proceed...';
    PRINT '========================================';
    PRINT '';
    WAITFOR DELAY '00:00:05';  -- 5 second pause to allow cancellation
END

PRINT 'Step 3: Disabling schedules...';
PRINT '';

BEGIN TRANSACTION;

BEGIN TRY
    UPDATE s
    SET 
        s.IsEnabled = 0,
        s.NextRunTime = NULL,
        s.UpdatedAt = @UpdatedAt,
        s.UpdatedBy = @UpdatedBy
    FROM dbo.Schedules s
    INNER JOIN #SchedulesToDisable td ON s.Id = td.ScheduleId
    WHERE s.IsEnabled = 1;

    SET @RowsAffected = @@ROWCOUNT;

    COMMIT TRANSACTION;

    PRINT 'Successfully disabled ' + CAST(@RowsAffected AS VARCHAR(10)) + ' schedules';
    PRINT '';

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    
    PRINT 'ERROR: Failed to disable schedules';
    PRINT 'Error Message: ' + ERROR_MESSAGE();
    PRINT 'Error Line: ' + CAST(ERROR_LINE() AS VARCHAR(10));
    
    THROW;
END CATCH;

SkipUpdate:

IF @DryRun = 0
BEGIN
    PRINT 'Step 4: Verification...';
    PRINT '';

    SELECT 
        'Total schedules identified' AS Metric,
        COUNT(*) AS Count
    FROM #SchedulesToDisable
    UNION ALL
    SELECT 
        'Schedules now disabled',
        COUNT(*)
    FROM dbo.Schedules s
    INNER JOIN #SchedulesToDisable td ON s.Id = td.ScheduleId
    WHERE s.IsEnabled = 0
    UNION ALL
    SELECT 
        'Schedules with NULL NextRunTime',
        COUNT(*)
    FROM dbo.Schedules s
    INNER JOIN #SchedulesToDisable td ON s.Id = td.ScheduleId
    WHERE s.NextRunTime IS NULL;
END

PRINT '';
PRINT '========================================';
PRINT 'Script completed at: ' + CONVERT(VARCHAR(30), GETDATE(), 120);
PRINT '========================================';

DROP TABLE #SchedulesToDisable;
DROP TABLE #Targets;

SET NOCOUNT OFF;
GO
