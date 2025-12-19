using CommandLine;

namespace SqlHealthDumper.Options;

/// <summary>
/// serve サブコマンドの CLI 入力を CommandLineParser から受け取る DTO。
/// </summary>
[Verb("serve", HelpText = "既存スナップショットをローカル Web ダッシュボードで表示")]
public sealed class ServeArguments
{
    [Option("path", HelpText = "スナップショットフォルダまたはその親ディレクトリを指定（既定: ./result）", Default = "result")]
    /// <summary>
    /// 表示対象の最上位パス。スナップショットディレクトリ自身でも良い。
    /// </summary>
    public string Path { get; set; } = "result";

    [Option("port", HelpText = "待ち受けポート番号 (既定: 5080)", Default = 5080)]
    /// <summary>
    /// Web サーバーが listen するポート。
    /// </summary>
    public int Port { get; set; } = 5080;

    [Option("host", HelpText = "待ち受けホスト (既定: localhost)", Default = "localhost")]
    /// <summary>
    /// バインドするホスト名。
    /// </summary>
    public string Host { get; set; } = "localhost";

    [Option("open-browser", HelpText = "起動時に既定ブラウザを自動で開く", Default = false)]
    /// <summary>
    /// 自動でブラウザを開くか。
    /// </summary>
    public bool OpenBrowser { get; set; }
}
