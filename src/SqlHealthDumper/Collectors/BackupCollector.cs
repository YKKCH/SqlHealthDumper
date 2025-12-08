using Microsoft.Data.SqlClient;
using SqlHealthDumper.Domain;
using SqlHealthDumper.Infrastructure;
using SqlHealthDumper.Options;

namespace SqlHealthDumper.Collectors;

/// <summary>
/// バックアップ履歴や Agent ジョブ状況を収集するコレクター。
/// </summary>
public sealed class BackupCollector
{
    private readonly ISqlLoader _sqlLoader;
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly SqlExecutionHelper _sql;
    private const string MsdbUnavailableMessage = "msdb が存在しないためバックアップ/ジョブ情報は取得できません（Azure SQL Database 等）。";

    /// <summary>
    /// バックアップ収集に必要な依存関係を注入する。
    /// </summary>
    public BackupCollector(ISqlLoader sqlLoader, SqlConnectionFactory connectionFactory, SqlExecutionHelper sql)
    {
        _sqlLoader = sqlLoader;
        _connectionFactory = connectionFactory;
        _sql = sql;
    }

    /// <summary>
    /// msdb 利用可否を確認した上でバックアップ/ジョブ情報を収集する。
    /// </summary>
    public async Task<CollectorResult<BackupMaintenanceInfo>> CollectAsync(string databaseName, AppConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = _connectionFactory.CreateOpenConnection(config, databaseName);
            var hasMsdb = await HasMsdbAsync(connection, config.Execution.QueryTimeoutSeconds, cancellationToken);
            if (!hasMsdb)
            {
                return CollectorResult<BackupMaintenanceInfo>.Skipped(MsdbUnavailableMessage, new BackupMaintenanceInfo
                {
                    IsSupported = false,
                    NotAvailableReason = MsdbUnavailableMessage
                });
            }

            var sqlText = _sqlLoader.GetSql("Database.backup_and_maintenance");
            var rows = await _sql.QueryAsync(connection, sqlText, config.Execution.QueryTimeoutSeconds, cancellationToken);

            var info = new BackupMaintenanceInfo();
            var checkDbByJob = new Dictionary<string, CheckDbEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                if (row.ContainsKey("backup_start_date"))
                {
                    info.Backups.Add(MapBackup(row));
                }
                else if (row.ContainsKey("job_name"))
                {
                    var job = MapJob(row);
                    info.AgentJobs.Add(job);
                    UpdateCheckDb(checkDbByJob, row, job);
                }
            }

            if (checkDbByJob.Count > 0)
            {
                info.CheckDb.AddRange(checkDbByJob.Values);
            }

            return CollectorResult<BackupMaintenanceInfo>.Success(info);
        }
        catch (Exception ex)
        {
            return CollectorResult<BackupMaintenanceInfo>.Failed($"Backup collection failed for {databaseName}", ex);
        }
    }

    private static BackupHistoryEntry MapBackup(Dictionary<string, object?> row)
    {
        return new BackupHistoryEntry
        {
            Type = row.GetValueOrDefault("backup_type") as string ?? string.Empty,
            StartTime = row.GetValueOrDefault("backup_start_date") as DateTime?,
            Duration = CalcDuration(row),
            SizeMb = ConvertToDouble(row.GetValueOrDefault("backup_size_mb"))
        };
    }

    private static TimeSpan? CalcDuration(Dictionary<string, object?> row)
    {
        var start = row.GetValueOrDefault("backup_start_date") as DateTime?;
        var finish = row.GetValueOrDefault("backup_finish_date") as DateTime?;
        if (start.HasValue && finish.HasValue)
        {
            return finish - start;
        }
        return null;
    }

    private static AgentJobEntry MapJob(Dictionary<string, object?> row)
    {
        return new AgentJobEntry
        {
            Name = row.GetValueOrDefault("job_name") as string ?? string.Empty,
            LastRun = ParseJobRun(row),
            LastRunSucceeded = ConvertToInt(row.GetValueOrDefault("run_status")) == 1,
            Note = row.GetValueOrDefault("message") as string
        };
    }

    private static DateTime? ParseJobRun(Dictionary<string, object?> row)
    {
        // run_date in YYYYMMDD, run_time in HHMMSS
        var date = ConvertToInt(row.GetValueOrDefault("run_date"));
        var time = ConvertToInt(row.GetValueOrDefault("run_time"));
        if (date == 0) return null;

        var dateStr = date.ToString("00000000");
        var timeStr = time.ToString("000000");
        if (DateTime.TryParseExact($"{dateStr}{timeStr}", "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out var dt))
        {
            return dt;
        }
        return null;
    }

    private static void UpdateCheckDb(Dictionary<string, CheckDbEntry> checkDbByJob, Dictionary<string, object?> row, AgentJobEntry job)
    {
        if (string.IsNullOrWhiteSpace(job.Name) || job.LastRun is null)
        {
            return;
        }

        if (!job.Name.Contains("CHECKDB", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!checkDbByJob.TryGetValue(job.Name, out var entry))
        {
            entry = new CheckDbEntry { JobName = job.Name };
            checkDbByJob[job.Name] = entry;
        }

        if (job.LastRunSucceeded)
        {
            if (entry.LastSuccess is null || job.LastRun > entry.LastSuccess)
            {
                entry.LastSuccess = job.LastRun;
            }
        }
        else
        {
            if (entry.LastFailure is null || job.LastRun > entry.LastFailure)
            {
                entry.LastFailure = job.LastRun;
                entry.ErrorMessage = row.GetValueOrDefault("message") as string;
            }
        }
    }

    private static int ConvertToInt(object? value)
    {
        if (value is null) return 0;
        if (value is int i) return i;
        return Convert.ToInt32(value);
    }

    private static double ConvertToDouble(object? value)
    {
        if (value is null) return 0d;
        return Convert.ToDouble(value);
    }

    private static async Task<bool> HasMsdbAsync(SqlConnection connection, int timeoutSeconds, CancellationToken cancellationToken)
    {
        const string sql = "SELECT DB_ID('msdb') AS msdb_id;";
        try
        {
            using var cmd = new SqlCommand(sql, connection)
            {
                CommandTimeout = timeoutSeconds
            };
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return result is not null && result != DBNull.Value;
        }
        catch
        {
            return false;
        }
    }
}
