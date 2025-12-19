using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.IO;

namespace SqlHealthDumper.Dashboard;

/// <summary>
/// ファイルシステムからスナップショットディレクトリを探索し、UI 向けにキャッシュする。
/// </summary>
public sealed class SnapshotCatalog
{
    private readonly string _rootPath;
    private readonly object _gate = new();
    private readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(5);
    private DateTime _lastScanUtc = DateTime.MinValue;
    private List<SnapshotInfo> _snapshots = new();
    private ConcurrentDictionary<string, SnapshotInfo> _snapshotMap = new(StringComparer.OrdinalIgnoreCase);

    public SnapshotCatalog(string rootPath)
    {
        _rootPath = Path.GetFullPath(rootPath);
    }

    /// <summary>
    /// 現在検出済みのスナップショット一覧を返す。必要に応じて再スキャンする。
    /// </summary>
    public IReadOnlyList<SnapshotInfo> GetSnapshots(bool forceRefresh = false)
    {
        lock (_gate)
        {
            if (forceRefresh || CacheExpired())
            {
                Scan();
            }
            return _snapshots;
        }
    }

    /// <summary>
    /// ID からスナップショット情報を取り出す。必要に応じて再スキャンする。
    /// </summary>
    public bool TryGetSnapshot(string id, out SnapshotInfo info)
    {
        lock (_gate)
        {
            if (CacheExpired())
            {
                Scan();
            }

            return _snapshotMap.TryGetValue(id, out info!);
        }
    }

    private bool CacheExpired() => DateTime.UtcNow - _lastScanUtc > _cacheDuration;

    private void Scan()
    {
        var results = new List<SnapshotInfo>();
        var map = new ConcurrentDictionary<string, SnapshotInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in EnumerateSnapshotDirectories())
        {
            var info = CreateInfo(path);
            results.Add(info);
            map[info.Id] = info;
        }

        _snapshots = results
            .OrderByDescending(s => s.LastModifiedUtc)
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _snapshotMap = map;
        _lastScanUtc = DateTime.UtcNow;
    }

    private IEnumerable<string> EnumerateSnapshotDirectories()
    {
        if (!Directory.Exists(_rootPath))
        {
            yield break;
        }

        if (IsSnapshotDirectory(_rootPath))
        {
            yield return _rootPath;
        }

        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(_rootPath, "*", SearchOption.AllDirectories);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var dir in directories)
        {
            if (IsSnapshotDirectory(dir))
            {
                yield return dir;
            }
        }
    }

    private static bool IsSnapshotDirectory(string path)
    {
        return File.Exists(Path.Combine(path, "00_instance_overview.md"));
    }

    private SnapshotInfo CreateInfo(string fullPath)
    {
        var directoryInfo = new DirectoryInfo(fullPath);
        var lastWriteUtc = directoryInfo.LastWriteTimeUtc;
        var (fileCount, totalBytes) = CalculateSizeSafe(fullPath);

        var relative = TryGetRelative(fullPath);
        return new SnapshotInfo
        {
            Id = CreateStableId(fullPath),
            Name = directoryInfo.Name,
            FullPath = fullPath,
            RelativePath = relative,
            LastModifiedUtc = lastWriteUtc,
            FileCount = fileCount,
            TotalSizeBytes = totalBytes
        };
    }

    private string TryGetRelative(string path)
    {
        try
        {
            var relative = Path.GetRelativePath(_rootPath, path);
            return relative.Replace("\\", "/");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Path.GetFileName(path);
        }
    }

    private static string CreateStableId(string fullPath)
    {
        var normalized = Path.GetFullPath(fullPath);
        var bytes = Encoding.UTF8.GetBytes(normalized);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static (long FileCount, long SizeBytes) CalculateSizeSafe(string path)
    {
        try
        {
            long fileCount = 0;
            long size = 0;
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var info = new FileInfo(file);
                    fileCount++;
                    size += info.Length;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // ignore IO errors for individual files
                }
            }
            return (fileCount, size);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (0, 0);
        }
    }
}
