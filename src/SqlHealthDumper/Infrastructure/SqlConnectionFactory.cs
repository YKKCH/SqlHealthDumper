using Microsoft.Data.SqlClient;
using System.Text;
using SqlHealthDumper.Options;

namespace SqlHealthDumper.Infrastructure;

/// <summary>
/// AppConfig から安全な接続文字列を構築し、セッション設定を適用した接続を提供するファクトリ。
/// </summary>
public sealed class SqlConnectionFactory
{
    /// <summary>
    /// 設定をもとに接続を生成し、即座に Open 済みの状態で返す。
    /// </summary>
    public SqlConnection CreateOpenConnection(AppConfig config, string? database = null)
    {
        var builder = BuildConnectionString(config.Connection, database);
        var connection = new SqlConnection(builder.ConnectionString);
        connection.Open();
        ApplySessionSettings(connection, config.Execution);
        return connection;
    }

    // CLI 指定と既定値を組み合わせて接続文字列を構築する。
    private static SqlConnectionStringBuilder BuildConnectionString(ConnectionOptions options, string? database)
    {
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            var builder = new SqlConnectionStringBuilder(options.ConnectionString);
            if (!string.IsNullOrWhiteSpace(database))
            {
                builder.InitialCatalog = database;
            }
            return builder;
        }

        if (string.IsNullOrWhiteSpace(options.Server))
        {
            throw new InvalidOperationException("サーバー名または接続文字列が指定されていません。");
        }

        var sb = new SqlConnectionStringBuilder
        {
            DataSource = options.Server,
            InitialCatalog = database ?? "master",
            ApplicationName = "SQL Insight Snapshot",
            Encrypt = options.Encrypt,
            TrustServerCertificate = options.TrustServerCertificate,
        };

        if (options.Authentication == AuthenticationMode.Sql)
        {
            sb.UserID = options.UserName ?? string.Empty;
            sb.Password = options.Password ?? string.Empty;
            sb.IntegratedSecurity = false;
        }
        else
        {
            sb.IntegratedSecurity = true;
        }

        if (!string.IsNullOrWhiteSpace(options.CertificatePath))
        {
            sb["Column Encryption Setting"] = "Enabled";
            sb["Attestation Protocol"] = "HGS";
            sb["HostNameInCertificate"] = options.CertificatePath;
        }

        return sb;
    }

    // 収集処理向けのセッション設定をまとめて適用する。
    private static void ApplySessionSettings(SqlConnection connection, ExecutionOptions execution)
    {
        var commands = new List<string>
        {
            "SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED",
            "SET DEADLOCK_PRIORITY LOW"
        };

        if (execution.LockTimeoutMilliseconds.HasValue)
        {
            commands.Add($"SET LOCK_TIMEOUT {execution.LockTimeoutMilliseconds.Value}");
        }

        var sql = string.Join(";", commands) + ";";
        using var cmd = new SqlCommand(sql, connection);
        cmd.ExecuteNonQuery();
    }
}
