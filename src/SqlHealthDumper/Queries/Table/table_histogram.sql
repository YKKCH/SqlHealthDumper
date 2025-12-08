SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
SET DEADLOCK_PRIORITY LOW;
SET LOCK_TIMEOUT 5000;

DECLARE @object_id INT = {0};
DECLARE @stats_id INT = {1};
DECLARE @has_equal_rows BIT =
    CASE WHEN EXISTS (
        SELECT 1 FROM sys.all_columns
        WHERE object_id = OBJECT_ID('sys.dm_db_stats_histogram')
          AND name = 'equal_rows'
    ) THEN 1 ELSE 0 END;

DECLARE @sql NVARCHAR(MAX) = N'
SELECT
    step_number,
    range_high_key,
    range_rows,
    ' + CASE WHEN @has_equal_rows = 1 THEN N'equal_rows' ELSE N'eq_rows' END + N' AS eq_rows,
    distinct_range_rows
FROM sys.dm_db_stats_histogram(@object_id, @stats_id)
ORDER BY step_number
OPTION (MAXDOP 1);';

EXEC sys.sp_executesql @sql, N'@object_id INT, @stats_id INT', @object_id = @object_id, @stats_id = @stats_id;
