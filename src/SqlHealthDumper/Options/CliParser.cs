using CommandLine;
using System.Linq;

namespace SqlHealthDumper.Options;

/// <summary>
/// CommandLineParser の結果を内部オプションにマッピングするためのヘルパー。
/// </summary>
public static class CliParser
{
    /// <summary>
    /// CLI 引数をパースし、<see cref="CliOptions"/> へ落とし込む。異常時は例外を投げる。
    /// </summary>
    public static CliOptions Parse(string[] args)
    {
        var result = Parser.Default.ParseArguments<CliArguments>(args);

        CliOptions? mapped = null;
        result
            .WithParsed(parsed =>
            {
                mapped = new CliOptions
                {
                    Server = parsed.Server,
                    ConnectionString = parsed.ConnectionString,
                    Auth = parsed.Auth,
                    User = parsed.User,
                    Password = parsed.Password,
                    Output = parsed.Output,
                    IncludeSystemDbs = parsed.IncludeSystemDbs,
                    DbFilters = parsed.DbFilters.ToList(),
                    ExcludeDbFilters = parsed.ExcludeDbFilters.ToList(),
                    QueryTimeoutSeconds = parsed.QueryTimeoutSeconds,
                    MaxParallelism = parsed.MaxParallelism,
                    TableParallelism = parsed.TableParallelism,
                    QueriesPath = parsed.QueriesPath,
                    LogLevel = parsed.LogLevel,
                    LogFile = parsed.LogFile,
                    LockTimeoutMilliseconds = parsed.LockTimeout,
                    NoTopQueries = parsed.NoTopQueries,
                    NoMissingIndex = parsed.NoMissingIndex,
                    NoTableMarkdown = parsed.NoTableMarkdown,
                    NoBackupInfo = parsed.NoBackupInfo,
                    NoStats = parsed.NoStats,
                    Mode = parsed.Mode
                };
            })
            .WithNotParsed(errors =>
            {
                var messages = errors.Select(e => e.ToString());
                throw new ArgumentException($"引数のパースに失敗しました。詳細: {string.Join("; ", messages)}");
            });

        if (mapped is null)
        {
            throw new InvalidOperationException("引数のパースに失敗しました。");
        }

        return mapped;
    }
}
