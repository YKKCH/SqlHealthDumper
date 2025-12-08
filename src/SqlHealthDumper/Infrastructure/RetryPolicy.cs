namespace SqlHealthDumper.Infrastructure;

/// <summary>
/// 単純な遅延付きリトライを提供するユーティリティ。
/// </summary>
public sealed class RetryPolicy
{
    /// <summary>
    /// 最大リトライ回数。
    /// </summary>
    public int MaxRetryCount { get; set; } = 2;

    /// <summary>
    /// リトライ間の待機時間。
    /// </summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// 非永続的な失敗を想定し、指定回数まで再実行する。
    /// </summary>
    public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken = default)
    {
        var attempt = 0;
        while (true)
        {
            try
            {
                return await work();
            }
            catch when (attempt < MaxRetryCount)
            {
                attempt++;
                await Task.Delay(Delay, cancellationToken);
            }
        }
    }
}
