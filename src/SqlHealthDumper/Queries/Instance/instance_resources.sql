SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
SET DEADLOCK_PRIORITY LOW;

SELECT TOP (1)
    cpu_count = cpu_count,
    scheduler_count = scheduler_count,
    hyperthread_ratio = hyperthread_ratio,
    physical_memory_mb = physical_memory_kb / 1024.0,
    committed_memory_mb = committed_kb / 1024.0,
    committed_target_mb = committed_target_kb / 1024.0,
    max_workers_count = max_workers_count
FROM sys.dm_os_sys_info;
