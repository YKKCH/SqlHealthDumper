using System;
using System.Linq;
using SqlHealthDumper.Domain;
using SqlHealthDumper.Infrastructure;
using SqlHealthDumper.Options;

namespace SqlHealthDumper.Collectors;

/// <summary>
/// インデックス使用統計と欠損提案を取得するコレクター。
/// </summary>
public sealed class IndexUsageCollector
{
    private readonly ISqlLoader _sqlLoader;
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly SqlExecutionHelper _sql;

    /// <summary>
    /// SQL 実行依存を受け取り、再利用する。
    /// </summary>
    public IndexUsageCollector(ISqlLoader sqlLoader, SqlConnectionFactory connectionFactory, SqlExecutionHelper sql)
    {
        _sqlLoader = sqlLoader;
        _connectionFactory = connectionFactory;
        _sql = sql;
    }

    /// <summary>
    /// 既存/欠損インデックスの情報を収集し、環境制約に応じてスキップも判断する。
    /// </summary>
    public async Task<CollectorResult<IndexInsightSummary>> CollectAsync(string databaseName, AppConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            if (config.Execution.Capabilities.IsAzureSqlDatabase)
            {
                return CollectorResult<IndexInsightSummary>.Skipped("Azure SQL Database では missing index DMV が制限されるためスキップしました。");
            }

            using var connection = _connectionFactory.CreateOpenConnection(config, databaseName);
            var sqlText = _sqlLoader.GetSql("Database.index_usage_and_missing");
            var rows = await _sql.QueryAsync(connection, sqlText, config.Execution.QueryTimeoutSeconds, cancellationToken);
            if (!config.Execution.IncludeSystemObjects)
            {
                rows = rows.Where(row => !IsSystemObject(row)).ToList();
            }

            // Split usage and missing by presence of index_name
            var usage = rows.Where(r => r.ContainsKey("index_name")).Select(MapUsage).ToList();
            var missing = rows.Where(r => r.ContainsKey("group_handle") && r.ContainsKey("statement")).Select(MapMissing).ToList();

            var summary = new IndexInsightSummary();
            summary.Usage.AddRange(usage);
            summary.Missing.AddRange(missing);

            return CollectorResult<IndexInsightSummary>.Success(summary);
        }
        catch (Exception ex)
        {
            return CollectorResult<IndexInsightSummary>.Failed($"Index usage collection failed for {databaseName}", ex);
        }
    }

    private static IndexUsageInfo MapUsage(Dictionary<string, object?> row)
    {
        return new IndexUsageInfo
        {
            ObjectName = $"{row.GetValueOrDefault("schema_name")}.{row.GetValueOrDefault("table_name")}",
            IndexName = row.GetValueOrDefault("index_name") as string,
            Seeks = ConvertToLong(row.GetValueOrDefault("user_seeks")),
            Scans = ConvertToLong(row.GetValueOrDefault("user_scans")),
            Lookups = ConvertToLong(row.GetValueOrDefault("user_lookups")),
            Updates = ConvertToLong(row.GetValueOrDefault("user_updates"))
        };
    }

    private static MissingIndexInfo MapMissing(Dictionary<string, object?> row)
    {
        return new MissingIndexInfo
        {
            ObjectName = row.GetValueOrDefault("statement") as string ?? string.Empty,
            Impact = row.GetValueOrDefault("total_read_est")?.ToString(),
            Definition = BuildDefinition(row)
        };
    }

    private static string BuildDefinition(Dictionary<string, object?> row)
    {
        var eq = row.GetValueOrDefault("equality_columns") as string;
        var ineq = row.GetValueOrDefault("inequality_columns") as string;
        var inc = row.GetValueOrDefault("included_columns") as string;
        return $"EQUALITY: {eq}; INEQUALITY: {ineq}; INCLUDE: {inc}";
    }

    private static long ConvertToLong(object? value)
    {
        if (value is null) return 0;
        if (value is long l) return l;
        return Convert.ToInt64(value);
    }

    private static bool IsSystemObject(Dictionary<string, object?> row)
    {
        var schema = row.GetValueOrDefault("schema_name") as string;
        if (!string.IsNullOrWhiteSpace(schema) && (schema.Equals("sys", StringComparison.OrdinalIgnoreCase) || schema.Equals("internal", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var table = row.GetValueOrDefault("table_name") as string;
        if (!string.IsNullOrWhiteSpace(table) && table.StartsWith("sys", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var statement = row.GetValueOrDefault("statement") as string;
        if (!string.IsNullOrWhiteSpace(statement) && statement.Contains("sys.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
