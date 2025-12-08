using System;
using System.Linq;
using SqlHealthDumper.Domain;
using SqlHealthDumper.Infrastructure;
using SqlHealthDumper.Options;

namespace SqlHealthDumper.Collectors;

/// <summary>
/// 高負荷クエリ (Query Store/DMV) を抽出するコレクター。
/// </summary>
public sealed class TopQueriesCollector
{
    private readonly ISqlLoader _sqlLoader;
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly SqlExecutionHelper _sql;

    /// <summary>
    /// SQL 実行に必要な依存を注入して初期化する。
    /// </summary>
    public TopQueriesCollector(ISqlLoader sqlLoader, SqlConnectionFactory connectionFactory, SqlExecutionHelper sql)
    {
        _sqlLoader = sqlLoader;
        _connectionFactory = connectionFactory;
        _sql = sql;
    }

    /// <summary>
    /// Query Store を優先しつつ上位クエリを抽出し、診断用のノイズを除去する。
    /// </summary>
    public async Task<CollectorResult<List<QueryInsight>>> CollectAsync(string databaseName, AppConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = _connectionFactory.CreateOpenConnection(config, databaseName);
            var sqlKey = ResolveSqlKey(config.Execution);
            var sqlText = _sqlLoader.GetSql(sqlKey);
            var rows = await _sql.QueryAsync(connection, sqlText, config.Execution.QueryTimeoutSeconds, cancellationToken);
            var filtered = rows
                .Where(row => !SqlSignature.HasSignature(row.GetValueOrDefault("query_text") as string))
                .Where(row => !IsDiagnosticWorkload(row))
                .ToList();
            var mapped = filtered.Select(MapQuery).ToList();
            return CollectorResult<List<QueryInsight>>.Success(mapped);
        }
        catch (Exception ex)
        {
            return CollectorResult<List<QueryInsight>>.Failed($"Top queries collection failed for {databaseName}", ex);
        }
    }

    private static QueryInsight MapQuery(Dictionary<string, object?> row)
    {
        return new QueryInsight
        {
            QueryId = BuildQueryId(row),
            SqlText = row.GetValueOrDefault("query_text") as string,
            CpuTimeMs = ConvertToDouble(row.GetValueOrDefault("avg_cpu_time")) / 1000.0,
            DurationMs = ConvertToDouble(row.GetValueOrDefault("avg_elapsed_time")) / 1000.0,
            LogicalReads = ConvertToLong(row.GetValueOrDefault("avg_logical_reads")),
            Writes = ConvertToLong(row.GetValueOrDefault("avg_logical_writes")),
            ExecutionCount = ConvertToLong(row.GetValueOrDefault("execution_count")),
            LastExecutionTime = row.GetValueOrDefault("last_execution_time") as DateTime?
        };
    }

    /// <summary>
    /// 実行環境に応じて Query Store 用 SQL か従来 DMV の SQL を選択する。
    /// </summary>
    public static string ResolveSqlKey(ExecutionOptions execution)
    {
        // Query Store優先。Query Store非対応の場合は従来のdm_exec_query_statsを使用。
        return execution.Capabilities.SupportsQueryStore
            ? "Database.top_queries_qs"
            : "Database.top_queries";
    }

    private static string BuildQueryId(Dictionary<string, object?> row)
    {
        var handle = row.GetValueOrDefault("sql_handle")?.ToString();
        var planHandle = row.GetValueOrDefault("plan_handle")?.ToString();
        if (!string.IsNullOrEmpty(handle) || !string.IsNullOrEmpty(planHandle))
        {
            return $"{handle ?? "-"}-{planHandle ?? "-"}";
        }

        var queryId = row.GetValueOrDefault("query_id")?.ToString();
        var planId = row.GetValueOrDefault("plan_id")?.ToString();
        if (!string.IsNullOrEmpty(queryId) || !string.IsNullOrEmpty(planId))
        {
            return $"qs:{queryId ?? "-"}-plan:{planId ?? "-"}";
        }

        return "-";
    }

    private static bool IsDiagnosticWorkload(Dictionary<string, object?> row)
    {
        var text = row.GetValueOrDefault("query_text") as string;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.ToLowerInvariant()
            .Replace("\r", " ")
            .Replace("\n", " ");
        if (normalized.StartsWith("set ", StringComparison.Ordinal))
        {
            return true;
        }

        if (normalized.StartsWith("create statistics", StringComparison.Ordinal) ||
            normalized.StartsWith("create index", StringComparison.Ordinal) ||
            normalized.StartsWith("alter index", StringComparison.Ordinal))
        {
            return true;
        }

        if (normalized.Contains(" from sys.", StringComparison.Ordinal) ||
            normalized.Contains(" join sys.", StringComparison.Ordinal) ||
            normalized.Contains("sys.dm_", StringComparison.Ordinal) ||
            normalized.Contains("sys.all_objects", StringComparison.Ordinal) ||
            normalized.Contains("msdb.dbo", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static long ConvertToLong(object? value)
    {
        if (value is null) return 0;
        if (value is long l) return l;
        return Convert.ToInt64(value);
    }

    private static double ConvertToDouble(object? value)
    {
        if (value is null) return 0d;
        return Convert.ToDouble(value);
    }
}
