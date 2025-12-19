namespace SqlHealthDumper.Dashboard;

/// <summary>
/// スナップショット内のファイル一覧表示用 DTO。
/// </summary>
public sealed class SnapshotFileDescriptor
{
    public string Path { get; init; } = string.Empty;

    public long SizeBytes { get; init; }

    public DateTime LastModifiedUtc { get; init; }
}
