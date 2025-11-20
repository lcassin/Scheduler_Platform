
CREATE NONCLUSTERED INDEX [IX_JobExecutions_StartTime_Status_Duration]
ON [dbo].[JobExecutions] ([StartTime] DESC, [Status] ASC)
INCLUDE ([DurationSeconds], [ScheduleId], [EndTime])
GO

CREATE NONCLUSTERED INDEX [IX_JobExecutions_ScheduleId_StartTime]
ON [dbo].[JobExecutions] ([ScheduleId] ASC, [StartTime] DESC)
GO

SELECT 
    i.name AS IndexName,
    OBJECT_NAME(i.object_id) AS TableName,
    COL_NAME(ic.object_id, ic.column_id) AS ColumnName,
    ic.key_ordinal AS ColumnOrder,
    ic.is_descending_key AS IsDescending,
    ic.is_included_column AS IsIncluded
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
WHERE OBJECT_NAME(i.object_id) = 'JobExecutions'
    AND i.name IN ('IX_JobExecutions_StartTime_Status_Duration', 'IX_JobExecutions_ScheduleId_StartTime')
ORDER BY i.name, ic.key_ordinal, ic.is_included_column
GO
