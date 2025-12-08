namespace SqlHealthDumper.Domain;

/// <summary>
/// 対象バージョン/エディションで利用可能な機能フラグ。
/// </summary>
public sealed class VersionCapability
{
    /// <summary>
    /// Query Store がサポートされるか。
    /// </summary>
    public bool SupportsQueryStore { get; init; }

    /// <summary>
    /// <c>sys.dm_db_stats_properties</c> が利用可能か。
    /// </summary>
    public bool SupportsDmDbStatsProperties { get; init; }

    /// <summary>
    /// Azure SQL Database 上での実行か。
    /// </summary>
    public bool IsAzureSqlDatabase { get; init; }

    /// <summary>
    /// Azure SQL Managed Instance 上での実行か。
    /// </summary>
    public bool IsAzureManagedInstance { get; init; }
}
