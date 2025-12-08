SET NOCOUNT ON;

DECLARE @has_hadr BIT = CASE WHEN COL_LENGTH('sys.databases', 'is_hadr_enabled') IS NULL THEN 0 ELSE 1 END;

DECLARE @sql NVARCHAR(MAX) = N'
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

WITH files AS (
    SELECT
        database_id,
        type,
        size,
        FILEPROPERTY(name, ''SpaceUsed'') AS space_used
    FROM sys.master_files
)
SELECT
    d.name,
    d.compatibility_level,
    d.recovery_model_desc,
    d.log_reuse_wait_desc,
    d.is_read_only,
    d.is_encrypted,
    ' + CASE WHEN @has_hadr = 1 THEN N'd.is_hadr_enabled' ELSE N'CAST(0 AS bit)' END + N' AS is_hadr_enabled,
    CAST(SUM(CASE WHEN f.type = 0 THEN f.size END) * 8.0 / 1024 AS DECIMAL(18,2)) AS data_size_mb,
    CAST(SUM(CASE WHEN f.type = 1 THEN f.size END) * 8.0 / 1024 AS DECIMAL(18,2)) AS log_size_mb,
    CAST(SUM(CASE WHEN f.type = 0 THEN (f.size - f.space_used) END) * 8.0 / 1024 AS DECIMAL(18,2)) AS free_space_mb
FROM sys.databases d
LEFT JOIN files f ON d.database_id = f.database_id
WHERE d.state = 0
GROUP BY d.name, d.compatibility_level, d.recovery_model_desc, d.log_reuse_wait_desc, d.is_read_only, d.is_encrypted' + CASE WHEN @has_hadr = 1 THEN N', d.is_hadr_enabled' ELSE N'' END + N'
ORDER BY d.name;';

EXEC sp_executesql @sql;
