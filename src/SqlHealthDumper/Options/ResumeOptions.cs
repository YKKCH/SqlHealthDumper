namespace SqlHealthDumper.Options;

/// <summary>
/// 中断したスナップショットを再開するための永続ファイル関連設定。
/// </summary>
public sealed class ResumeOptions
{
    /// <summary>
    /// レジューム機能を利用するかどうか。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// スナップショット進捗を保存するファイルパス。
    /// </summary>
    public string SnapshotStatePath { get; set; } = "snapshot_state.json";

    /// <summary>
    /// 失敗した項目の記録ファイルパス。
    /// </summary>
    public string FailuresPath { get; set; } = "failures.json";
}
