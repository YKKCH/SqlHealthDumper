namespace SqlHealthDumper.Options;

/// <summary>
/// CLI から構築されたアプリ全体設定の集約ルート。
/// </summary>
public sealed class AppConfig
{
    /// <summary>
    /// 接続に関する認証/ターゲット設定。
    /// </summary>
    public ConnectionOptions Connection { get; init; } = new();

    /// <summary>
    /// 各種ファイル出力の制御設定。
    /// </summary>
    public OutputOptions Output { get; init; } = new();

    /// <summary>
    /// 実行時の動作パラメーター群。
    /// </summary>
    public ExecutionOptions Execution { get; init; } = new();

    /// <summary>
    /// ログ出力やレベルの設定。
    /// </summary>
    public LoggingOptions Logging { get; init; } = new();

    /// <summary>
    /// SQL クエリの供給元に関する設定。
    /// </summary>
    public QuerySourceOptions QuerySource { get; init; } = new();

    /// <summary>
    /// 途中再開に関する設定。
    /// </summary>
    public ResumeOptions Resume { get; init; } = new();

    /// <summary>
    /// 既定値で構成された設定インスタンスを生成する。
    /// </summary>
    public static AppConfig CreateDefault() => new();
}
