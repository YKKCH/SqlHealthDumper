using SqlHealthDumper.Options;
using System.Text;

namespace SqlHealthDumper.Infrastructure;

/// <summary>
/// 実行状況を外部出力へ記録するための最小限のロガー契約。
/// </summary>
public interface ILogger
{
    /// <summary>
    /// 利用者向けの進捗や重要イベントを記録する。
    /// </summary>
    void Info(string message);
    /// <summary>
    /// トラブルシューティング用の追加情報を記録する。
    /// </summary>
    void Debug(string message);
    /// <summary>
    /// 詳細レベルの追跡情報を記録する。
    /// </summary>
    void Trace(string message);
    /// <summary>
    /// 例外情報を含めたエラーを記録する。
    /// </summary>
    void Error(string message, Exception? ex = null);
}

/// <summary>
/// コンソールとファイルへの二重出力、およびログレベル制御を行うロガー実装。
/// </summary>
public sealed class Logger : ILogger, IDisposable
{
    private readonly LoggingOptions _options;
    private readonly object _lock = new();
    private StreamWriter? _writer;

    /// <summary>
    /// 構成に応じて出力先を初期化し、必要ならログファイルを即時 open する。
    /// </summary>
    public Logger(LoggingOptions options)
    {
        _options = options;
        if (_options.FileEnabled && !string.IsNullOrWhiteSpace(_options.LogFilePath))
        {
            var dir = Path.GetDirectoryName(_options.LogFilePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }
            _writer = new StreamWriter(new FileStream(_options.LogFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                AutoFlush = true,
                NewLine = Environment.NewLine
            };
        }
    }

    /// <inheritdoc />
    public void Info(string message) => Write(LogLevel.Info, message);
    /// <inheritdoc />
    public void Debug(string message) => Write(LogLevel.Debug, message);
    /// <inheritdoc />
    public void Trace(string message) => Write(LogLevel.Trace, message);

    /// <inheritdoc />
    public void Error(string message, Exception? ex = null)
    {
        var full = ex is null ? message : $"{message}: {ex.Message}";
        WriteInternal("ERROR", full, ex);
    }

    private void Write(LogLevel level, string message)
    {
        if (level > _options.Level) return;
        WriteInternal(level.ToString().ToUpperInvariant(), message, null);
    }

    private void WriteInternal(string level, string message, Exception? ex)
    {
        var line = $"{DateTime.UtcNow:O} [{level}] {message}";
        if (_options.ConsoleEnabled)
        {
            Console.WriteLine(line);
            if (ex is not null) Console.WriteLine(ex);
        }

        if (_writer is null) return;

        lock (_lock)
        {
            _writer.WriteLine(line);
            if (ex is not null)
            {
                _writer.WriteLine(ex);
            }
        }
    }

    /// <summary>
    /// ファイルハンドルを確実に閉じ、後続プロセスとの競合を避ける。
    /// </summary>
    public void Dispose()
    {
        _writer?.Dispose();
    }
}
