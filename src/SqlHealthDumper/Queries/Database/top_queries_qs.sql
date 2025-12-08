SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
SET DEADLOCK_PRIORITY LOW;
SET LOCK_TIMEOUT 5000;

-- Query Store based Top Queries (requires Query Store enabled)
SELECT TOP 50
    DB_NAME() AS database_name,
    qs.query_id,
    qp.plan_id,
    rs.count_executions AS execution_count,
    rs.avg_duration AS avg_elapsed_time,
    rs.avg_cpu_time AS avg_cpu_time,
    rs.avg_logical_io_reads AS avg_logical_reads,
    rs.avg_logical_io_writes AS avg_logical_writes,
    rs.last_execution_time,
    qt.query_sql_text AS query_text
FROM sys.query_store_runtime_stats rs
JOIN sys.query_store_plan qp ON rs.plan_id = qp.plan_id
JOIN sys.query_store_query qs ON qp.query_id = qs.query_id
JOIN sys.query_store_query_text qt ON qs.query_text_id = qt.query_text_id
WHERE qs.is_internal_query = 0
  AND qt.query_sql_text NOT LIKE '-- SqlHealthDumper%'
  AND qt.query_sql_text NOT LIKE 'SET TRANSACTION ISOLATION LEVEL%'
ORDER BY rs.avg_cpu_time DESC;
