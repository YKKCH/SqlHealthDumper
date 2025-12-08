namespace SqlHealthDumper.Domain;

/// <summary>
/// スナップショット対象インスタンスの運用環境分類。
/// </summary>
public enum EnvironmentKind
{
    /// <summary>
    /// 判別不能または未指定。
    /// </summary>
    Unknown,

    /// <summary>
    /// 物理/仮想サーバー上の SQL Server。
    /// </summary>
    OnPremises,

    /// <summary>
    /// コンテナ環境で稼働する SQL Server。
    /// </summary>
    Container,

    /// <summary>
    /// Azure SQL Database。
    /// </summary>
    AzureSqlDatabase,

    /// <summary>
    /// Azure SQL Managed Instance。
    /// </summary>
    AzureSqlManagedInstance
}

/// <summary>
/// 出力レポートのセクション区分。
/// </summary>
public enum SectionKind
{
    /// <summary>
    /// インスタンス全体概要。
    /// </summary>
    InstanceOverview,

    /// <summary>
    /// 待機統計セクション。
    /// </summary>
    InstanceWaits,

    /// <summary>
    /// データベース基本情報。
    /// </summary>
    DatabaseOverview,

    /// <summary>
    /// インデックス利用状況と欠損提案。
    /// </summary>
    IndexUsageAndMissing,

    /// <summary>
    /// 上位クエリ分析。
    /// </summary>
    TopQueries,

    /// <summary>
    /// 統計情報とパラメータセクション。
    /// </summary>
    StatsAndParams,

    /// <summary>
    /// バックアップ/メンテナンス状況。
    /// </summary>
    BackupAndMaintenance,

    /// <summary>
    /// テーブル定義と構造情報。
    /// </summary>
    TableDefinition
}
