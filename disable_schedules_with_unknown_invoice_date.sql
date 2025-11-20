--
--

SET NOCOUNT ON;
GO

DECLARE @DefaultDate DATETIME2(7) = '0001-01-01T00:00:00';
DECLARE @UpdatedBy NVARCHAR(100) = 'DisableUnknownInvoiceDateScript';
DECLARE @UpdatedAt DATETIME2(7) = SYSUTCDATETIME();
DECLARE @RowsAffected INT = 0;

PRINT '========================================';
PRINT 'Disable Schedules with Unknown Invoice Date';
PRINT 'Started at: ' + CONVERT(VARCHAR(30), GETDATE(), 120);
PRINT '========================================';
PRINT '';

PRINT 'Step 1: Identifying schedules to disable...';
PRINT '';

IF OBJECT_ID('tempdb..#SchedulesToDisable') IS NOT NULL
    DROP TABLE #SchedulesToDisable;

CREATE TABLE #SchedulesToDisable (
    ScheduleId INT PRIMARY KEY,
    ScheduleName NVARCHAR(500),
    ClientId INT,
    ClientName NVARCHAR(500),
    CurrentIsEnabled BIT,
    CurrentNextRunTime DATETIME
);

INSERT INTO #SchedulesToDisable (ScheduleId, ScheduleName, ClientId, ClientName, CurrentIsEnabled, CurrentNextRunTime)
SELECT DISTINCT
    s.Id,
    s.Name,
    s.ClientId,
    c.ClientName,
    s.IsEnabled,
    s.NextRunTime
FROM dbo.Schedules s
INNER JOIN dbo.Clients c ON s.ClientId = c.Id
WHERE s.IsDeleted = 0
    AND s.CreatedBy = 'ScheduleSync'  -- Only affect auto-generated schedules
    AND EXISTS (
        SELECT 1
        FROM dbo.ScheduleSyncSources sss
        WHERE sss.IsDeleted = 0
            AND sss.LastInvoiceDate = @DefaultDate
            AND sss.ClientName = c.ClientName
            AND s.Name LIKE '%' + ISNULL(sss.VendorName, '') + '%' + sss.AccountNumber + '%'
    );

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
PRINT 'Ready to disable ' + CAST(@RowsAffected AS VARCHAR(10)) + ' schedules';
PRINT 'Press Ctrl+C to cancel, or continue to proceed...';
PRINT '========================================';
PRINT '';


PRINT 'Step 2: Disabling schedules...';
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
    WHERE s.IsEnabled = 1;  -- Only update schedules that are currently enabled

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

PRINT 'Step 3: Verification...';
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

PRINT '';
PRINT '========================================';
PRINT 'Script completed at: ' + CONVERT(VARCHAR(30), GETDATE(), 120);
PRINT '========================================';

DROP TABLE #SchedulesToDisable;

SET NOCOUNT OFF;
GO
