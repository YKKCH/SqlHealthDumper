namespace SqlHealthDumper.Options;

/// <summary>
/// CLI 引数を標準化前の形で保持し <see cref="AppConfig"/> へと正規化するための中間モデル。
/// </summary>
public sealed class CliOptions
{
    /// <summary>
    /// 接続先サーバー名。接続文字列と排他。
    /// </summary>
    public string? Server { get; set; }

    /// <summary>
    /// 完全接続文字列。細かい調整が必要な場合に使用。
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// 認証方式 (Windows/Sql/Aad など)。
    /// </summary>
    public string? Auth { get; set; }

    /// <summary>
    /// SQL 認証ユーザー名。
    /// </summary>
    public string? User { get; set; }

    /// <summary>
    /// SQL 認証パスワード。
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// 出力先ディレクトリの指定。
    /// </summary>
    public string? Output { get; set; }

    /// <summary>
    /// システム DB を収集対象に含めるフラグ。
    /// </summary>
    public bool IncludeSystemDbs { get; set; }

    /// <summary>
    /// 対象 DB を限定するフィルター群 (ワイルドカード対応)。
    /// </summary>
    public List<string> DbFilters { get; set; } = new();

    /// <summary>
    /// 除外する DB のフィルター群。
    /// </summary>
    public List<string> ExcludeDbFilters { get; set; } = new();

    /// <summary>
    /// クエリ実行タイムアウト (秒)。
    /// </summary>
    public int? QueryTimeoutSeconds { get; set; }

    /// <summary>
    /// コレクター全体の最大並列数。
    /// </summary>
    public int? MaxParallelism { get; set; }

    /// <summary>
    /// テーブル関連処理の並列数上限。
    /// </summary>
    public int? TableParallelism { get; set; }

    /// <summary>
    /// クエリ定義を格納するディレクトリ。
    /// </summary>
    public string? QueriesPath { get; set; }

    /// <summary>
    /// ログレベル文字列。
    /// </summary>
    public string? LogLevel { get; set; }

    /// <summary>
    /// ログ出力ファイルパス。
    /// </summary>
    public string? LogFile { get; set; }

    /// <summary>
    /// クエリ実行時のロックタイムアウト (ミリ秒)。
    /// </summary>
    public int? LockTimeoutMilliseconds { get; set; }

    /// <summary>
    /// 上位クエリ収集をスキップするか。
    /// </summary>
    public bool NoTopQueries { get; set; }

    /// <summary>
    /// 欠損インデックス分析をスキップするか。
    /// </summary>
    public bool NoMissingIndex { get; set; }

    /// <summary>
    /// テーブル Markdown の生成を抑制するか。
    /// </summary>
    public bool NoTableMarkdown { get; set; }

    /// <summary>
    /// バックアップ情報の収集を無効化するか。
    /// </summary>
    public bool NoBackupInfo { get; set; }

    /// <summary>
    /// 統計情報収集を無効化するか。
    /// </summary>
    public bool NoStats { get; set; }

    /// <summary>
    /// ツールのモード (snapshot/resume 等) を指定する文字列。
    /// </summary>
    public string? Mode { get; set; }

    /// <summary>
    /// レジューム機能を強制的に無効化するか。
    /// </summary>
    public bool NoResume { get; set; }

    /// <summary>
    /// システムオブジェクト (sys schema 等) を含めるか。
    /// </summary>
    public bool IncludeSystemObjects { get; set; }
}
