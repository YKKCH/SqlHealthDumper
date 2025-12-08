using SqlHealthDumper.Domain;

namespace SqlHealthDumper.Collectors;

/// <summary>
/// コレクター実行の結果ステータス。
/// </summary>
public enum CollectorStatus
{
    /// <summary>
    /// 正常に完了。
    /// </summary>
    Success,

    /// <summary>
    /// 条件によりスキップされた。
    /// </summary>
    Skipped,

    /// <summary>
    /// 失敗した。
    /// </summary>
    Failed
}

/// <summary>
/// コレクター実行結果と付随情報のラッパー。
/// </summary>
public sealed class CollectorResult<T>
{
    /// <summary>
    /// 実行結果ステータス。
    /// </summary>
    public CollectorStatus Status { get; init; }

    /// <summary>
    /// スキップや失敗理由の説明。
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// 例外情報。必要に応じて null。
    /// </summary>
    public Exception? Error { get; init; }

    /// <summary>
    /// 収集済みペイロード。
    /// </summary>
    public T? Payload { get; init; }

    /// <summary>
    /// 成功結果を構築するショートカット。
    /// </summary>
    public static CollectorResult<T> Success(T payload) => new() { Status = CollectorStatus.Success, Payload = payload };

    /// <summary>
    /// ペイロードなしでスキップを表す。
    /// </summary>
    public static CollectorResult<T> Skipped(string reason) => new() { Status = CollectorStatus.Skipped, Reason = reason };

    /// <summary>
    /// 一部情報を確保したままスキップ扱いとする。
    /// </summary>
    public static CollectorResult<T> Skipped(string reason, T payload) => new() { Status = CollectorStatus.Skipped, Reason = reason, Payload = payload };

    /// <summary>
    /// 失敗時のメタ情報をまとめて返す。
    /// </summary>
    public static CollectorResult<T> Failed(string reason, Exception? ex = null) => new() { Status = CollectorStatus.Failed, Reason = reason, Error = ex };
}
