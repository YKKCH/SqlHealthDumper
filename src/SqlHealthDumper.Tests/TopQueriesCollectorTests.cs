#nullable enable
using SqlHealthDumper.Collectors;
using SqlHealthDumper.Domain;
using SqlHealthDumper.Options;
using Xunit;

namespace SqlHealthDumper.Tests;

/// <summary>
/// <see cref="TopQueriesCollector"/> の実行 SQL 選択ロジックを検証する。
/// </summary>
public sealed class TopQueriesCollectorTests
{
    [Fact]
    /// <summary>
    /// Query Store サポート時に専用 SQL が利用されることを確認。
    /// </summary>
    public void ResolveSqlKey_UsesQueryStoreWhenSupported()
    {
        var options = new ExecutionOptions
        {
            Capabilities = new VersionCapability { SupportsQueryStore = true }
        };

        var key = TopQueriesCollector.ResolveSqlKey(options);
        Assert.Equal("Database.top_queries_qs", key);
    }

    [Fact]
    /// <summary>
    /// Query Store 非対応時に従来 DMV 用 SQL が選択されることを検証。
    /// </summary>
    public void ResolveSqlKey_FallsBackWhenNotSupported()
    {
        var options = new ExecutionOptions
        {
            Capabilities = new VersionCapability { SupportsQueryStore = false }
        };

        var key = TopQueriesCollector.ResolveSqlKey(options);
        Assert.Equal("Database.top_queries", key);
    }
}
