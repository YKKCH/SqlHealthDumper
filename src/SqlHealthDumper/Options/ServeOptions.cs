namespace SqlHealthDumper.Options;

/// <summary>
/// serve サブコマンド実行時に必要な設定値。
/// </summary>
public sealed class ServeOptions
{
    /// <summary>
    /// スナップショット探索の起点ディレクトリ。
    /// </summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>
    /// バインドするホスト名。
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// 待ち受けポート番号。
    /// </summary>
    public int Port { get; set; } = 5080;

    /// <summary>
    /// 起動時にブラウザを自動で開くか。
    /// </summary>
    public bool OpenBrowser { get; set; }
}
