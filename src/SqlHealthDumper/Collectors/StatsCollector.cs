using System;
using SqlHealthDumper.Domain;
using SqlHealthDumper.Infrastructure;
using SqlHealthDumper.Options;

namespace SqlHealthDumper.Collectors;

/// <summary>
/// DMV ベースで統計情報の概要を収集するコレクター。
/// </summary>
public sealed class StatsCollector
{
    private readonly ISqlLoader _sqlLoader;
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly SqlExecutionHelper _sql;

    /// <summary>
    /// SQL 実行に必要な依存を注入して初期化する。
    /// </summary>
    public StatsCollector(ISqlLoader sqlLoader, SqlConnectionFactory connectionFactory, SqlExecutionHelper sql)
    {
        _sqlLoader = sqlLoader;
        _connectionFactory = connectionFactory;
        _sql = sql;
    }

    /// <summary>
    /// 統計情報 SQL を実行し、環境制約に応じてスキップ判定も行う。
    /// </summary>
    public async Task<CollectorResult<List<StatsInsight>>> CollectAsync(string databaseName, AppConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!config.Execution.Capabilities.SupportsDmDbStatsProperties)
            {
                return CollectorResult<List<StatsInsight>>.Skipped("dm_db_stats_properties を利用できない環境のため統計収集をスキップしました。", new List<StatsInsight>());
            }

            using var connection = _connectionFactory.CreateOpenConnection(config, databaseName);
            var sqlText = _sqlLoader.GetSql("Database.stats_and_params");
            var rows = await _sql.QueryAsync(connection, sqlText, config.Execution.QueryTimeoutSeconds, cancellationToken);
            var mapped = rows.Select(MapStats).ToList();
            return CollectorResult<List<StatsInsight>>.Success(mapped);
        }
        catch (Exception ex)
        {
            return CollectorResult<List<StatsInsight>>.Failed($"Stats collection failed for {databaseName}", ex);
        }
    }

    private static StatsInsight MapStats(Dictionary<string, object?> row)
    {
        var rows = ConvertToLong(row.GetValueOrDefault("effective_rows"));
        if (rows == 0)
        {
            rows = ConvertToLong(row.GetValueOrDefault("rows"));
        }

        return new StatsInsight
        {
            StatName = row.GetValueOrDefault("stat_name") as string ?? string.Empty,
            LastUpdated = row.GetValueOrDefault("last_updated") as DateTime?,
            Rows = rows,
            RowsSampled = ConvertToLong(row.GetValueOrDefault("rows_sampled")),
            ModificationCounter = ConvertToLong(row.GetValueOrDefault("modification_counter")),
            RowsEstimated = ConvertToBool(row.GetValueOrDefault("rows_estimated")),
            HistogramUnavailable = ConvertToBool(row.GetValueOrDefault("histogram_unavailable"))
        };
    }

    private static long ConvertToLong(object? value)
    {
        if (value is null) return 0;
        if (value is long l) return l;
        return Convert.ToInt64(value);
    }

    private static bool ConvertToBool(object? value)
    {
        if (value is null) return false;
        if (value is bool b) return b;
        return Convert.ToInt32(value) != 0;
    }
}
