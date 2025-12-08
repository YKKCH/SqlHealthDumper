namespace SqlHealthDumper.Infrastructure;

/// <summary>
/// ツール由来の SQL であることを示す署名を付与・判定するヘルパー。
/// </summary>
internal static class SqlSignature
{
    internal const string CollectorPrefix = "-- SqlHealthDumper";

    /// <summary>
    /// 既定の署名が無い場合に先頭へ挿入する。
    /// </summary>
    internal static string EnsureSignature(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return CollectorPrefix;
        }

        return sql.TrimStart().StartsWith(CollectorPrefix, StringComparison.Ordinal)
            ? sql
            : $"{CollectorPrefix}{Environment.NewLine}{sql}";
    }

    /// <summary>
    /// 渡された SQL が署名済みかどうかを判定する。
    /// </summary>
    internal static bool HasSignature(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return false;
        return sql.TrimStart().StartsWith(CollectorPrefix, StringComparison.Ordinal);
    }
}
