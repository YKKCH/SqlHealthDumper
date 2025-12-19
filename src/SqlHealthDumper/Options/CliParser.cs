using CommandLine;
using System.IO;
using System.Linq;

namespace SqlHealthDumper.Options;

/// <summary>
/// CommandLineParser の結果を内部オプションにマッピングするためのヘルパー。
/// </summary>
public static class CliParser
{
    /// <summary>
    /// CLI 引数をパースし、実行すべきコマンドとオプションを返す。異常時は例外を投げる。
    /// </summary>
    public static CliParseResult Parse(string[] args)
    {
        var result = Parser.Default.ParseArguments<CliArguments, ServeArguments>(args);

        return result switch
        {
            Parsed<object> parsed when parsed.Value is CliArguments runArgs
                => CliParseResult.ForRun(MapRunArguments(runArgs)),
            Parsed<object> parsed when parsed.Value is ServeArguments serveArgs
                => CliParseResult.ForServe(MapServeArguments(serveArgs)),
            NotParsed<object> notParsed => ThrowParseError(notParsed.Errors),
            _ => throw new ArgumentException("引数のパースに失敗しました。")
        };
    }

    private static CliOptions MapRunArguments(CliArguments parsed)
    {
        return new CliOptions
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
    }

    private static ServeOptions MapServeArguments(ServeArguments parsed)
    {
        var root = NormalizeRootPath(parsed.Path);
        return new ServeOptions
        {
            RootPath = root,
            Host = string.IsNullOrWhiteSpace(parsed.Host) ? "localhost" : parsed.Host,
            Port = parsed.Port,
            OpenBrowser = parsed.OpenBrowser
        };
    }

    private static string NormalizeRootPath(string? path)
    {
        var fallback = "result";
        var input = string.IsNullOrWhiteSpace(path) ? fallback : path!;
        return Path.GetFullPath(input, Environment.CurrentDirectory);
    }

    private static CliParseResult ThrowParseError(IEnumerable<Error> errors)
    {
        var messages = errors.Select(e => e.Tag.ToString());
        throw new ArgumentException($"引数のパースに失敗しました。詳細: {string.Join("; ", messages)}");
    }
}
