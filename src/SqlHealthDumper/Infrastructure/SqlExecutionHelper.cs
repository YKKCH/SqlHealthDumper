using System.Data.Common;
using System.Data;
using Microsoft.Data.SqlClient;
using SqlHealthDumper.Options;

namespace SqlHealthDumper.Infrastructure;

/// <summary>
/// SQL 実行のリトライ制御と結果の整形を担当し、各コレクターの実装を単純化する。
/// </summary>
public sealed class SqlExecutionHelper
{
    private readonly RetryPolicy _retryPolicy;
    private readonly ISqlCommandExecutor _executor;

    /// <summary>
    /// リトライポリシーと実行戦略を受け取り、テスト容易性を確保する。
    /// </summary>
    public SqlExecutionHelper(RetryPolicy retryPolicy, ISqlCommandExecutor? executor = null)
    {
        _retryPolicy = retryPolicy;
        _executor = executor ?? new DefaultSqlCommandExecutor();
    }

    /// <summary>
    /// クエリ結果を列名と値の辞書に展開しつつ、指定回数までリトライする。
    /// </summary>
    public async Task<List<Dictionary<string, object?>>> QueryAsync(SqlConnection connection, string sql, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        return await _retryPolicy.ExecuteWithRetryAsync(async () =>
        {
            using var reader = await _executor.ExecuteReaderAsync(connection, sql, timeoutSeconds, cancellationToken);
            var results = new List<Dictionary<string, object?>>();

            do
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var row = new Dictionary<string, object?>(reader.FieldCount, StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    }
                    results.Add(row);
                }
            } while (await reader.NextResultAsync(cancellationToken));

            return results;
        }, cancellationToken);
    }
}

/// <summary>
/// 実際の SqlCommand 実行を抽象化し、テストや差し替えを容易にするためのインターフェース。
/// </summary>
public interface ISqlCommandExecutor
{
    /// <summary>
    /// 指定クエリを非同期で実行し、読み取り専用のリーダーを返す。
    /// </summary>
    Task<DbDataReader> ExecuteReaderAsync(SqlConnection connection, string sql, int timeoutSeconds, CancellationToken cancellationToken = default);
}

internal sealed class DefaultSqlCommandExecutor : ISqlCommandExecutor
{
    public async Task<DbDataReader> ExecuteReaderAsync(SqlConnection connection, string sql, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        // SqlDataReader は実行した SqlCommand に依存するため、呼び出し元がリーダーを読み終える
        // 前にコマンドを破棄してはいけない。using にせずリーダーに所有権を委ねる。
        var cmd = new SqlCommand(sql, connection)
        {
            CommandType = CommandType.Text,
            CommandTimeout = timeoutSeconds
        };

        try
        {
            return await cmd.ExecuteReaderAsync(cancellationToken);
        }
        catch
        {
            cmd.Dispose();
            throw;
        }
    }
}
