namespace SqlHealthDumper.Options;

using SqlHealthDumper.Domain;

/// <summary>
/// セクション単位で収集処理をオン/オフするためのフラグ集。
/// </summary>
public sealed class SectionToggles
{
    /// <summary>
    /// 上位クエリを収集するかどうか。
    /// </summary>
    public bool CollectTopQueries { get; set; } = true;

    /// <summary>
    /// 欠損インデックス分析を行うかどうか。
    /// </summary>
    public bool CollectMissingIndex { get; set; } = true;

    /// <summary>
    /// テーブル Markdown を生成するかどうか。
    /// </summary>
    public bool CollectTableMarkdown { get; set; } = true;

    /// <summary>
    /// バックアップ/メンテナンス情報を収集するか。
    /// </summary>
    public bool CollectBackupInfo { get; set; } = true;

    /// <summary>
    /// 統計情報を収集するか。
    /// </summary>
    public bool CollectStats { get; set; } = true;
}

/// <summary>
/// ツール実行時の負荷レベルプリセット。
/// </summary>
public enum ExecutionMode
{
    /// <summary>
    /// 収集頻度を抑えた低負荷モード。
    /// </summary>
    LowLoad,

    /// <summary>
    /// バランス型。既定モード。
    /// </summary>
    Balanced,

    /// <summary>
    /// 時間短縮を優先した高速モード。
    /// </summary>
    Fast
}

/// <summary>
/// スナップショット実行に関するパフォーマンスおよび対象制御設定。
/// </summary>
public sealed class ExecutionOptions
{
    /// <summary>
    /// 実行モード。
    /// </summary>
    public ExecutionMode Mode { get; set; } = ExecutionMode.LowLoad;

    /// <summary>
    /// クエリタイムアウト (秒)。
    /// </summary>
    public int QueryTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// 全体処理の最大並列数。
    /// </summary>
    public int MaxParallelism { get; set; } = 1;

    /// <summary>
    /// テーブル単位処理の並列上限。
    /// </summary>
    public int TableMaxParallelism { get; set; } = 1;

    /// <summary>
    /// ロックタイムアウト (ミリ秒)。null の場合は既定。
    /// </summary>
    public int? LockTimeoutMilliseconds { get; set; }

    /// <summary>
    /// システム DB を含めるか。
    /// </summary>
    public bool IncludeSystemDatabases { get; set; }

    /// <summary>
    /// システムオブジェクトを含めるか。
    /// </summary>
    public bool IncludeSystemObjects { get; set; }

    /// <summary>
    /// 収集対象へ含める DB パターン。
    /// </summary>
    public List<string> IncludeDatabases { get; init; } = new();

    /// <summary>
    /// 除外 DB パターン。
    /// </summary>
    public List<string> ExcludeDatabases { get; init; } = new();

    /// <summary>
    /// セクション単位の実行フラグ。
    /// </summary>
    public SectionToggles Sections { get; init; } = new();

    /// <summary>
    /// 対象バージョンがサポートする機能群。
    /// </summary>
    public VersionCapability Capabilities { get; set; } = new();
}
