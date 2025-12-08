using System;
using System.IO;
using SqlHealthDumper.Infrastructure;
using Xunit;

namespace SqlHealthDumper.Tests;

/// <summary>
/// <see cref="FileSystem"/> のパス生成およびサニタイズ動作を検証するテスト群。
/// </summary>
public sealed class FileSystemTests
{
    [Fact]
    /// <summary>
    /// 無効文字の除去と最大長の制限が期待通り働くかを確認。
    /// </summary>
    public void SanitizeName_RemovesInvalidAndTruncates()
    {
        var fs = new FileSystem();
        var result = fs.SanitizeName(@"inva|id:name?.txt", 5);
        Assert.Equal("invai", result); // invalid chars removed, then truncated to 5
    }

    [Fact]
    /// <summary>
    /// 既存ファイルがある場合に連番付きパスを返すことを検証。
    /// </summary>
    public void BuildSafeFilePath_EnsuresUniqueWhenExists()
    {
        var fs = new FileSystem();
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var first = fs.BuildSafeFilePath(root, "name", ".txt", 50, "file");
            File.WriteAllText(first, "exists");

            var second = fs.BuildSafeFilePath(root, "name", ".txt", 50, "file");
            Assert.NotEqual(first, second);
            Assert.False(File.Exists(second)); // path is unused when returned
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    /// <summary>
    /// 既存ディレクトリがある場合に連番で衝突を避けることを検証。
    /// </summary>
    public void BuildSafeDirectory_EnsuresUniqueWhenExists()
    {
        var fs = new FileSystem();
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var existing = Path.Combine(root, "name");
        Directory.CreateDirectory(existing);

        try
        {
            var next = fs.BuildSafeDirectory(root, "name", 50, "directory");
            Assert.NotEqual(existing, next);
            Assert.False(Directory.Exists(next));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
