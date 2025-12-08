SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
SET DEADLOCK_PRIORITY LOW;
SET LOCK_TIMEOUT 5000;

-- Index usage
SELECT
    DB_NAME() AS database_name,
    OBJECT_SCHEMA_NAME(i.object_id) AS schema_name,
    OBJECT_NAME(i.object_id) AS table_name,
    i.name AS index_name,
    i.index_id,
    COALESCE(usage_stats.user_seeks,0) AS user_seeks,
    COALESCE(usage_stats.user_scans,0) AS user_scans,
    COALESCE(usage_stats.user_lookups,0) AS user_lookups,
    COALESCE(usage_stats.user_updates,0) AS user_updates
FROM sys.indexes i
LEFT JOIN sys.dm_db_index_usage_stats usage_stats
    ON usage_stats.object_id = i.object_id
    AND usage_stats.index_id = i.index_id
    AND usage_stats.database_id = DB_ID()
WHERE i.object_id > 255
ORDER BY (COALESCE(usage_stats.user_seeks,0) + COALESCE(usage_stats.user_scans,0) + COALESCE(usage_stats.user_lookups,0)) DESC;

-- Missing index
SELECT
    DB_NAME() AS database_name,
    migs.group_handle,
    COALESCE(mid.index_handle, 0) AS index_handle,
    COALESCE((migs.unique_compiles + migs.user_seeks + migs.user_scans),0) AS total_read_est,
    statement = mid.statement,
    equality_columns = mid.equality_columns,
    inequality_columns = mid.inequality_columns,
    included_columns = mid.included_columns
FROM sys.dm_db_missing_index_group_stats migs
JOIN sys.dm_db_missing_index_groups mig ON migs.group_handle = mig.index_group_handle
JOIN sys.dm_db_missing_index_details mid ON mig.index_handle = mid.index_handle
WHERE mid.database_id = DB_ID()
ORDER BY total_read_est DESC;
