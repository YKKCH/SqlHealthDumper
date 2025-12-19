namespace SqlHealthDumper.Dashboard;

/// <summary>
/// ダッシュボードで扱うスナップショットのメタデータ。
/// </summary>
public sealed class SnapshotInfo
{
    /// <summary>
    /// 内部的に使用する一意 ID。
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 表示名（通常はディレクトリ名）。
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// ルートパス。
    /// </summary>
    public string FullPath { get; init; } = string.Empty;

    /// <summary>
    /// 指定ルート配下での相対パス。
    /// </summary>
    public string RelativePath { get; init; } = string.Empty;

    /// <summary>
    /// 最終更新日時 (UTC)。
    /// </summary>
    public DateTime LastModifiedUtc { get; init; }

    /// <summary>
    /// 含まれるファイル数。
    /// </summary>
    public long FileCount { get; init; }

    /// <summary>
    /// 合計サイズ (bytes)。
    /// </summary>
    public long TotalSizeBytes { get; init; }
}
