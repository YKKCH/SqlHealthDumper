SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
SET DEADLOCK_PRIORITY LOW;
SET LOCK_TIMEOUT 5000;

-- dm_exec_query_stats fallback (no Query Store dependency)
SELECT TOP 50
    DB_NAME() AS database_name,
    qs.sql_handle,
    qs.plan_handle,
    qs.execution_count,
    qs.total_elapsed_time / NULLIF(qs.execution_count,0) AS avg_elapsed_time,
    qs.total_worker_time / NULLIF(qs.execution_count,0) AS avg_cpu_time,
    qs.total_logical_reads / NULLIF(qs.execution_count,0) AS avg_logical_reads,
    qs.total_logical_writes / NULLIF(qs.execution_count,0) AS avg_logical_writes,
    qs.last_execution_time,
    SUBSTRING(st.text, (qs.statement_start_offset/2)+1,
        ((CASE qs.statement_end_offset
          WHEN -1 THEN DATALENGTH(st.text)
          ELSE qs.statement_end_offset
          END - qs.statement_start_offset)/2) + 1) AS query_text
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
WHERE qs.execution_count > 0
  AND (st.dbid IS NULL OR st.dbid = DB_ID())
  AND st.text NOT LIKE '-- SqlHealthDumper%'
  AND st.text NOT LIKE 'SET TRANSACTION ISOLATION LEVEL%'
ORDER BY avg_cpu_time DESC;
