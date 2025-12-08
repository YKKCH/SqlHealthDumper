#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SqlHealthDumper.Infrastructure;
using Xunit;

namespace SqlHealthDumper.Tests;

/// <summary>
/// <see cref="SqlExecutionHelper"/> の結果読み取り挙動を確認するテスト。
/// </summary>
public sealed class SqlExecutionHelperTests
{
    [Fact]
    /// <summary>
    /// 複数結果セットを連結して返すことを検証。
    /// </summary>
    public async Task QueryAsync_ReadsMultipleResultSets()
    {
        var rows1 = new List<Dictionary<string, object?>>
        {
            new() { { "col1", 1 } },
            new() { { "col1", 2 } }
        };
        var rows2 = new List<Dictionary<string, object?>>
        {
            new() { { "col2", "a" } }
        };

        var executor = new FakeExecutor(rows1, rows2);
        var helper = new SqlExecutionHelper(new RetryPolicy(), executor);
        using var conn = new SqlConnection(); // not used by fake executor

        var result = await helper.QueryAsync(conn, "unused", 5);

        Assert.Equal(3, result.Count);
        Assert.Equal(2, result.Count(r => r.ContainsKey("col1")));
        Assert.Equal(1, result.Count(r => r.ContainsKey("col2")));
    }

    private sealed class FakeExecutor : ISqlCommandExecutor
    {
        private readonly List<List<Dictionary<string, object?>>> _resultSets;

        public FakeExecutor(params List<Dictionary<string, object?>>[] sets)
        {
            _resultSets = sets.ToList();
        }

        public Task<DbDataReader> ExecuteReaderAsync(SqlConnection connection, string sql, int timeoutSeconds, CancellationToken cancellationToken = default)
        {
            var tables = new List<System.Data.DataTable>();
            foreach (var set in _resultSets)
            {
                var table = new System.Data.DataTable();
                if (set.Count == 0)
                {
                    tables.Add(table);
                    continue;
                }

                foreach (var key in set[0].Keys)
                {
                    table.Columns.Add(key);
                }

                foreach (var row in set)
                {
                    var values = row.Values.Select(v => v ?? DBNull.Value).ToArray();
                    table.Rows.Add(values);
                }

                tables.Add(table);
            }

            var reader = new System.Data.DataTableReader(tables.ToArray());
            return Task.FromResult<DbDataReader>(reader);
        }
    }
}
