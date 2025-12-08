using CommandLine;

namespace SqlHealthDumper.Options;

/// <summary>
/// CommandLineParser が CLI 入力を受け取るための DTO。
/// </summary>
[Verb("run", HelpText = "SQL Insight Snapshot を実行")]
public sealed class CliArguments
{
    [Option("server", HelpText = "SQL Server ホスト名またはインスタンス名（--connection-string といずれか必須）")]
    /// <summary>
    /// 接続先サーバー名。<c>--connection-string</c> との排他必須条件を持つ。
    /// </summary>
    public string? Server { get; set; }

    [Option("connection-string", HelpText = "接続文字列を直接指定（--server といずれか必須）")]
    /// <summary>
    /// 完全接続文字列。サーバー名指定より優先される。
    /// </summary>
    public string? ConnectionString { get; set; }

    [Option("auth", HelpText = "認証方式 windows|sql（デフォルト: windows）", Default = "windows")]
    /// <summary>
    /// 認証方式を文字列で指定する。
    /// </summary>
    public string? Auth { get; set; }

    [Option("user", HelpText = "SQL認証ユーザー名（--auth sql 時必須）")]
    /// <summary>
    /// SQL 認証用ユーザー名。
    /// </summary>
    public string? User { get; set; }

    [Option("password", HelpText = "SQL認証パスワード（--auth sql 時必須）")]
    /// <summary>
    /// SQL 認証パスワード。
    /// </summary>
    public string? Password { get; set; }

    [Option("output", HelpText = "出力ルートディレクトリ（未指定時はアプリ配下 output）")]
    /// <summary>
    /// レポート出力のルートパス。
    /// </summary>
    public string? Output { get; set; }

    [Option("include-system-dbs", HelpText = "システムDBを含める", Default = false)]
    /// <summary>
    /// システム DB を含めるかどうか。
    /// </summary>
    public bool IncludeSystemDbs { get; set; }

    [Option("db", HelpText = "対象DB（複数可)", Separator = ',')]
    /// <summary>
    /// 収集対象に含める DB フィルター。
    /// </summary>
    public IEnumerable<string> DbFilters { get; set; } = Array.Empty<string>();

    [Option("exclude-db", HelpText = "除外DB（複数可)", Separator = ',')]
    /// <summary>
    /// 除外する DB フィルター。
    /// </summary>
    public IEnumerable<string> ExcludeDbFilters { get; set; } = Array.Empty<string>();

    [Option("query-timeout", HelpText = "クエリタイムアウト秒（全セクション共通）")]
    /// <summary>
    /// 全クエリに適用するタイムアウト秒。
    /// </summary>
    public int? QueryTimeoutSeconds { get; set; }

    [Option("max-parallelism", HelpText = "DB並列度（モード依存のデフォルト）")]
    /// <summary>
    /// コレクター全体の並列上限。
    /// </summary>
    public int? MaxParallelism { get; set; }

    [Option("table-parallelism", HelpText = "テーブル関連処理の並列度（デフォルトモード依存）")]
    /// <summary>
    /// テーブル処理に特化した並列度。
    /// </summary>
    public int? TableParallelism { get; set; }

    [Option("queries-path", HelpText = "外部SQLパス（指定時は外部優先）")]
    /// <summary>
    /// カスタム SQL を格納したディレクトリ。
    /// </summary>
    public string? QueriesPath { get; set; }

    [Option("log-level", HelpText = "ログレベル info|debug|trace（デフォルト: info）")]
    /// <summary>
    /// ログレベルの明示指定。
    /// </summary>
    public string? LogLevel { get; set; }

    [Option("log-file", HelpText = "ログファイルパス（未指定時は出力ルート配下）")]
    /// <summary>
    /// ログファイルのフルパス。
    /// </summary>
    public string? LogFile { get; set; }

    [Option("lock-timeout", HelpText = "LOCK_TIMEOUT ミリ秒")]
    /// <summary>
    /// ロック待ちタイムアウト (ms)。
    /// </summary>
    public int? LockTimeout { get; set; }

    [Option("no-top-queries", HelpText = "トップクエリ収集を無効化", Default = false)]
    /// <summary>
    /// 上位クエリ収集を無効化するスイッチ。
    /// </summary>
    public bool NoTopQueries { get; set; }

    [Option("no-missing-index", HelpText = "インデックス利用/欠落収集を無効化", Default = false)]
    /// <summary>
    /// インデックス関連収集を無効化するスイッチ。
    /// </summary>
    public bool NoMissingIndex { get; set; }

    [Option("no-table-md", HelpText = "テーブルMarkdown出力を無効化", Default = false)]
    /// <summary>
    /// テーブル Markdown 出力を抑制するスイッチ。
    /// </summary>
    public bool NoTableMarkdown { get; set; }

    [Option("no-backup-info", HelpText = "バックアップ/メンテナンス収集を無効化", Default = false)]
    /// <summary>
    /// バックアップ関連収集を省略するスイッチ。
    /// </summary>
    public bool NoBackupInfo { get; set; }

    [Option("no-stats", HelpText = "統計収集を無効化", Default = false)]
    /// <summary>
    /// 統計情報収集を省略するスイッチ。
    /// </summary>
    public bool NoStats { get; set; }

    [Option("mode", HelpText = "実行モード low|balanced|fast（デフォルト: low）")]
    /// <summary>
    /// 実行モードの指定。
    /// </summary>
    public string? Mode { get; set; }
}
