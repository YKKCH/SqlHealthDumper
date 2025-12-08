SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
SET DEADLOCK_PRIORITY LOW;
SET LOCK_TIMEOUT 5000;

SELECT
    s.name AS stat_name,
    OBJECT_SCHEMA_NAME(s.object_id) AS schema_name,
    OBJECT_NAME(s.object_id) AS table_name,
    sp.last_updated,
    sp.rows,
    sp.rows_sampled,
    sp.steps,
    sp.modification_counter,
    COALESCE(NULLIF(sp.rows, 0), ps.row_count) AS effective_rows,
    CASE 
        WHEN COALESCE(ps.row_count, 0) = 0 THEN 0
        WHEN (ISNULL(sp.rows, 0) = 0 OR ISNULL(sp.rows_sampled, 0) = 0 OR sp.last_updated IS NULL) THEN 1
        ELSE 0
    END AS rows_estimated,
    CASE WHEN ISNULL(sp.steps, 0) = 0 THEN 1 ELSE 0 END AS histogram_unavailable
FROM sys.stats s
CROSS APPLY sys.dm_db_stats_properties(s.object_id, s.stats_id) sp
OUTER APPLY (
    SELECT SUM(ps.row_count) AS row_count
    FROM sys.dm_db_partition_stats ps
    WHERE ps.object_id = s.object_id
      AND ps.index_id IN (0, 1)
) ps
WHERE OBJECTPROPERTY(s.object_id, 'IsMsShipped') = 0
ORDER BY sp.last_updated DESC;
