namespace SqlHealthDumper.Domain;

/// <summary>
/// 1 回の実行で取得した SQL Server インスタンス全体の状態を保持する。
/// </summary>
public sealed class InstanceSnapshot
{
    /// <summary>
    /// 機能差異を把握するためのエディション名。
    /// </summary>
    public string? Edition { get; set; }

    /// <summary>
    /// 既知問題や互換性判断に使うバージョン表記。
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// 細かな更新レベルを識別するビルド番号。
    /// </summary>
    public string? Build { get; set; }

    /// <summary>
    /// 本番/検証などの運用環境区分。
    /// </summary>
    public EnvironmentKind Environment { get; set; } = EnvironmentKind.Unknown;

    /// <summary>
    /// 検出済みの機能サポート状況。
    /// </summary>
    public VersionCapability Capabilities { get; set; } = new();

    /// <summary>
    /// スナップショット対象となったデータベースの一覧。
    /// </summary>
    public List<DatabaseSnapshot> Databases { get; } = new();

    /// <summary>
    /// CPU やメモリなどインスタンス単位のリソースメトリック。
    /// </summary>
    public InstanceResourceSummary? ResourceSummary { get; set; }

    /// <summary>
    /// 待機統計の要約。パフォーマンス調査の起点となる。
    /// </summary>
    public List<InstanceWaitSummary> Waits { get; } = new();

    /// <summary>
    /// 収集時の補足事項や注意点。
    /// </summary>
    public List<string> Notes { get; } = new();
}

/// <summary>
/// インスタンスが持つ固定的/準固定的なリソース構成を表す。
/// </summary>
public sealed class InstanceResourceSummary
{
    /// <summary>
    /// 利用可能な論理 CPU 数。
    /// </summary>
    public int CpuCount { get; set; }

    /// <summary>
    /// 物理メモリ容量 (MB)。サイジングの判断材料。
    /// </summary>
    public double PhysicalMemoryMb { get; set; }

    /// <summary>
    /// SQL Server が確保済みのコミット済みメモリ (MB)。
    /// </summary>
    public double CommittedMemoryMb { get; set; }

    /// <summary>
    /// SQL Server のターゲットメモリ値 (MB)。
    /// </summary>
    public double TargetMemoryMb { get; set; }

    /// <summary>
    /// 最大ワーカー数 (scheduler thread) の推定値。
    /// </summary>
    public int MaxWorkerCount { get; set; }
}

/// <summary>
/// インスタンスレベルの主要 wait statistic を保持する。
/// </summary>
public sealed class InstanceWaitSummary
{
    /// <summary>
    /// Wait タイプの名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 単位時間あたりの待機時間 (ms/s)。
    /// </summary>
    public double WaitMsPerSec { get; set; }

    /// <summary>
    /// CPU を待機していた時間 (ms/s)。
    /// </summary>
    public double SignalMsPerSec { get; set; }

    /// <summary>
    /// 全待機に占める割合。
    /// </summary>
    public double PercentOfTotal { get; set; }
}

/// <summary>
/// 個別データベースのメタ情報や収集結果を格納する。
/// </summary>
public sealed class DatabaseSnapshot
{
    /// <summary>
    /// データベース名。出力の識別キー。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 互換レベル。利用可能機能の境界評価に使用。
    /// </summary>
    public int CompatibilityLevel { get; set; }

    /// <summary>
    /// リカバリモデル。バックアップ/ログ戦略の推測に用いる。
    /// </summary>
    public string? RecoveryModel { get; set; }

    /// <summary>
    /// ログ再利用を阻害している理由の説明。
    /// </summary>
    public string? LogReuseWaitDescription { get; set; }

    /// <summary>
    /// 読み取り専用構成かどうか。
    /// </summary>
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// TDE などによる暗号化有無。
    /// </summary>
    public bool IsEncrypted { get; set; }

    /// <summary>
    /// 可用性グループ/ミラーリング等の HA 機能有効状態。
    /// </summary>
    public bool IsHadrEnabled { get; set; }

    /// <summary>
    /// サイズ情報のサマリー。
    /// </summary>
    public DatabaseSizeInfo SizeInfo { get; set; } = new();

    /// <summary>
    /// 各データ/ログファイルの詳細。
    /// </summary>
    public List<DatabaseFileInfo> Files { get; } = new();

    /// <summary>
    /// インデックス使用状況と欠損分析。
    /// </summary>
    public IndexInsightSummary IndexInsights { get; set; } = new();

    /// <summary>
    /// 代表的な高負荷クエリ集合。
    /// </summary>
    public List<QueryInsight> TopQueries { get; } = new();

    /// <summary>
    /// 統計情報に関する詳細。
    /// </summary>
    public List<StatsInsight> Stats { get; } = new();

    /// <summary>
    /// バックアップ・CheckDB など保守タスクの状況。
    /// </summary>
    public BackupMaintenanceInfo BackupMaintenance { get; set; } = new();

    /// <summary>
    /// テーブルごとの構造とメトリック。
    /// </summary>
    public List<TableSnapshot> Tables { get; } = new();

    /// <summary>
    /// データベース個別の補足メモ。
    /// </summary>
    public List<string> Notes { get; } = new();
}

/// <summary>
/// データ/ログ/空き領域の概算サイズをまとめる。
/// </summary>
public sealed class DatabaseSizeInfo
{
    /// <summary>
    /// データファイル容量 (MB)。
    /// </summary>
    public double DataSizeMb { get; set; }

    /// <summary>
    /// ログファイル容量 (MB)。
    /// </summary>
    public double LogSizeMb { get; set; }

    /// <summary>
    /// 推定空き領域 (MB)。
    /// </summary>
    public double FreeSpaceMb { get; set; }
}

/// <summary>
/// インデックスに関する利用状況と欠損候補を保持する。
/// </summary>
public sealed class IndexInsightSummary
{
    /// <summary>
    /// 既存インデックスの使用頻度指標。
    /// </summary>
    public List<IndexUsageInfo> Usage { get; } = new();

    /// <summary>
    /// 欠損インデックスの提案情報。
    /// </summary>
    public List<MissingIndexInfo> Missing { get; } = new();
}

/// <summary>
/// DMV から取得したインデックス使用統計を保持する。
/// </summary>
public sealed class IndexUsageInfo
{
    /// <summary>
    /// テーブル/ビュー名。
    /// </summary>
    public string ObjectName { get; set; } = string.Empty;

    /// <summary>
    /// インデックス名。クラスターレベルなどで null の場合あり。
    /// </summary>
    public string? IndexName { get; set; }

    /// <summary>
    /// Seek 回数。ポイント検索の有効度合いに利用。
    /// </summary>
    public long Seeks { get; set; }

    /// <summary>
    /// Scan 回数。広範囲読み取りの多さを示す。
    /// </summary>
    public long Scans { get; set; }

    /// <summary>
    /// Lookup 回数。Bookmark lookup によるコスト指標。
    /// </summary>
    public long Lookups { get; set; }

    /// <summary>
    /// Update 回数。書き込み負荷の指標。
    /// </summary>
    public long Updates { get; set; }
}

/// <summary>
/// 欠損インデックス候補の内容と影響度を表す。
/// </summary>
public sealed class MissingIndexInfo
{
    /// <summary>
    /// 対象テーブル名。
    /// </summary>
    public string ObjectName { get; set; } = string.Empty;

    /// <summary>
    /// DMV が提示する潜在効果。
    /// </summary>
    public string? Impact { get; set; }

    /// <summary>
    /// CREATE INDEX 文の骨子となる推奨定義。
    /// </summary>
    public string Definition { get; set; } = string.Empty;
}

/// <summary>
/// 単一テーブルの構造、サイズ、関連メタ情報をまとめる。
/// </summary>
public sealed class TableSnapshot
{
    /// <summary>
    /// スキーマ名。
    /// </summary>
    public string Schema { get; set; } = string.Empty;

    /// <summary>
    /// テーブル名。
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// 推定行数。容量傾向を掴むために使用。
    /// </summary>
    public long RowCount { get; set; }

    /// <summary>
    /// ヒープ/クラスターデータ領域のサイズ。
    /// </summary>
    public double DataSizeMb { get; set; }

    /// <summary>
    /// 非クラスタインデックスを含む索引領域サイズ。
    /// </summary>
    public double IndexSizeMb { get; set; }

    /// <summary>
    /// 列定義の一覧。
    /// </summary>
    public List<ColumnDefinition> Columns { get; } = new();

    /// <summary>
    /// インデックス構成の一覧。
    /// </summary>
    public List<IndexDefinition> Indexes { get; } = new();

    /// <summary>
    /// 制約 (PK/FK など) の一覧。
    /// </summary>
    public List<ConstraintDefinition> Constraints { get; } = new();

    /// <summary>
    /// トリガーの一覧。
    /// </summary>
    public List<TriggerDefinition> Triggers { get; } = new();

    /// <summary>
    /// 統計情報の一覧。カーディナリティ推定の診断に使用。
    /// </summary>
    public List<StatsInsight> Stats { get; } = new();

    /// <summary>
    /// 詳細ヒストグラムをファイル出力した場合のパス。
    /// </summary>
    public string? HistogramDetailPath { get; set; }
}

/// <summary>
/// テーブル列のメタデータ。
/// </summary>
public sealed class ColumnDefinition
{
    /// <summary>
    /// 列名。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 完全修飾済みのデータ型表示。
    /// </summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>
    /// NULL 許容かどうか。
    /// </summary>
    public bool IsNullable { get; set; }

    /// <summary>
    /// 既定値定義。式の場合も文字列で保持。
    /// </summary>
    public string? DefaultValue { get; set; }
}

/// <summary>
/// インデックス構成の概要。
/// </summary>
public sealed class IndexDefinition
{
    /// <summary>
    /// インデックス名。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// クラスタードかどうか。
    /// </summary>
    public bool IsClustered { get; set; }

    /// <summary>
    /// ユニーク制約を持つかどうか。
    /// </summary>
    public bool IsUnique { get; set; }

    /// <summary>
    /// キー列の順序付きリスト。
    /// </summary>
    public List<string> KeyColumns { get; } = new();

    /// <summary>
    /// Include 列の一覧。
    /// </summary>
    public List<string> IncludeColumns { get; } = new();
}

/// <summary>
/// 制約オブジェクトの識別情報。
/// </summary>
public sealed class ConstraintDefinition
{
    /// <summary>
    /// 制約名。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 制約種別 (PK, FK など)。
    /// </summary>
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// テーブルに紐づくトリガーの状態。
/// </summary>
public sealed class TriggerDefinition
{
    /// <summary>
    /// トリガー名。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 有効/無効の状態。
    /// </summary>
    public bool IsEnabled { get; set; }
}

/// <summary>
/// 高負荷クエリの統計と補足情報を保持する。
/// </summary>
public sealed class QueryInsight
{
    /// <summary>
    /// DMV 上のプラン/クエリ識別子。
    /// </summary>
    public string QueryId { get; set; } = string.Empty;

    /// <summary>
    /// 抜粋した SQL テキスト。サイズ制約で null の場合あり。
    /// </summary>
    public string? SqlText { get; set; }

    /// <summary>
    /// 長文 SQL を外部ファイルに書き出した際のパス。
    /// </summary>
    public string? SqlTextPath { get; set; }

    /// <summary>
    /// 累計 CPU 時間 (ms)。負荷指標。
    /// </summary>
    public double CpuTimeMs { get; set; }

    /// <summary>
    /// 累計実行時間 (ms)。ユーザーへの体感影響を示す。
    /// </summary>
    public double DurationMs { get; set; }

    /// <summary>
    /// ロジカルリード回数。
    /// </summary>
    public long LogicalReads { get; set; }

    /// <summary>
    /// 書き込みページ数。
    /// </summary>
    public long Writes { get; set; }

    /// <summary>
    /// 実行回数。頻度とコストの掛け算で優先度を決める。
    /// </summary>
    public long ExecutionCount { get; set; }

    /// <summary>
    /// 最終実行日時。現在のホットさを判断する材料。
    /// </summary>
    public DateTime? LastExecutionTime { get; set; }

    /// <summary>
    /// 実行計画の要約や重要メモ。
    /// </summary>
    public string? PlanSummary { get; set; }
}

/// <summary>
/// 統計情報の状態とヒストグラム詳細を表す。
/// </summary>
public sealed class StatsInsight
{
    /// <summary>
    /// 統計情報の名称。
    /// </summary>
    public string StatName { get; set; } = string.Empty;

    /// <summary>
    /// 最終更新日時。
    /// </summary>
    public DateTime? LastUpdated { get; set; }

    /// <summary>
    /// 統計が表す総行数。
    /// </summary>
    public long Rows { get; set; }

    /// <summary>
    /// サンプリング行数。
    /// </summary>
    public long RowsSampled { get; set; }

    /// <summary>
    /// 変更カウンター。更新必要性を判断する指標。
    /// </summary>
    public long ModificationCounter { get; set; }

    /// <summary>
    /// Density vector の詳細。
    /// </summary>
    public List<DensityVectorEntry> DensityVector { get; } = new();

    /// <summary>
    /// ヒストグラム要約。
    /// </summary>
    public HistogramSummary? HistogramSummary { get; set; }

    /// <summary>
    /// ヒストグラム全バケットの詳細。
    /// </summary>
    public List<HistogramBucket> HistogramDetail { get; } = new();

    /// <summary>
    /// 詳細を外部ファイル保存した場合の参照パス。
    /// </summary>
    public string? HistogramDetailPath { get; set; }

    /// <summary>
    /// 行数が推定値かどうか (一部 DMV で推定になるケース)。
    /// </summary>
    public bool RowsEstimated { get; set; }

    /// <summary>
    /// ヒストグラム取得が不可能だった場合のフラグ。
    /// </summary>
    public bool HistogramUnavailable { get; set; }
}

/// <summary>
/// Density vector の単一列情報。
/// </summary>
public sealed class DensityVectorEntry
{
    /// <summary>
    /// 列名。
    /// </summary>
    public string Column { get; set; } = string.Empty;

    /// <summary>
    /// Density 値。
    /// </summary>
    public double Density { get; set; }
}

/// <summary>
/// ヒストグラムの高位要約。
/// </summary>
public sealed class HistogramSummary
{
    /// <summary>
    /// バケットの一覧。
    /// </summary>
    public List<HistogramBucket> Buckets { get; } = new();
}

/// <summary>
/// ヒストグラムの単一バケットを表す。
/// </summary>
public sealed class HistogramBucket
{
    /// <summary>
    /// 範囲下限。
    /// </summary>
    public double RangeFrom { get; set; }

    /// <summary>
    /// 範囲上限。
    /// </summary>
    public double RangeTo { get; set; }

    /// <summary>
    /// 下限値と完全一致した行数。
    /// </summary>
    public double EqualRows { get; set; }

    /// <summary>
    /// 範囲内の推定行数。
    /// </summary>
    public double RangeRows { get; set; }

    /// <summary>
    /// 範囲内の推定異なる値数。
    /// </summary>
    public double DistinctRangeRows { get; set; }
}

/// <summary>
/// バックアップおよび整合性チェックの状況を記録する。
/// </summary>
public sealed class BackupMaintenanceInfo
{
    /// <summary>
    /// バックアップ情報が収集可能だったか。
    /// </summary>
    public bool IsSupported { get; set; } = true;

    /// <summary>
    /// 収集が不可能だった理由の説明。
    /// </summary>
    public string? NotAvailableReason { get; set; }

    /// <summary>
    /// 主要バックアップ履歴。
    /// </summary>
    public List<BackupHistoryEntry> Backups { get; } = new();

    /// <summary>
    /// DBCC CHECKDB 実行状況。
    /// </summary>
    public List<CheckDbEntry> CheckDb { get; } = new();

    /// <summary>
    /// エージェントジョブの成否記録。
    /// </summary>
    public List<AgentJobEntry> AgentJobs { get; } = new();
}

/// <summary>
/// データベースファイルの基本情報。
/// </summary>
public sealed class DatabaseFileInfo
{
    /// <summary>
    /// 論理ファイル名。
    /// </summary>
    public string LogicalName { get; set; } = string.Empty;

    /// <summary>
    /// ファイル種別 (ROWS/LOG 等)。
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 物理パス。
    /// </summary>
    public string PhysicalPath { get; set; } = string.Empty;

    /// <summary>
    /// ファイルサイズ (MB)。
    /// </summary>
    public double SizeMb { get; set; }

    /// <summary>
    /// 使用済み領域 (MB)。
    /// </summary>
    public double UsedMb { get; set; }

    /// <summary>
    /// 増分設定の説明。
    /// </summary>
    public string GrowthDescription { get; set; } = string.Empty;

    /// <summary>
    /// 最大サイズ設定の説明。
    /// </summary>
    public string MaxSizeDescription { get; set; } = string.Empty;
}

/// <summary>
/// 個別バックアップの概要。
/// </summary>
public sealed class BackupHistoryEntry
{
    /// <summary>
    /// バックアップ種別 (FULL/DIFF/LOG)。
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 開始日時。
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 実行時間。
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// バックアップサイズ (MB)。
    /// </summary>
    public double SizeMb { get; set; }
}

/// <summary>
/// DBCC CHECKDB の実行履歴を表す。
/// </summary>
public sealed class CheckDbEntry
{
    /// <summary>
    /// 実行元のジョブ名。
    /// </summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// 直近成功日時。
    /// </summary>
    public DateTime? LastSuccess { get; set; }

    /// <summary>
    /// 直近失敗日時。
    /// </summary>
    public DateTime? LastFailure { get; set; }

    /// <summary>
    /// 失敗時のエラーメッセージ。
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// SQL Server Agent ジョブの概要。
/// </summary>
public sealed class AgentJobEntry
{
    /// <summary>
    /// ジョブ名。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 直近の実行日時。
    /// </summary>
    public DateTime? LastRun { get; set; }

    /// <summary>
    /// 直近実行が成功したかどうか。
    /// </summary>
    public bool LastRunSucceeded { get; set; }

    /// <summary>
    /// 備考。エラー内容や役割メモを保持。
    /// </summary>
    public string? Note { get; set; }
}

/// <summary>
/// テーブル単位で抽出された統計ヒストグラムの集合。
/// </summary>
public sealed class TableHistogramDetail
{
    /// <summary>
    /// スキーマ名。
    /// </summary>
    public string Schema { get; set; } = string.Empty;

    /// <summary>
    /// テーブル名。
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// 各統計の詳細。
    /// </summary>
    public List<TableHistogramStat> Stats { get; } = new();
}

/// <summary>
/// 単一統計のヒストグラム詳細。
/// </summary>
public sealed class TableHistogramStat
{
    /// <summary>
    /// 統計名。
    /// </summary>
    public string StatName { get; set; } = string.Empty;

    /// <summary>
    /// ヒストグラムバケット集合。
    /// </summary>
    public List<HistogramBucket> Buckets { get; } = new();
}
