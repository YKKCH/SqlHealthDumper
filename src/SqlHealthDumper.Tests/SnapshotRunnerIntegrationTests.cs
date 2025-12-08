#nullable enable
using Microsoft.Data.SqlClient;
using SqlHealthDumper.Options;
using SqlHealthDumper.Orchestration;
using Xunit;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Linq;

namespace SqlHealthDumper.Tests;

/// <summary>
/// 実際の SQL Server コンテナを用いてスナップショット一連の動作を検証する統合テスト。
/// </summary>
public sealed class SnapshotRunnerIntegrationTests : IAsyncLifetime
{
    private const string DatabaseName = "HealthDemo";
    private const string SaPassword = "YourStrong!Passw0rd";

    private readonly string _containerName = $"sqlhealthdumper-it-{Guid.NewGuid():N}";
    private readonly string _outputRoot;
    private string? _connectionString;
    private int _hostPort;
    private bool _skip;

    /// <summary>
    /// 一時ディレクトリをセットアップする。
    /// </summary>
    public SnapshotRunnerIntegrationTests()
    {
        _outputRoot = Path.Combine(Path.GetTempPath(), "SqlHealthDumperIT", Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// Docker コンテナを起動し SQL Server を準備する。必要ならテストをスキップする。
    /// </summary>
    public async Task InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("SKIP_SQL_INTEGRATION") == "1")
        {
            _skip = true;
            return;
        }

        try
        {
            EnsureDockerAvailable();
            _hostPort = GetFreeTcpPort();
            StartContainer(_hostPort);
            _connectionString = BuildConnectionString(_hostPort);
            await WaitForSqlReadyAsync(_connectionString);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"IT InitializeAsync error: {ex.Message}");
            _skip = true;
        }
    }

    /// <summary>
    /// コンテナの停止と簡易クリーンアップを行う。
    /// </summary>
    public Task DisposeAsync()
    {
        if (!_skip)
        {
            StopContainer();
        }

        //TryDeleteDirectory(_outputRoot);
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    /// <summary>
    /// Collector から Renderer までのフルパイプラインが例外なく実行できることを検証する。
    /// </summary>
    public async Task Collector_to_Renderer_smoke_runs_end_to_end(bool enableQueryStore)
    {
        Console.WriteLine($"IT output root: {_outputRoot}");
        Assert.False(_skip, "Integration test skipped");
        if (_skip)
        {
            return;
        }

        await SeedDatabaseAsync(_connectionString!, enableQueryStore);

        var runner = new SnapshotRunner();
        var scenarioRoot = Path.Combine(_outputRoot, enableQueryStore ? "qs-on" : "qs-off");
        var config = BuildConfig(_connectionString!, scenarioRoot);

        await runner.RunAsync(config);

        AssertOutputArtifacts(config.Output.OutputRoot, DatabaseName);
    }

    private static void EnsureDockerAvailable()
    {
        var check = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = "info",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(check) ?? throw new InvalidOperationException("docker command not found.");
        if (!process.WaitForExit(10000) || process.ExitCode != 0)
        {
            throw new InvalidOperationException("Docker is not available for integration test.");
        }
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private void StartContainer(int port)
    {
        var args = $"run -d --rm --name {_containerName} -e ACCEPT_EULA=Y -e MSSQL_PID=Developer -e SA_PASSWORD={SaPassword} -p {port}:1433 mcr.microsoft.com/mssql/server:2022-latest";
        RunDocker(args, 300000, "Failed to start SQL Server container");
    }

    private void StopContainer()
    {
        RunDocker($"rm -f {_containerName}", 20000, "Failed to stop SQL Server container", ignoreFailures: true);
    }

    private static void RunDocker(string arguments, int timeoutMs, string errorMessage, bool ignoreFailures = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            if (!ignoreFailures) throw new InvalidOperationException(errorMessage);
            return;
        }

        if (!process.WaitForExit(timeoutMs))
        {
            if (!ignoreFailures) throw new InvalidOperationException($"{errorMessage}: timeout");
            return;
        }

        if (process.ExitCode != 0 && !ignoreFailures)
        {
            var stderr = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"{errorMessage}: {stderr}");
        }
    }

    private static string BuildConnectionString(int port)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"localhost,{port}",
            UserID = "sa",
            Password = SaPassword,
            TrustServerCertificate = true,
            Encrypt = false
        };
        return builder.ConnectionString;
    }

    private static AppConfig BuildConfig(string connectionString, string outputRoot)
    {
        return new AppConfig
        {
            Connection = new ConnectionOptions
            {
                ConnectionString = connectionString,
                Authentication = AuthenticationMode.Sql,
                Encrypt = false,
                TrustServerCertificate = true
            },
            Output = new OutputOptions
            {
                OutputRoot = outputRoot,
                FileNameMaxLength = 80,
                LongSqlExternal = true,
                UseUtcTimestamps = true
            },
            Execution = new ExecutionOptions
            {
                QueryTimeoutSeconds = 60,
                MaxParallelism = 2,
                TableMaxParallelism = 2,
                IncludeSystemDatabases = false,
                IncludeDatabases = { DatabaseName },
                Sections = new SectionToggles
                {
                    CollectBackupInfo = true,
                    CollectMissingIndex = true,
                    CollectTableMarkdown = true,
                    CollectStats = true,
                    CollectTopQueries = true
                }
            },
            Logging = new LoggingOptions
            {
                Level = LogLevel.Info,
                ConsoleEnabled = false,
                FileEnabled = true,
                LogFilePath = Path.Combine(outputRoot, "integration.log")
            },
            Resume = new ResumeOptions
            {
                SnapshotStatePath = Path.Combine(outputRoot, "snapshot_state.json"),
                FailuresPath = Path.Combine(outputRoot, "failures.json")
            }
        };
    }

    private static void AssertOutputArtifacts(string outputRoot, string databaseName)
    {
        Assert.True(File.Exists(Path.Combine(outputRoot, "00_instance_overview.md")));

        var dbDir = Directory.GetDirectories(outputRoot)
            .FirstOrDefault(d => Path.GetFileName(d).StartsWith(databaseName, StringComparison.OrdinalIgnoreCase));
        Assert.False(string.IsNullOrWhiteSpace(dbDir));

        Assert.True(File.Exists(Path.Combine(dbDir!, "10_db_overview.md")));
        Assert.True(File.Exists(Path.Combine(dbDir!, "12_index_usage_and_missing.md")));
        Assert.True(File.Exists(Path.Combine(dbDir!, "13_top_queries.md")));
        Assert.True(File.Exists(Path.Combine(dbDir!, "14_stats_and_params.md")));
        Assert.True(File.Exists(Path.Combine(dbDir!, "20_backup_and_maintenance.md")));

        var tablesDir = Path.Combine(dbDir!, "Tables");
        Assert.True(Directory.Exists(tablesDir));

        var tableMd = Directory.GetFiles(tablesDir, "*.md")
            .FirstOrDefault(f => Path.GetFileName(f).Contains("sales.orders", StringComparison.OrdinalIgnoreCase));
        Assert.False(string.IsNullOrWhiteSpace(tableMd));

        var tableContent = File.ReadAllText(tableMd!);
        Assert.Contains("sales.orders", tableContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("行数", tableContent);

        var histogram = Directory.GetFiles(tablesDir, "*.hist.json").FirstOrDefault();
        Assert.False(string.IsNullOrWhiteSpace(histogram));
        Assert.Contains("Stats", File.ReadAllText(histogram!));

        var topQueries = File.ReadAllText(Path.Combine(dbDir!, "13_top_queries.md"));
        Assert.Contains("Top Queries", topQueries);
    }

    private static async Task WaitForSqlReadyAsync(string connectionString, int retries = 90)
    {
        for (var i = 0; i < retries; i++)
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                return;
            }
            catch
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }

        throw new InvalidOperationException("SQL Server did not become ready in time.");
    }

    private static async Task SeedDatabaseAsync(string connectionString, bool enableQueryStore)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        connection.ChangeDatabase("master");

        var masterCommands = new[]
        {
            $"IF DB_ID('{DatabaseName}') IS NOT NULL BEGIN ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{DatabaseName}]; END;",
            $"CREATE DATABASE [{DatabaseName}];",
            $"ALTER DATABASE [{DatabaseName}] SET RECOVERY SIMPLE;"
        }.ToList();

        if (enableQueryStore)
        {
            masterCommands.Add($"ALTER DATABASE [{DatabaseName}] SET QUERY_STORE = ON;");
            masterCommands.Add($"ALTER DATABASE [{DatabaseName}] SET QUERY_STORE (OPERATION_MODE = READ_WRITE, QUERY_CAPTURE_MODE = ALL);");
        }
        else
        {
            masterCommands.Add($"ALTER DATABASE [{DatabaseName}] SET QUERY_STORE = OFF;");
        }

        foreach (var sql in masterCommands)
        {
            await ExecuteNonQueryAsync(connection, sql);
        }

        connection.ChangeDatabase(DatabaseName);

        var databaseCommands = new[]
        {
            "CREATE SCHEMA sales;",
            "CREATE TABLE sales.Orders (OrderId INT IDENTITY PRIMARY KEY, CustomerId INT NOT NULL, OrderDate DATETIME2 NOT NULL, TotalAmount DECIMAL(18,2) NOT NULL, Note NVARCHAR(200) NULL);",
            "INSERT INTO sales.Orders (CustomerId, OrderDate, TotalAmount, Note) SELECT TOP (200) ABS(CHECKSUM(NEWID())) % 20 + 1, DATEADD(day, -ABS(CHECKSUM(NEWID())) % 30, SYSUTCDATETIME()), ABS(CHECKSUM(NEWID())) % 5000 / 10.0, CASE WHEN (ROW_NUMBER() OVER (ORDER BY (SELECT 1)) % 5)=0 THEN 'High value' ELSE NULL END FROM sys.all_objects;",
            "CREATE NONCLUSTERED INDEX IX_Orders_Customer ON sales.Orders(CustomerId) INCLUDE (OrderDate, TotalAmount);",
            "CREATE TABLE sales.Payments (PaymentId INT IDENTITY PRIMARY KEY, CustomerId INT NOT NULL, Amount DECIMAL(18,2) NOT NULL, PaidOn DATETIME2 NOT NULL);",
            "INSERT INTO sales.Payments (CustomerId, Amount, PaidOn) SELECT TOP (80) ABS(CHECKSUM(NEWID())) % 20 + 1, ABS(CHECKSUM(NEWID())) % 2000 / 10.0, DATEADD(day, -ABS(CHECKSUM(NEWID())) % 15, SYSUTCDATETIME()) FROM sys.all_objects;",
            "CREATE STATISTICS stats_totalamount ON sales.Orders(TotalAmount) WITH FULLSCAN;"
        };

        foreach (var sql in databaseCommands)
        {
            await ExecuteNonQueryAsync(connection, sql);
        }

        for (var i = 0; i < 5; i++)
        {
            await ExecuteNonQueryAsync(connection, $"SELECT SUM(TotalAmount) FROM sales.Orders WHERE CustomerId = {i + 1};");
            await ExecuteNonQueryAsync(connection, $"SELECT * FROM sales.Orders WHERE TotalAmount > {400 + i * 10};");
        }

        if (enableQueryStore)
        {
            await ExecuteNonQueryAsync(connection, "EXEC sys.sp_query_store_flush_db;");
        }

        await ExecuteNonQueryAsync(connection, $"BACKUP DATABASE [{DatabaseName}] TO DISK = '/var/opt/mssql/data/{DatabaseName}.bak' WITH INIT, COMPRESSION;");
    }

    private static async Task ExecuteNonQueryAsync(SqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 120;
        await command.ExecuteNonQueryAsync();
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
            // ignore cleanup failures
        }
    }
}
