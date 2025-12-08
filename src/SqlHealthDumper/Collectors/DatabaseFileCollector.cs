using System.Linq;
using SqlHealthDumper.Domain;
using SqlHealthDumper.Infrastructure;
using SqlHealthDumper.Options;

namespace SqlHealthDumper.Collectors;

/// <summary>
/// データベースごとのファイル構成を取得するコレクター。
/// </summary>
public sealed class DatabaseFileCollector
{
    private readonly ISqlLoader _sqlLoader;
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly SqlExecutionHelper _sql;

    /// <summary>
    /// SQL 実行の依存を注入して初期化する。
    /// </summary>
    public DatabaseFileCollector(ISqlLoader sqlLoader, SqlConnectionFactory connectionFactory, SqlExecutionHelper sql)
    {
        _sqlLoader = sqlLoader;
        _connectionFactory = connectionFactory;
        _sql = sql;
    }

    /// <summary>
    /// 指定データベースでファイル情報クエリを実行し、<see cref="DatabaseFileInfo"/> リストを返す。
    /// </summary>
    public async Task<CollectorResult<List<DatabaseFileInfo>>> CollectAsync(string databaseName, AppConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = _connectionFactory.CreateOpenConnection(config, databaseName);
            var sqlText = _sqlLoader.GetSql("Database.db_files");
            var rows = await _sql.QueryAsync(connection, sqlText, config.Execution.QueryTimeoutSeconds, cancellationToken);
            var files = rows.Select(MapFile).ToList();
            return CollectorResult<List<DatabaseFileInfo>>.Success(files);
        }
        catch (Exception ex)
        {
            return CollectorResult<List<DatabaseFileInfo>>.Failed($"Database file collection failed for {databaseName}", ex);
        }
    }

    private static DatabaseFileInfo MapFile(Dictionary<string, object?> row)
    {
        return new DatabaseFileInfo
        {
            LogicalName = row.GetValueOrDefault("logical_name") as string ?? string.Empty,
            Type = row.GetValueOrDefault("type_desc") as string ?? string.Empty,
            PhysicalPath = row.GetValueOrDefault("physical_name") as string ?? string.Empty,
            SizeMb = ConvertToDouble(row.GetValueOrDefault("size_mb")),
            UsedMb = ConvertToDouble(row.GetValueOrDefault("used_mb")),
            GrowthDescription = row.GetValueOrDefault("growth_desc") as string ?? string.Empty,
            MaxSizeDescription = row.GetValueOrDefault("max_size_desc") as string ?? string.Empty
        };
    }

    private static double ConvertToDouble(object? value)
    {
        if (value is null) return 0d;
        return Convert.ToDouble(value);
    }
}
