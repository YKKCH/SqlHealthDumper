namespace SqlHealthDumper.Options;

/// <summary>
/// ファイル出力全般の構成設定。
/// </summary>
public sealed class OutputOptions
{
    /// <summary>
    /// ルート出力ディレクトリ。
    /// </summary>
    public string OutputRoot { get; set; } = Path.Combine(AppContext.BaseDirectory, "output");

    /// <summary>
    /// ファイル名の最大長。OS 制限を意識して短縮するため。
    /// </summary>
    public int FileNameMaxLength { get; set; } = 50;

    /// <summary>
    /// 長文 SQL を外部ファイルへ出すか。(false の場合は Markdown に埋め込む)
    /// </summary>
    public bool LongSqlExternal { get; set; } = true;

    /// <summary>
    /// テーブルヒストグラムのサブフォルダ名。
    /// </summary>
    public string HistogramDetailFolderName { get; set; } = "Tables";

    /// <summary>
    /// タイムスタンプを UTC で出力するか。
    /// </summary>
    public bool UseUtcTimestamps { get; set; } = true;

    /// <summary>
    /// 実行ごとにサブディレクトリを作成するか。
    /// </summary>
    public bool CreateRunSubdirectory { get; set; } = true;
}
