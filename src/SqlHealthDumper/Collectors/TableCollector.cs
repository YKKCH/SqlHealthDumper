using System.Linq;
using SqlHealthDumper.Domain;
using SqlHealthDumper.Infrastructure;
using SqlHealthDumper.Options;

namespace SqlHealthDumper.Collectors;

/// <summary>
/// テーブルメタデータと統計情報を包括的に収集するコレクター。
/// </summary>
public sealed class TableCollector
{
    private readonly ISqlLoader _sqlLoader;
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly SqlExecutionHelper _sql;
    private readonly ILogger? _logger;

    /// <summary>
    /// SQL ローダー/接続工場/実行ヘルパーおよび任意のロガーを受け取って初期化する。
    /// </summary>
    public TableCollector(ISqlLoader sqlLoader, SqlConnectionFactory connectionFactory, SqlExecutionHelper sql, ILogger? logger = null)
    {
        _sqlLoader = sqlLoader;
        _connectionFactory = connectionFactory;
        _sql = sql;
        _logger = logger;
    }

    /// <summary>
    /// テーブル概要から統計ヒストグラムまで段階的にロードし、<see cref="TableSnapshot"/> の一覧を返す。
    /// </summary>
    public async Task<CollectorResult<List<TableSnapshot>>> CollectAsync(string databaseName, AppConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = _connectionFactory.CreateOpenConnection(config, databaseName);

            var tables = await LoadTableOverviewAsync(connection, config.Execution.QueryTimeoutSeconds, cancellationToken);
            if (tables.Count == 0)
            {
                return CollectorResult<List<TableSnapshot>>.Success(new List<TableSnapshot>());
            }

            await LoadColumnsAsync(connection, tables, config.Execution.QueryTimeoutSeconds, cancellationToken);
            await LoadIndexesAsync(connection, tables, config.Execution.QueryTimeoutSeconds, cancellationToken);
            await LoadConstraintsAsync(connection, tables, config.Execution.QueryTimeoutSeconds, cancellationToken);
            await LoadTriggersAsync(connection, tables, config.Execution.QueryTimeoutSeconds, cancellationToken);
            await LoadStatsAsync(connection, tables, config, databaseName, config.Execution.QueryTimeoutSeconds, cancellationToken);

            return CollectorResult<List<TableSnapshot>>.Success(tables.Values.ToList());
        }
        catch (Exception ex)
        {
            return CollectorResult<List<TableSnapshot>>.Failed($"Table collection failed for {databaseName}", ex);
        }
    }

    private async Task<Dictionary<string, TableSnapshot>> LoadTableOverviewAsync(Microsoft.Data.SqlClient.SqlConnection connection, int timeoutSeconds, CancellationToken cancellationToken)
    {
        var sqlText = _sqlLoader.GetSql("Table.table_overview");
        var rows = await _sql.QueryAsync(connection, sqlText, timeoutSeconds, cancellationToken);

        var map = new Dictionary<string, TableSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var schema = row.GetValueOrDefault("schema_name") as string ?? string.Empty;
            var table = row.GetValueOrDefault("table_name") as string ?? string.Empty;
            var key = $"{schema}.{table}";
            if (string.IsNullOrWhiteSpace(schema) || string.IsNullOrWhiteSpace(table))
            {
                continue;
            }

            map[key] = new TableSnapshot
            {
                Schema = schema,
                TableName = table,
                RowCount = ConvertToLong(row.GetValueOrDefault("row_count")),
                DataSizeMb = ConvertToDouble(row.GetValueOrDefault("data_size_mb")),
                IndexSizeMb = ConvertToDouble(row.GetValueOrDefault("index_size_mb"))
            };
        }

        return map;
    }

    private async Task LoadColumnsAsync(Microsoft.Data.SqlClient.SqlConnection connection, Dictionary<string, TableSnapshot> tables, int timeoutSeconds, CancellationToken cancellationToken)
    {
        var sqlText = _sqlLoader.GetSql("Table.table_columns");
        var rows = await _sql.QueryAsync(connection, sqlText, timeoutSeconds, cancellationToken);

        foreach (var row in rows)
        {
            var snapshot = GetTable(tables, row);
            if (snapshot is null) continue;

            snapshot.Columns.Add(new ColumnDefinition
            {
                Name = row.GetValueOrDefault("column_name") as string ?? string.Empty,
                DataType = FormatDataType(row),
                IsNullable = ConvertToBool(row.GetValueOrDefault("is_nullable")),
                DefaultValue = row.GetValueOrDefault("default_value") as string
            });
        }
    }

    private async Task LoadIndexesAsync(Microsoft.Data.SqlClient.SqlConnection connection, Dictionary<string, TableSnapshot> tables, int timeoutSeconds, CancellationToken cancellationToken)
    {
        var sqlText = _sqlLoader.GetSql("Table.table_indexes");
        var rows = await _sql.QueryAsync(connection, sqlText, timeoutSeconds, cancellationToken);

        foreach (var row in rows)
        {
            var snapshot = GetTable(tables, row);
            if (snapshot is null) continue;

            var indexName = row.GetValueOrDefault("index_name") as string ?? string.Empty;
            if (string.IsNullOrWhiteSpace(indexName)) continue;

            var index = snapshot.Indexes.FirstOrDefault(i => i.Name.Equals(indexName, StringComparison.OrdinalIgnoreCase));
            if (index is null)
            {
                index = new IndexDefinition
                {
                    Name = indexName,
                    IsClustered = ConvertToBool(row.GetValueOrDefault("is_clustered")),
                    IsUnique = ConvertToBool(row.GetValueOrDefault("is_unique"))
                };
                snapshot.Indexes.Add(index);
            }

            var columnName = row.GetValueOrDefault("column_name") as string;
            if (string.IsNullOrWhiteSpace(columnName)) continue;

            var isIncluded = ConvertToBool(row.GetValueOrDefault("is_included_column"));
            if (isIncluded)
            {
                index.IncludeColumns.Add(columnName);
            }
            else
            {
                index.KeyColumns.Add(columnName);
            }
        }
    }

    private async Task LoadConstraintsAsync(Microsoft.Data.SqlClient.SqlConnection connection, Dictionary<string, TableSnapshot> tables, int timeoutSeconds, CancellationToken cancellationToken)
    {
        var sqlText = _sqlLoader.GetSql("Table.table_constraints");
        var rows = await _sql.QueryAsync(connection, sqlText, timeoutSeconds, cancellationToken);

        foreach (var row in rows)
        {
            var snapshot = GetTable(tables, row);
            if (snapshot is null) continue;

            snapshot.Constraints.Add(new ConstraintDefinition
            {
                Name = row.GetValueOrDefault("constraint_name") as string ?? string.Empty,
                Type = MapConstraintType(row.GetValueOrDefault("constraint_type") as string)
            });
        }
    }

    private async Task LoadTriggersAsync(Microsoft.Data.SqlClient.SqlConnection connection, Dictionary<string, TableSnapshot> tables, int timeoutSeconds, CancellationToken cancellationToken)
    {
        var sqlText = _sqlLoader.GetSql("Table.table_triggers");
        var rows = await _sql.QueryAsync(connection, sqlText, timeoutSeconds, cancellationToken);

        foreach (var row in rows)
        {
            var snapshot = GetTable(tables, row);
            if (snapshot is null) continue;

            snapshot.Triggers.Add(new TriggerDefinition
            {
                Name = row.GetValueOrDefault("trigger_name") as string ?? string.Empty,
                IsEnabled = !ConvertToBool(row.GetValueOrDefault("is_disabled"))
            });
        }
    }

    private async Task LoadStatsAsync(Microsoft.Data.SqlClient.SqlConnection connection, Dictionary<string, TableSnapshot> tables, AppConfig config, string databaseName, int timeoutSeconds, CancellationToken cancellationToken)
    {
        var statsSql = _sqlLoader.GetSql("Table.table_stats");
        var histogramTemplate = _sqlLoader.GetSql("Table.table_histogram");
        var rows = await _sql.QueryAsync(connection, statsSql, timeoutSeconds, cancellationToken);

        if (rows.Count == 0)
        {
            _logger?.Info($"統計情報が {databaseName} で見つからなかったため、テーブル統計の詳細は生成されません。");
        }

        var histogramAvailable = await IsHistogramAvailableAsync(connection, timeoutSeconds, cancellationToken);
        if (!histogramAvailable)
        {
            _logger?.Info($"sys.dm_db_stats_histogram が {databaseName} で使用できないため、ヒストグラムをスキップします。");
        }

        var histogramTasks = new List<Task>();
        var histGate = new SemaphoreSlim(Math.Max(1, config.Execution.TableMaxParallelism));

        foreach (var row in rows)
        {
            var snapshot = GetTable(tables, row);
            if (snapshot is null) continue;

            var stat = new StatsInsight
            {
                StatName = row.GetValueOrDefault("stat_name") as string ?? string.Empty,
                LastUpdated = row.GetValueOrDefault("last_updated") as DateTime?,
                Rows = ConvertToLong(row.GetValueOrDefault("rows")),
                RowsSampled = ConvertToLong(row.GetValueOrDefault("rows_sampled")),
                ModificationCounter = ConvertToLong(row.GetValueOrDefault("modification_counter")),
                HistogramUnavailable = !histogramAvailable
            };

            var objectId = ConvertToInt(row.GetValueOrDefault("object_id"));
            var statsId = ConvertToInt(row.GetValueOrDefault("stats_id"));
            if (stat.Rows == 0 && snapshot.RowCount > 0)
            {
                stat.Rows = snapshot.RowCount;
                stat.RowsEstimated = true;
                _logger?.Info($"統計 {snapshot.Schema}.{snapshot.TableName}.{stat.StatName} の行数をテーブル行数で補完しました。");
            }

            if (histogramAvailable && objectId > 0 && statsId > 0)
            {
                var histogramSql = string.Format(histogramTemplate, objectId, statsId);
                if (config.Execution.TableMaxParallelism <= 1)
                {
                    var buckets = await _sql.QueryAsync(connection, histogramSql, timeoutSeconds, cancellationToken);
                    var mapped = MapHistogramBuckets(buckets);
                    stat.HistogramSummary = BuildHistogramSummary(mapped);
                    stat.HistogramDetail.AddRange(mapped);
                    stat.HistogramUnavailable = false;
                }
                else
                {
                    histogramTasks.Add(FetchHistogramParallelAsync(stat, histogramSql, config, databaseName, timeoutSeconds, histGate, cancellationToken));
                }
            }
            else if (histogramAvailable)
            {
                stat.HistogramUnavailable = true;
                _logger?.Info($"ヒストグラムを {snapshot.Schema}.{snapshot.TableName}.{stat.StatName} でスキップしました（object_id: {objectId}, stats_id: {statsId}）。");
            }
            snapshot.Stats.Add(stat);
        }

        if (histogramTasks.Count > 0)
        {
            await Task.WhenAll(histogramTasks);
        }
    }

    private static string? CachedHistogramRowCountColumn;

    private static List<HistogramBucket> MapHistogramBuckets(List<Dictionary<string, object?>> rows)
    {
        var eqRowsColumn = CachedHistogramRowCountColumn ??= DetectHistogramCountColumn(rows.FirstOrDefault() ?? new Dictionary<string, object?>());
        var mapped = new List<HistogramBucket>(rows.Count);
        double? previousHigh = null;

        foreach (var row in rows)
        {
            var high = ConvertToDouble(row.GetValueOrDefault("range_high_key"));
            var bucket = new HistogramBucket
            {
                RangeFrom = previousHigh ?? high,
                RangeTo = high,
                EqualRows = ConvertToDouble(row.GetValueOrDefault(eqRowsColumn)),
                RangeRows = ConvertToDouble(row.GetValueOrDefault("range_rows")),
                DistinctRangeRows = ConvertToDouble(row.GetValueOrDefault("distinct_range_rows"))
            };
            mapped.Add(bucket);
            previousHigh = high;
        }

        return mapped;
    }

    private static string DetectHistogramCountColumn(Dictionary<string, object?> row)
    {
        if (row.ContainsKey("equal_rows"))
        {
            return "equal_rows";
        }
        if (row.ContainsKey("eq_rows"))
        {
            return "eq_rows";
        }
        return "eq_rows";
    }

    private static HistogramSummary BuildHistogramSummary(List<HistogramBucket> buckets)
    {
        if (buckets.Count == 0) return new HistogramSummary();
        // 間引き: 最大15ステップまで拾う
        var maxSteps = 15;
        var step = Math.Max(1, buckets.Count / maxSteps);
        var summary = new HistogramSummary();
        for (var i = 0; i < buckets.Count; i += step)
        {
            summary.Buckets.Add(buckets[i]);
            if (summary.Buckets.Count >= maxSteps) break;
        }
        return summary;
    }

    private static TableSnapshot? GetTable(Dictionary<string, TableSnapshot> tables, Dictionary<string, object?> row)
    {
        var schema = row.GetValueOrDefault("schema_name") as string ?? string.Empty;
        var table = row.GetValueOrDefault("table_name") as string ?? string.Empty;
        var key = $"{schema}.{table}";
        return tables.TryGetValue(key, out var snap) ? snap : null;
    }

    private static string FormatDataType(Dictionary<string, object?> row)
    {
        var dataType = row.GetValueOrDefault("data_type") as string ?? string.Empty;
        var maxLength = ConvertToInt(row.GetValueOrDefault("max_length"));
        var precision = Convert.ToByte(row.GetValueOrDefault("precision") ?? (byte)0);
        var scale = Convert.ToByte(row.GetValueOrDefault("scale") ?? (byte)0);

        var adjustedLength = dataType.StartsWith("n", StringComparison.OrdinalIgnoreCase)
            ? (maxLength > 0 ? maxLength / 2 : maxLength)
            : maxLength;

        return dataType.ToLower() switch
        {
            "varchar" or "nvarchar" or "char" or "nchar" or "binary" or "varbinary" => $"{dataType}({(maxLength == -1 ? "max" : adjustedLength)})",
            "decimal" or "numeric" => $"{dataType}({precision},{scale})",
            _ => dataType
        };
    }

    private static string MapConstraintType(string? type)
    {
        return type switch
        {
            "PK" => "PRIMARY KEY",
            "UQ" => "UNIQUE",
            "F" => "FOREIGN KEY",
            "C" => "CHECK",
            _ => type ?? string.Empty
        };
    }

    private static bool ConvertToBool(object? value)
    {
        if (value is null) return false;
        if (value is bool b) return b;
        return Convert.ToInt32(value) != 0;
    }

    private async Task<bool> IsHistogramAvailableAsync(Microsoft.Data.SqlClient.SqlConnection connection, int timeoutSeconds, CancellationToken cancellationToken)
    {
        const string sql = "SELECT CASE WHEN OBJECT_ID('sys.dm_db_stats_histogram') IS NOT NULL THEN 1 ELSE 0 END AS available;";
        try
        {
            var rows = await _sql.QueryAsync(connection, sql, timeoutSeconds, cancellationToken);
            var first = rows.FirstOrDefault();
            return ConvertToBool(first?.GetValueOrDefault("available"));
        }
        catch
        {
            return false;
        }
    }

    private async Task FetchHistogramParallelAsync(StatsInsight stat, string histogramSql, AppConfig config, string databaseName, int timeoutSeconds, SemaphoreSlim gate, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            using var conn = _connectionFactory.CreateOpenConnection(config, databaseName);
            var rows = await _sql.QueryAsync(conn, histogramSql, timeoutSeconds, cancellationToken);
            var mapped = MapHistogramBuckets(rows);
            stat.HistogramSummary = BuildHistogramSummary(mapped);
            stat.HistogramDetail.AddRange(mapped);
        }
        finally
        {
            gate.Release();
        }
    }

    private static int ConvertToInt(object? value)
    {
        if (value is null) return 0;
        if (value is int i) return i;
        return Convert.ToInt32(value);
    }

    private static long ConvertToLong(object? value)
    {
        if (value is null) return 0;
        if (value is long l) return l;
        return Convert.ToInt64(value);
    }

    private static double ConvertToDouble(object? value)
    {
        if (value is null || value == DBNull.Value) return 0d;
        
        if (value is double d) return d;
        if (value is float f) return f;
        if (value is decimal dec) return (double)dec;
        if (value is int i) return i;
        if (value is long l) return l;
        if (value is short s) return s;
        if (value is byte b) return b;
        if (value is uint ui) return ui;
        if (value is ulong ul) return ul;
        if (value is ushort us) return us;
        if (value is sbyte sb) return sb;
        
        if (value is string str && double.TryParse(str, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }
        
        try
        {
            return Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0d;
        }
    }
}
