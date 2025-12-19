using Markdig;
using System.IO;
using System.Linq;
using System.Text;

namespace SqlHealthDumper.Dashboard;

/// <summary>
/// スナップショット配下のファイル列挙とコンテンツ取得を担うサービス。
/// </summary>
public sealed class SnapshotFileService
{
    private const int MaxPreviewBytes = 2 * 1024 * 1024; // 2MB
    private readonly SnapshotCatalog _catalog;
    private readonly MarkdownPipeline _markdown;

    public SnapshotFileService(SnapshotCatalog catalog, MarkdownPipeline markdown)
    {
        _catalog = catalog;
        _markdown = markdown;
    }

    public IReadOnlyList<SnapshotFileDescriptor>? ListFiles(string snapshotId)
    {
        if (!_catalog.TryGetSnapshot(snapshotId, out var snapshot))
        {
            return null;
        }

        try
        {
            var files = Directory.EnumerateFiles(snapshot.FullPath, "*", SearchOption.AllDirectories)
                .Select(path =>
                {
                    var info = new FileInfo(path);
                    return new SnapshotFileDescriptor
                    {
                        Path = NormalizeRelativePath(snapshot.FullPath, path),
                        SizeBytes = info.Length,
                        LastModifiedUtc = info.LastWriteTimeUtc
                    };
                })
                .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return files;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<SnapshotFileDescriptor>();
        }
    }

    public async Task<SnapshotFileContent?> ReadFileAsync(string snapshotId, string relativePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        if (!_catalog.TryGetSnapshot(snapshotId, out var snapshot))
        {
            return null;
        }

        var normalized = relativePath.Replace('\\', '/');
        var fullPath = GetSafeFullPath(snapshot.FullPath, normalized);
        if (fullPath is null || !File.Exists(fullPath))
        {
            return null;
        }

        var info = new FileInfo(fullPath);
        var (text, truncated) = await ReadFileTextAsync(info, cancellationToken);
        var extension = Path.GetExtension(fullPath);
        var mediaType = ResolveMediaType(extension);

        string? html = null;
        if (string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase))
        {
            html = Markdown.ToHtml(text, _markdown);
        }

        return new SnapshotFileContent
        {
            Path = NormalizeRelativePath(snapshot.FullPath, fullPath),
            MediaType = mediaType,
            Text = text,
            Html = html,
            IsTruncated = truncated,
            SizeBytes = info.Length,
            LastModifiedUtc = info.LastWriteTimeUtc
        };
    }

    private static string NormalizeRelativePath(string root, string fullPath)
    {
        var relative = Path.GetRelativePath(root, fullPath);
        return relative.Replace("\\", "/");
    }

    private static string? GetSafeFullPath(string root, string relative)
    {
        try
        {
            var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var combined = Path.GetFullPath(Path.Combine(normalizedRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!combined.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            return combined;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static async Task<(string Text, bool Truncated)> ReadFileTextAsync(FileInfo fileInfo, CancellationToken cancellationToken)
    {
        if (fileInfo.Length <= MaxPreviewBytes)
        {
            var content = await File.ReadAllTextAsync(fileInfo.FullName, Encoding.UTF8, cancellationToken);
            return (content, false);
        }

        await using var stream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[MaxPreviewBytes];
        var read = await reader.ReadBlockAsync(buffer, 0, buffer.Length);
        var text = new string(buffer, 0, read);
        var notice = $"{Environment.NewLine}{Environment.NewLine}---{Environment.NewLine}Content truncated at {MaxPreviewBytes / 1024 / 1024.0:0.#} MB for preview.";
        return (text + notice, true);
    }

    private static string ResolveMediaType(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return "text/plain";
        }

        return extension.ToLowerInvariant() switch
        {
            ".md" => "text/markdown",
            ".json" => "application/json",
            ".log" => "text/plain",
            ".txt" => "text/plain",
            _ => "text/plain"
        };
    }
}
