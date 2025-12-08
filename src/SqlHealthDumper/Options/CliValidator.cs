namespace SqlHealthDumper.Options;

/// <summary>
/// CLI 入力が実行前に満たすべき前提条件をチェックするバリデーター。
/// </summary>
public static class CliValidator
{
    /// <summary>
    /// <see cref="CliOptions"/> の妥当性を確認し、エラーメッセージの一覧を返す。
    /// </summary>
    public static List<string> Validate(CliOptions cli)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(cli.Server) && string.IsNullOrWhiteSpace(cli.ConnectionString))
        {
            errors.Add("--server または --connection-string のいずれかを指定してください。");
        }

        if (!string.IsNullOrWhiteSpace(cli.Auth) &&
            !cli.Auth.Equals("windows", StringComparison.OrdinalIgnoreCase) &&
            !cli.Auth.Equals("sql", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("--auth は windows または sql を指定してください。");
        }

        if (IsSqlAuth(cli) && string.IsNullOrWhiteSpace(cli.ConnectionString))
        {
            if (string.IsNullOrWhiteSpace(cli.User))
            {
                errors.Add("--auth sql の場合は --user を指定してください。");
            }

            if (string.IsNullOrWhiteSpace(cli.Password))
            {
                errors.Add("--auth sql の場合は --password を指定してください。");
            }
        }

        if (cli.QueryTimeoutSeconds is { } qt && qt <= 0)
        {
            errors.Add("--query-timeout は 1 以上を指定してください。");
        }

        if (cli.MaxParallelism is { } mp && mp <= 0)
        {
            errors.Add("--max-parallelism は 1 以上を指定してください。");
        }

        if (cli.TableParallelism is { } tp && tp <= 0)
        {
            errors.Add("--table-parallelism は 1 以上を指定してください。");
        }

        if (cli.LockTimeoutMilliseconds is { } lt && lt < 0)
        {
            errors.Add("--lock-timeout は 0 以上を指定してください。");
        }

        if (!string.IsNullOrWhiteSpace(cli.Mode) && !Enum.TryParse<ExecutionMode>(cli.Mode, true, out _))
        {
            errors.Add("--mode は low|balanced|fast のいずれかを指定してください。");
        }

        if (!string.IsNullOrWhiteSpace(cli.LogLevel) && !Enum.TryParse<LogLevel>(cli.LogLevel, true, out _))
        {
            errors.Add("--log-level は info|debug|trace のいずれかを指定してください。");
        }

        return errors;
    }

    /// <summary>
    /// ユーザーがすぐ参考にできるサンプルコマンドを生成する。
    /// </summary>
    public static string BuildSampleCommands()
    {
        var samples = new List<string>
        {
            "Windows 認証例: SqlHealthDumper run --server localhost --output C:\\tmp\\snapshot",
            "SQL 認証例: SqlHealthDumper run --server localhost --auth sql --user sa --password P@ssw0rd --output C:\\tmp\\snapshot"
        };

        return string.Join(Environment.NewLine, samples);
    }

    private static bool IsSqlAuth(CliOptions cli)
    {
        return cli.Auth?.Equals("sql", StringComparison.OrdinalIgnoreCase) == true;
    }
}
