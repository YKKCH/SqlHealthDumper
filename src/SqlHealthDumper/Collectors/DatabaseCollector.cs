using System;
using SqlHealthDumper.Domain;
using SqlHealthDumper.Infrastructure;
using SqlHealthDumper.Options;

namespace SqlHealthDumper.Collectors;

/// <summary>
/// データベース単位のメタ情報を収集し、絞り込みを適用するコレクター。
/// </summary>
public sealed class DatabaseCollector
{
    private readonly ISqlLoader _sqlLoader;
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly SqlExecutionHelper _sql;

    /// <summary>
    /// 必要な依存を注入し、再利用可能な形で保持する。
    /// </summary>
    public DatabaseCollector(ISqlLoader sqlLoader, SqlConnectionFactory connectionFactory, SqlExecutionHelper sql)
    {
        _sqlLoader = sqlLoader;
        _connectionFactory = connectionFactory;
        _sql = sql;
    }

    /// <summary>
    /// DB overview SQL を実行し、指定フィルターを反映した <see cref="DatabaseSnapshot"/> リストを生成する。
    /// </summary>
    public async Task<CollectorResult<List<DatabaseSnapshot>>> CollectAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = _connectionFactory.CreateOpenConnection(config);
            var sqlText = _sqlLoader.GetSql("Database.db_overview");
            var rows = await _sql.QueryAsync(connection, sqlText, config.Execution.QueryTimeoutSeconds, cancellationToken);

            var filtered = ApplyFilters(rows, config.Execution);
            var snapshots = filtered.Select(MapDatabase).ToList();
            return CollectorResult<List<DatabaseSnapshot>>.Success(snapshots);
        }
        catch (Exception ex)
        {
            return CollectorResult<List<DatabaseSnapshot>>.Failed("Database collection failed", ex);
        }
    }

    private static IEnumerable<Dictionary<string, object?>> ApplyFilters(List<Dictionary<string, object?>> rows, ExecutionOptions execution)
    {
        foreach (var row in rows)
        {
            var name = row.GetValueOrDefault("name") as string ?? string.Empty;
            if (!execution.IncludeSystemDatabases && IsSystemDatabase(name))
            {
                continue;
            }

            if (execution.IncludeDatabases.Count > 0 && !execution.IncludeDatabases.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (execution.ExcludeDatabases.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return row;
        }
    }

    private static bool IsSystemDatabase(string name)
    {
        return name.Equals("master", StringComparison.OrdinalIgnoreCase)
            || name.Equals("tempdb", StringComparison.OrdinalIgnoreCase)
            || name.Equals("model", StringComparison.OrdinalIgnoreCase)
            || name.Equals("msdb", StringComparison.OrdinalIgnoreCase);
    }

    private static DatabaseSnapshot MapDatabase(Dictionary<string, object?> row)
    {
        var snapshot = new DatabaseSnapshot
        {
            Name = row.GetValueOrDefault("name") as string ?? string.Empty,
            CompatibilityLevel = ConvertToInt(row.GetValueOrDefault("compatibility_level")),
            RecoveryModel = row.GetValueOrDefault("recovery_model_desc") as string,
            LogReuseWaitDescription = row.GetValueOrDefault("log_reuse_wait_desc") as string,
            IsReadOnly = ConvertToBool(row.GetValueOrDefault("is_read_only")),
            IsEncrypted = ConvertToBool(row.GetValueOrDefault("is_encrypted")),
            IsHadrEnabled = ConvertToBool(row.GetValueOrDefault("is_hadr_enabled")),
            SizeInfo = new DatabaseSizeInfo
            {
                DataSizeMb = ConvertToDouble(row.GetValueOrDefault("data_size_mb")),
                LogSizeMb = ConvertToDouble(row.GetValueOrDefault("log_size_mb")),
                FreeSpaceMb = ConvertToDouble(row.GetValueOrDefault("free_space_mb"))
            }
        };

        AppendNotes(snapshot);
        return snapshot;
    }

    private static void AppendNotes(DatabaseSnapshot snapshot)
    {
        if (snapshot.IsReadOnly)
        {
            snapshot.Notes.Add("読み取り専用モードです。");
        }

        if (!snapshot.IsEncrypted)
        {
            snapshot.Notes.Add("Transparent Data Encryption は無効です。");
        }

        if (snapshot.CompatibilityLevel > 0 && snapshot.CompatibilityLevel < 150)
        {
            snapshot.Notes.Add($"互換性レベル {snapshot.CompatibilityLevel} のため一部最新機能が無効です。");
        }

        var dataSize = snapshot.SizeInfo.DataSizeMb;
        if (dataSize > 0)
        {
            var freeRatio = snapshot.SizeInfo.FreeSpaceMb / dataSize;
            if (freeRatio > 0.25)
            {
                snapshot.Notes.Add($"データファイル空き容量が {freeRatio:P0} です。必要なら圧縮や縮小を検討してください。");
            }
        }

        if (!string.IsNullOrWhiteSpace(snapshot.LogReuseWaitDescription) && !snapshot.LogReuseWaitDescription.Equals("NOTHING", StringComparison.OrdinalIgnoreCase))
        {
            snapshot.Notes.Add($"ログ再利用待ち: {snapshot.LogReuseWaitDescription}");
        }
    }

    private static int ConvertToInt(object? value) => value is null ? 0 : Convert.ToInt32(value);
    private static bool ConvertToBool(object? value)
    {
        if (value is null) return false;
        if (value is bool b) return b;
        return Convert.ToInt32(value) != 0;
    }
    private static double ConvertToDouble(object? value) => value is null ? 0d : Convert.ToDouble(value);
}
