namespace SqlHealthDumper.Options;

/// <summary>
/// ログ出力の詳細度。
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// 情報レベル。最小限の進捗のみ。
    /// </summary>
    Info,

    /// <summary>
    /// 追加情報を含むデバッグレベル。
    /// </summary>
    Debug,

    /// <summary>
    /// 低レイヤーの詳細追跡。
    /// </summary>
    Trace
}

/// <summary>
/// ログ出力先とレベル設定。
/// </summary>
public sealed class LoggingOptions
{
    /// <summary>
    /// 出力するログレベルの下限。
    /// </summary>
    public LogLevel Level { get; set; } = LogLevel.Info;

    /// <summary>
    /// ログファイルパス。null の場合はファイル出力しない。
    /// </summary>
    public string? LogFilePath { get; set; }

    /// <summary>
    /// コンソール出力を有効にするか。
    /// </summary>
    public bool ConsoleEnabled { get; set; } = true;

    /// <summary>
    /// ファイル出力を有効にするか。
    /// </summary>
    public bool FileEnabled { get; set; } = true;
}
