using System.Text.Json;
using System.Text.RegularExpressions;

namespace SqlHealthDumper.Infrastructure;

/// <summary>
/// OS ごとのファイルシステム制約を意識せずに安全なパス生成とファイル入出力を行うためのヘルパー。
/// </summary>
public sealed class FileSystem
{
    private static readonly Regex InvalidChars = new(@"[\\\/:\*\?""<>\|]", RegexOptions.Compiled);

    /// <summary>
    /// ファイルシステムで使用できない文字を除去し、最大長を超えないように正規化する。
    /// </summary>
    public string SanitizeName(string name, int maxLength)
    {
        var cleaned = InvalidChars.Replace(name, string.Empty);
        if (cleaned.Length > maxLength)
        {
            cleaned = cleaned.Substring(0, maxLength);
        }

        return cleaned;
    }

    /// <summary>
    /// 同名ファイルとの衝突を避けつつ、指定条件に収まる安全なファイルパスを構築する。
    /// </summary>
    public string BuildSafeFilePath(string directory, string baseName, string extension, int maxLength, string fallbackName = "file")
    {
        Directory.CreateDirectory(directory);
        var safe = SanitizeName(baseName, maxLength);
        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = fallbackName;
        }

        var normalizedExt = string.IsNullOrWhiteSpace(extension)
            ? string.Empty
            : (extension.StartsWith(".") ? extension : "." + extension);

        var path = Path.Combine(directory, $"{safe}{normalizedExt}");
        return EnsureUniquePath(path);
    }

    /// <summary>
    /// 既存ディレクトリとの競合を避けながら安全なディレクトリ名を生成する。
    /// </summary>
    public string BuildSafeDirectory(string root, string name, int maxLength, string fallbackName = "directory")
    {
        var safe = SanitizeName(name, maxLength);
        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = fallbackName;
        }

        var basePath = Path.Combine(root, safe);
        return Directory.Exists(basePath) ? EnsureUniqueDirectory(basePath) : basePath;
    }

    /// <summary>
    /// 指定ディレクトリが既に存在する場合は連番を付与して衝突を回避する。
    /// </summary>
    public string EnsureUniqueDirectory(string basePath)
    {
        if (!Directory.Exists(basePath))
        {
            return basePath;
        }

        var directory = Path.GetDirectoryName(basePath) ?? string.Empty;
        var name = Path.GetFileName(basePath);
        var counter = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(directory, $"{name}_{counter}");
            counter++;
        } while (Directory.Exists(candidate));

        return candidate;
    }

    /// <summary>
    /// 既存ファイルとの重複を避けるために連番を付与したパスを返す。
    /// </summary>
    public string EnsureUniquePath(string basePath)
    {
        if (!File.Exists(basePath))
        {
            return basePath;
        }

        var directory = Path.GetDirectoryName(basePath) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(basePath);
        var extension = Path.GetExtension(basePath);

        var counter = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(directory, $"{fileName}_{counter}{extension}");
            counter++;
        } while (File.Exists(candidate));

        return candidate;
    }

    /// <summary>
    /// 親ディレクトリがなければ作成したうえでテキストを書き出す。
    /// </summary>
    public async Task WriteTextAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        await File.WriteAllTextAsync(path, content, cancellationToken);
    }

    /// <summary>
    /// JSON を整形済みで保存し、後続の diff やレビューをしやすくする。
    /// </summary>
    public async Task WriteJsonAsync<T>(string path, T payload, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    /// <summary>
    /// ファイルが存在しない場合は null を返し、オプショナルな設定ファイルを扱いやすくする。
    /// </summary>
    public async Task<T?> ReadJsonAsync<T>(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: cancellationToken);
    }
}
