SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
SET DEADLOCK_PRIORITY LOW;
SET LOCK_TIMEOUT 5000;

SELECT
    s.name AS schema_name,
    t.name AS table_name,
    st.object_id,
    st.name AS stat_name,
    st.stats_id,
    st.auto_created,
    st.user_created,
    st.has_filter,
    sp.last_updated,
    sp.rows,
    sp.rows_sampled,
    sp.modification_counter
FROM sys.stats st
JOIN sys.tables t ON st.object_id = t.object_id
JOIN sys.schemas s ON t.schema_id = s.schema_id
OUTER APPLY sys.dm_db_stats_properties(st.object_id, st.stats_id) sp
WHERE t.is_ms_shipped = 0
ORDER BY s.name, t.name, st.stats_id
OPTION (MAXDOP 1);
