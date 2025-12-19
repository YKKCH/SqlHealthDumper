namespace SqlHealthDumper.Dashboard;

/// <summary>
/// ファイルコンテンツの取得結果。
/// </summary>
public sealed class SnapshotFileContent
{
    public string Path { get; init; } = string.Empty;

    public string MediaType { get; init; } = "text/plain";

    public string? Text { get; init; }

    public string? Html { get; init; }

    public bool IsTruncated { get; init; }

    public long SizeBytes { get; init; }

    public DateTime LastModifiedUtc { get; init; }
}
