namespace SqlHealthDumper.Options;

/// <summary>
/// SQL クエリを取得する際の埋め込みリソース名や外部パス設定。
/// </summary>
public sealed class QuerySourceOptions
{
    /// <summary>
    /// 埋め込みリソースに格納された SQL のルート名前空間。
    /// </summary>
    public string EmbeddedNamespace { get; set; } = "SqlHealthDumper.Queries";

    /// <summary>
    /// 外部ディレクトリによる上書きパス。指定時はそちらを優先する。
    /// </summary>
    public string? QueriesPathOverride { get; set; }
}
