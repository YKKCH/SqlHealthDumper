SET NOCOUNT ON;
SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
SET DEADLOCK_PRIORITY LOW;
SET LOCK_TIMEOUT 5000;

SELECT
    DB_NAME() AS database_name,
    mf.name AS logical_name,
    mf.type_desc,
    mf.physical_name,
    CAST(mf.size * 8.0 / 1024 AS DECIMAL(18,2)) AS size_mb,
    CAST(FILEPROPERTY(mf.name, 'SpaceUsed') * 8.0 / 1024 AS DECIMAL(18,2)) AS used_mb,
    CASE WHEN mf.is_percent_growth = 1
        THEN CONCAT(mf.growth, '%')
        ELSE CONCAT(CAST(mf.growth * 8.0 / 1024 AS DECIMAL(18,2)), ' MB')
    END AS growth_desc,
    CASE WHEN mf.max_size = -1
        THEN 'UNLIMITED'
        ELSE CONCAT(CAST(mf.max_size * 8.0 / 1024 AS DECIMAL(18,2)), ' MB')
    END AS max_size_desc
FROM sys.database_files mf
ORDER BY mf.file_id;
