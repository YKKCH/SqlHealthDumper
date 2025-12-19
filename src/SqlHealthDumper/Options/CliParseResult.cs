namespace SqlHealthDumper.Options;

/// <summary>
/// CLI 引数解析の結果と実行すべきコマンド種別を表す。
/// </summary>
public sealed class CliParseResult
{
    private CliParseResult(CliCommand command, CliOptions? runOptions, ServeOptions? serveOptions)
    {
        Command = command;
        RunOptions = runOptions;
        ServeOptions = serveOptions;
    }

    /// <summary>
    /// 実行対象コマンド。
    /// </summary>
    public CliCommand Command { get; }

    /// <summary>
    /// run コマンドのオプション。
    /// </summary>
    public CliOptions? RunOptions { get; }

    /// <summary>
    /// serve コマンドのオプション。
    /// </summary>
    public ServeOptions? ServeOptions { get; }

    public static CliParseResult ForRun(CliOptions options) => new(CliCommand.Run, options, null);

    public static CliParseResult ForServe(ServeOptions options) => new(CliCommand.Serve, null, options);
}

/// <summary>
/// CLI で選択可能なコマンドの列挙。
/// </summary>
public enum CliCommand
{
    Run,
    Serve
}
