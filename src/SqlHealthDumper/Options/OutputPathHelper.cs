using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Data.SqlClient;

namespace SqlHealthDumper.Options;

/// <summary>
/// 出力ディレクトリ名の生成と正規化を担う内部ヘルパー。
/// </summary>
internal static class OutputPathHelper
{
    /// <summary>
    /// 実行ごとのサブディレクトリを末尾に追加し、再生成を防ぐためにフラグを反転する。
    /// </summary>
    public static void EnsureRunDirectory(AppConfig config)
    {
        if (!config.Output.CreateRunSubdirectory)
        {
            return;
        }

        var runFolder = BuildRunFolderName(config);
        config.Output.OutputRoot = Path.Combine(config.Output.OutputRoot, runFolder);
        config.Output.CreateRunSubdirectory = false;
    }

    /// <summary>
    /// サーバー名とタイムスタンプから人間が判別しやすいフォルダ名を構築する。
    /// </summary>
    public static string BuildRunFolderName(AppConfig config)
    {
        var source = ResolveServerName(config);
        var sanitized = SanitizeName(source);
        var timestamp = (config.Output.UseUtcTimestamps ? DateTime.UtcNow : DateTime.Now)
            .ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture);
        return $"{sanitized}_{timestamp}";
    }

    private static string ResolveServerName(AppConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.Connection.Server))
        {
            return config.Connection.Server!;
        }

        if (!string.IsNullOrWhiteSpace(config.Connection.ConnectionString))
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(config.Connection.ConnectionString);
                if (!string.IsNullOrWhiteSpace(builder.DataSource))
                {
                    return builder.DataSource!;
                }
            }
            catch
            {
                // ignore parse failures
            }
        }

        return "snapshot";
    }

    private static string SanitizeName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(invalid.Contains(ch) ? '_' : ch);
        }

        var trimmed = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? "snapshot" : trimmed;
    }
}
