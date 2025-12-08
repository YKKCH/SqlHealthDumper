SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
SET DEADLOCK_PRIORITY LOW;
SET LOCK_TIMEOUT 5000;

-- Recent backup history (Top 20)
SELECT TOP 20
    b.database_name,
    b.backup_start_date,
    b.backup_finish_date,
    b.backup_size / 1024.0 / 1024.0 AS backup_size_mb,
    b.type AS backup_type
FROM msdb.dbo.backupset b
WHERE b.database_name = DB_NAME()
ORDER BY b.backup_finish_date DESC;

-- CHECKDB last run (msdb)
SELECT TOP 5
    j.name AS job_name,
    h.run_date,
    h.run_time,
    h.run_status,
    h.message
FROM msdb.dbo.sysjobs j
JOIN msdb.dbo.sysjobhistory h ON j.job_id = h.job_id
WHERE j.name LIKE '%CHECKDB%' AND h.step_id = 0
ORDER BY h.run_date DESC, h.run_time DESC;
