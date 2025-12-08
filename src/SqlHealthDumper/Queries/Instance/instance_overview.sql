SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    SERVERPROPERTY('Edition') AS edition,
    SERVERPROPERTY('ProductVersion') AS product_version,
    SERVERPROPERTY('ProductLevel') AS product_level,
    SERVERPROPERTY('EngineEdition') AS engine_edition,
    SERVERPROPERTY('IsClustered') AS is_clustered,
    SERVERPROPERTY('EngineEdition') AS engine_edition_raw,
    SERVERPROPERTY('ComputerNamePhysicalNetBIOS') AS machine_name,
    (SELECT TOP 1 host_platform FROM sys.dm_os_host_info) AS host_platform,
    (SELECT TOP 1 host_distribution FROM sys.dm_os_host_info) AS host_distribution,
    (SELECT TOP 1 host_release FROM sys.dm_os_host_info) AS host_release;
