using System.IO;

namespace SqlHealthDumper.Options;

/// <summary>
/// CLI 入力をもとに <see cref="AppConfig"/> を構築・正規化するためのファクトリ。
/// </summary>
public static class AppConfigFactory
{
    /// <summary>
    /// CLI オプションを評価し、実行に必要な既定値や派生値を含む設定を構築する。
    /// </summary>
    public static AppConfig FromCli(CliOptions cli)
    {
        var config = AppConfig.CreateDefault();

        config.Execution.Mode = ParseMode(cli.Mode);
        ApplyModeDefaults(config, cli);

        config.Connection.Server = cli.Server ?? config.Connection.Server;
        config.Connection.ConnectionString = cli.ConnectionString ?? config.Connection.ConnectionString;
        if (cli.Auth is not null && cli.Auth.Equals("sql", StringComparison.OrdinalIgnoreCase))
        {
            config.Connection.Authentication = AuthenticationMode.Sql;
        }
        config.Connection.UserName = cli.User ?? config.Connection.UserName;
        config.Connection.Password = cli.Password ?? config.Connection.Password;

        if (!string.IsNullOrWhiteSpace(cli.Output))
        {
            config.Output.OutputRoot = cli.Output!;
        }

        OutputPathHelper.EnsureRunDirectory(config);

        config.Execution.IncludeSystemDatabases = cli.IncludeSystemDbs;
        config.Execution.IncludeSystemObjects = cli.IncludeSystemObjects;
        if (cli.DbFilters.Count > 0) config.Execution.IncludeDatabases.AddRange(cli.DbFilters);
        if (cli.ExcludeDbFilters.Count > 0) config.Execution.ExcludeDatabases.AddRange(cli.ExcludeDbFilters);
        if (cli.QueryTimeoutSeconds is { } qt) config.Execution.QueryTimeoutSeconds = qt;
        if (cli.MaxParallelism is { } mp) config.Execution.MaxParallelism = mp;
        if (cli.TableParallelism is { } tp) config.Execution.TableMaxParallelism = tp;
        if (cli.LockTimeoutMilliseconds is { } lt) config.Execution.LockTimeoutMilliseconds = lt;

        if (!string.IsNullOrWhiteSpace(cli.QueriesPath))
        {
            config.QuerySource.QueriesPathOverride = cli.QueriesPath;
        }

        if (!string.IsNullOrWhiteSpace(cli.LogLevel) &&
            Enum.TryParse<LogLevel>(cli.LogLevel, true, out var level))
        {
            config.Logging.Level = level;
        }
        if (!string.IsNullOrWhiteSpace(cli.LogFile))
        {
            config.Logging.LogFilePath = cli.LogFile;
        }

        // Normalize output paths for resume files to be under output root by default
        config.Resume.SnapshotStatePath = Path.Combine(config.Output.OutputRoot, config.Resume.SnapshotStatePath);
        config.Resume.FailuresPath = Path.Combine(config.Output.OutputRoot, config.Resume.FailuresPath);

        // Default log file path under output root if not specified
        if (string.IsNullOrWhiteSpace(config.Logging.LogFilePath))
        {
            config.Logging.LogFilePath = Path.Combine(config.Output.OutputRoot, "log.txt");
        }

        // Section toggles from CLI
        if (cli.NoTopQueries) config.Execution.Sections.CollectTopQueries = false;
        if (cli.NoMissingIndex) config.Execution.Sections.CollectMissingIndex = false;
        if (cli.NoTableMarkdown) config.Execution.Sections.CollectTableMarkdown = false;
        if (cli.NoBackupInfo) config.Execution.Sections.CollectBackupInfo = false;
        if (cli.NoStats) config.Execution.Sections.CollectStats = false;
        if (cli.NoResume) config.Resume.Enabled = false;

        return config;
    }

    // CLI 入力を元に実行モードを安全にパースする。
    private static ExecutionMode ParseMode(string? mode)
    {
        if (!string.IsNullOrWhiteSpace(mode) && Enum.TryParse<ExecutionMode>(mode, true, out var parsed))
        {
            return parsed;
        }
        return ExecutionMode.LowLoad;
    }

    // モードに応じたデフォルト値を CLI 指定に上書きされない範囲で適用する。
    private static void ApplyModeDefaults(AppConfig config, CliOptions cli)
    {
        var mode = config.Execution.Mode;
        if (cli.QueryTimeoutSeconds is null)
        {
            config.Execution.QueryTimeoutSeconds = mode switch
            {
                ExecutionMode.LowLoad => 90,
                ExecutionMode.Balanced => 60,
                ExecutionMode.Fast => 45,
                _ => config.Execution.QueryTimeoutSeconds
            };
        }

        if (cli.MaxParallelism is null)
        {
            config.Execution.MaxParallelism = mode switch
            {
                ExecutionMode.LowLoad => 1,
                ExecutionMode.Balanced => 3,
                ExecutionMode.Fast => 6,
                _ => config.Execution.MaxParallelism
            };
        }

        if (cli.TableParallelism is null)
        {
            config.Execution.TableMaxParallelism = mode switch
            {
                ExecutionMode.LowLoad => 1,
                ExecutionMode.Balanced => 2,
                ExecutionMode.Fast => 4,
                _ => config.Execution.TableMaxParallelism
            };
        }
    }
}
