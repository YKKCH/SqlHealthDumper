namespace SqlHealthDumper.Orchestration;

/// <summary>
/// レジューム用にセクション単位の完了状態を保持するモデル。
/// </summary>
public sealed class SnapshotState
{
    /// <summary>
    /// データベースごとの完了済みセクション集合。
    /// </summary>
    public Dictionary<string, HashSet<string>> Databases { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 指定セクションが完了済みかチェックする。
    /// </summary>
    public bool IsCompleted(string database, string section)
    {
        return Databases.TryGetValue(database, out var set) && set.Contains(section);
    }

    /// <summary>
    /// セクションを完了済みとしてマークする。
    /// </summary>
    public void MarkCompleted(string database, string section)
    {
        if (!Databases.TryGetValue(database, out var set))
        {
            set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Databases[database] = set;
        }
        set.Add(section);
    }
}

/// <summary>
/// 失敗したセクションとエラー情報を保持するレジューム用エントリ。
/// </summary>
public sealed class FailureEntry
{
    /// <summary>
    /// 失敗したデータベース名。
    /// </summary>
    public string Database { get; set; } = string.Empty;

    /// <summary>
    /// セクション名。
    /// </summary>
    public string Section { get; set; } = string.Empty;

    /// <summary>
    /// エラーメッセージ。
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// 発生日時 (UTC)。
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
