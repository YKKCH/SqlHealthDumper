using System;
using System.Data.SqlClient;
using System.Linq;
using SqlHealthDumper.Domain;
using SqlHealthDumper.Infrastructure;
using SqlHealthDumper.Options;

namespace SqlHealthDumper.Collectors;

/// <summary>
/// インスタンス全体のメタ情報と待機統計を収集するコレクター。
/// </summary>
public sealed class InstanceCollector
{
    private readonly ISqlLoader _sqlLoader;
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly SqlExecutionHelper _sql;

    /// <summary>
    /// SQL ローダー/接続/実行ヘルパーを受け取り、再利用する。
    /// </summary>
    public InstanceCollector(ISqlLoader sqlLoader, SqlConnectionFactory connectionFactory, SqlExecutionHelper sql)
    {
        _sqlLoader = sqlLoader;
        _connectionFactory = connectionFactory;
        _sql = sql;
    }

    private static void ApplyResourceSummary(InstanceSnapshot snapshot, List<Dictionary<string, object?>> rows)
    {
        var first = rows.FirstOrDefault();
        if (first is null) return;

        snapshot.ResourceSummary = new InstanceResourceSummary
        {
            CpuCount = ConvertToInt(first.GetValueOrDefault("cpu_count")),
            PhysicalMemoryMb = Convert.ToDouble(first.GetValueOrDefault("physical_memory_mb") ?? 0d),
            CommittedMemoryMb = Convert.ToDouble(first.GetValueOrDefault("committed_memory_mb") ?? 0d),
            TargetMemoryMb = Convert.ToDouble(first.GetValueOrDefault("committed_target_mb") ?? 0d),
            MaxWorkerCount = ConvertToInt(first.GetValueOrDefault("max_workers_count"))
        };
    }

    private static void ApplyWaits(InstanceSnapshot snapshot, List<Dictionary<string, object?>> rows)
    {
        snapshot.Waits.Clear();
        foreach (var row in rows)
        {
            snapshot.Waits.Add(new InstanceWaitSummary
            {
                Name = row.GetValueOrDefault("wait_type") as string ?? string.Empty,
                WaitMsPerSec = Convert.ToDouble(row.GetValueOrDefault("resource_wait_ms") ?? 0d),
                SignalMsPerSec = Convert.ToDouble(row.GetValueOrDefault("signal_wait_time_ms") ?? 0d),
                PercentOfTotal = Convert.ToDouble(row.GetValueOrDefault("percent_of_total") ?? 0d)
            });
        }
    }

    /// <summary>
    /// インスタンス overview/resouce/wait クエリを順に実行し、<see cref="InstanceSnapshot"/> を構築する。
    /// </summary>
    public async Task<CollectorResult<InstanceSnapshot>> CollectAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = _connectionFactory.CreateOpenConnection(config);
            var overviewSql = _sqlLoader.GetSql("Instance.instance_overview");
            var overviewRows = await _sql.QueryAsync(connection, overviewSql, config.Execution.QueryTimeoutSeconds, cancellationToken);

            var snapshot = MapInstance(overviewRows);

            var resourceSql = _sqlLoader.GetSql("Instance.instance_resources");
            var resourceRows = await _sql.QueryAsync(connection, resourceSql, config.Execution.QueryTimeoutSeconds, cancellationToken);
            ApplyResourceSummary(snapshot, resourceRows);

            var waitSql = _sqlLoader.GetSql("Instance.instance_waits");
            var waitRows = await _sql.QueryAsync(connection, waitSql, config.Execution.QueryTimeoutSeconds, cancellationToken);
            ApplyWaits(snapshot, waitRows);
            return CollectorResult<InstanceSnapshot>.Success(snapshot);
        }
        catch (Exception ex)
        {
            return CollectorResult<InstanceSnapshot>.Failed("Instance collection failed", ex);
        }
    }

    private static InstanceSnapshot MapInstance(List<Dictionary<string, object?>> rows)
    {
        var snap = new InstanceSnapshot();
        var first = rows.FirstOrDefault();
        if (first is not null)
        {
            snap.Edition = first.GetValueOrDefault("edition") as string;
            snap.Version = first.GetValueOrDefault("product_version") as string;
            snap.Build = first.GetValueOrDefault("product_level") as string;
            snap.Environment = MapEnvironment(first);
            snap.Capabilities = BuildCapabilities(first);
        }

        AddNotes(snap, first, rows.Count);
        return snap;
    }

    private static void AddNotes(InstanceSnapshot snapshot, Dictionary<string, object?>? firstRow, int rowCount)
    {
        if (rowCount == 0)
        {
            snapshot.Notes.Add("インスタンス情報を取得できませんでした。");
            return;
        }

        snapshot.Notes.Add(snapshot.Capabilities.SupportsQueryStore
            ? "Query Store サポート済み"
            : "Query Store 未サポート（SQL Server 2014 以前）");

        if (!snapshot.Capabilities.SupportsDmDbStatsProperties)
        {
            snapshot.Notes.Add("dm_db_stats_properties が未サポートのため統計詳細が一部取得できません。");
        }

        snapshot.Notes.Add($"検出環境: {snapshot.Environment}");

        if (firstRow is not null)
        {
            var hostPlatform = firstRow.GetValueOrDefault("host_platform") as string;
            var hostDistribution = firstRow.GetValueOrDefault("host_distribution") as string;
            if (!string.IsNullOrWhiteSpace(hostPlatform))
            {
                var distribution = string.IsNullOrWhiteSpace(hostDistribution) ? string.Empty : $" ({hostDistribution})";
                snapshot.Notes.Add($"ホスト: {hostPlatform}{distribution}");
            }

            var machineName = firstRow.GetValueOrDefault("machine_name") as string;
            if (!string.IsNullOrWhiteSpace(machineName))
            {
                snapshot.Notes.Add($"マシン名: {machineName}");
            }

            if (snapshot.Environment == EnvironmentKind.Container)
            {
                snapshot.Notes.Add("コンテナ環境（Docker等）で稼働しています。");
            }
        }
    }

    private static EnvironmentKind MapEnvironment(Dictionary<string, object?> row)
    {
        var engineEdition = row.GetValueOrDefault("engine_edition") ?? row.GetValueOrDefault("engine_edition_raw");
        var editionValue = ConvertToInt(engineEdition);

        var environment = editionValue switch
        {
            5 => EnvironmentKind.AzureSqlDatabase, // Azure SQL Database
            8 => EnvironmentKind.AzureSqlManagedInstance, // Azure SQL Managed Instance
            _ => EnvironmentKind.OnPremises
        };

        if (environment == EnvironmentKind.OnPremises && IsContainerEnvironment(row))
        {
            return EnvironmentKind.Container;
        }

        return environment;
    }

    private static VersionCapability BuildCapabilities(Dictionary<string, object?> row)
    {
        var productVersion = row.GetValueOrDefault("product_version") as string;
        var major = ParseMajor(productVersion);
        var environment = MapEnvironment(row);

        return new VersionCapability
        {
            IsAzureSqlDatabase = environment == EnvironmentKind.AzureSqlDatabase,
            IsAzureManagedInstance = environment == EnvironmentKind.AzureSqlManagedInstance,
            SupportsDmDbStatsProperties = major >= 12, // SQL Server 2014+
            SupportsQueryStore = major >= 13 // SQL Server 2016+
        };
    }

    private static int ParseMajor(string? productVersion)
    {
        if (string.IsNullOrWhiteSpace(productVersion)) return 0;
        var parts = productVersion.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return int.TryParse(parts.FirstOrDefault(), out var major) ? major : 0;
    }

    private static bool IsContainerEnvironment(Dictionary<string, object?> row)
    {
        var machineName = row.GetValueOrDefault("machine_name") as string ?? string.Empty;
        if (IsHex(machineName, 12))
        {
            return true;
        }

        var distribution = row.GetValueOrDefault("host_distribution") as string;
        if (!string.IsNullOrWhiteSpace(distribution) && distribution.Contains("docker", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsHex(string value, int expectedLength)
    {
        if (value.Length != expectedLength) return false;
        foreach (var ch in value)
        {
            var isHex = (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F');
            if (!isHex) return false;
        }
        return true;
    }

    private static int ConvertToInt(object? value)
    {
        if (value is null) return 0;
        if (value is int i) return i;
        return Convert.ToInt32(value);
    }
}
