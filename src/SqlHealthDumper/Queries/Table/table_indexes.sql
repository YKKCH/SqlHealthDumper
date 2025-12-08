SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
SET DEADLOCK_PRIORITY LOW;
SET LOCK_TIMEOUT 5000;

SELECT
    s.name AS schema_name,
    t.name AS table_name,
    i.name AS index_name,
    CASE WHEN i.type IN (1,5,7) THEN 1 ELSE 0 END AS is_clustered,
    i.is_unique,
    i.type_desc AS index_type,
    ic.is_included_column,
    ic.key_ordinal,
    ic.index_column_id,
    c.name AS column_name
FROM sys.indexes i
JOIN sys.tables t ON i.object_id = t.object_id
JOIN sys.schemas s ON t.schema_id = s.schema_id
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE t.is_ms_shipped = 0
  AND i.index_id > 0
  AND i.is_hypothetical = 0
ORDER BY s.name, t.name, i.index_id, ic.key_ordinal, ic.index_column_id
OPTION (MAXDOP 1);
