using System;
using SqlHealthDumper.Domain;
using SqlHealthDumper.Infrastructure;
using SqlHealthDumper.Options;
using System.Security.Cryptography;
using System.Text;

namespace SqlHealthDumper.Rendering;

/// <summary>
/// 収集したスナップショットを Markdown/JSON ファイルに整形して出力するレンダラー。
/// </summary>
public sealed class MarkdownRenderer
{
    private readonly FileSystem _fileSystem;
    private readonly OutputOptions _outputOptions;
    private readonly Dictionary<string, string> _dbDirCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// ファイルシステムと出力設定を受け取りレンダラーを初期化する。
    /// </summary>
    public MarkdownRenderer(FileSystem fileSystem, OutputOptions outputOptions)
    {
        _fileSystem = fileSystem;
        _outputOptions = outputOptions;
    }

    /// <summary>
    /// レジュームによりスキップしたセクションの Markdown を出力する。
    /// </summary>
    public async Task WriteSectionSkippedAsync(DatabaseSnapshot database, string fileName, string title, string reason, string outputRoot, CancellationToken cancellationToken = default)
    {
        var dbDir = GetDatabaseDirectory(database.Name, outputRoot);
        var path = Path.Combine(dbDir, fileName);
        var content = $"# {title}{Environment.NewLine}取得不可: {reason}";
        await _fileSystem.WriteTextAsync(path, content, cancellationToken);
    }

    /// <summary>
    /// インスタンス概要を Markdown にレンダリングする。
    /// </summary>
    public async Task WriteInstanceAsync(InstanceSnapshot snapshot, string outputRoot, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputRoot);
        var path = Path.Combine(outputRoot, "00_instance_overview.md");
        var content = BuildInstanceMarkdown(snapshot);
        await _fileSystem.WriteTextAsync(path, content, cancellationToken);
    }

    /// <summary>
    /// データベース概要セクションを出力する。
    /// </summary>
    public async Task WriteDatabaseAsync(DatabaseSnapshot database, string outputRoot, CancellationToken cancellationToken = default)
    {
        var dbDir = GetDatabaseDirectory(database.Name, outputRoot);
        var path = Path.Combine(dbDir, "10_db_overview.md");
        var content = BuildDatabaseMarkdown(database);
        await _fileSystem.WriteTextAsync(path, content, cancellationToken);
    }

    /// <summary>
    /// インデックス使用状況の Markdown を出力する。
    /// </summary>
    public async Task WriteIndexInsightAsync(DatabaseSnapshot database, string outputRoot, CancellationToken cancellationToken = default)
    {
        var dbDir = GetDatabaseDirectory(database.Name, outputRoot);
        var path = Path.Combine(dbDir, "12_index_usage_and_missing.md");
        var content = BuildIndexMarkdown(database);
        await _fileSystem.WriteTextAsync(path, content, cancellationToken);
    }

    /// <summary>
    /// トップクエリ一覧を出力し、長文 SQL を別ファイルへ逃がす際のリンクも生成する。
    /// </summary>
    public async Task WriteTopQueriesAsync(DatabaseSnapshot database, string outputRoot, CancellationToken cancellationToken = default)
    {
        var dbDir = GetDatabaseDirectory(database.Name, outputRoot);
        var path = Path.Combine(dbDir, "13_top_queries.md");
        var content = await BuildTopQueriesMarkdownAsync(database, dbDir, cancellationToken);
        await _fileSystem.WriteTextAsync(path, content, cancellationToken);
    }

    /// <summary>
    /// 統計情報の Markdown を出力する。
    /// </summary>
    public async Task WriteStatsAsync(DatabaseSnapshot database, string outputRoot, CancellationToken cancellationToken = default)
    {
        var dbDir = GetDatabaseDirectory(database.Name, outputRoot);
        var path = Path.Combine(dbDir, "14_stats_and_params.md");
        var content = BuildStatsMarkdown(database);
        await _fileSystem.WriteTextAsync(path, content, cancellationToken);
    }

    /// <summary>
    /// バックアップ/メンテナンスに関する Markdown を出力する。
    /// </summary>
    public async Task WriteBackupAsync(DatabaseSnapshot database, string outputRoot, CancellationToken cancellationToken = default)
    {
        var dbDir = GetDatabaseDirectory(database.Name, outputRoot);
        var path = Path.Combine(dbDir, "20_backup_and_maintenance.md");
        var content = BuildBackupMarkdown(database);
        await _fileSystem.WriteTextAsync(path, content, cancellationToken);
    }

    /// <summary>
    /// 各テーブルの Markdown とヒストグラム JSON を出力する。
    /// </summary>
    public async Task WriteTablesAsync(DatabaseSnapshot database, string outputRoot, CancellationToken cancellationToken = default)
    {
        if (database.Tables.Count == 0) return;

        var dbDir = GetDatabaseDirectory(database.Name, outputRoot);
        var tablesDir = Path.Combine(dbDir, "Tables");
        Directory.CreateDirectory(tablesDir);

        foreach (var table in database.Tables)
        {
            var baseName = $"{table.Schema}.{table.TableName}";
            var mdPath = _fileSystem.BuildSafeFilePath(tablesDir, baseName, ".md", _outputOptions.FileNameMaxLength, "table");
            var histPath = _fileSystem.BuildSafeFilePath(tablesDir, baseName, ".hist.json", _outputOptions.FileNameMaxLength, "table");

            var histRelative = Path.GetRelativePath(dbDir, histPath).Replace("\\", "/");
            foreach (var stat in table.Stats)
            {
                if (stat.HistogramDetail.Count > 0)
                {
                    stat.HistogramDetailPath = histRelative;
                }
            }

            var content = BuildTableMarkdown(table, histRelative);
            await _fileSystem.WriteTextAsync(mdPath, content, cancellationToken);

            if (table.Stats.Any(s => s.HistogramDetail.Count > 0))
            {
                var detail = new TableHistogramDetail
                {
                    Schema = table.Schema,
                    TableName = table.TableName
                };
                foreach (var stat in table.Stats.Where(s => s.HistogramDetail.Count > 0))
                {
                    var entry = new TableHistogramStat { StatName = stat.StatName };
                    entry.Buckets.AddRange(stat.HistogramDetail);
                    detail.Stats.Add(entry);
                }
                await _fileSystem.WriteJsonAsync(histPath, detail, cancellationToken);
            }
        }
    }

    private static string BuildInstanceMarkdown(InstanceSnapshot snapshot)
    {
        var lines = new List<string>
        {
            "# Instance Overview",
            $"Edition: {snapshot.Edition ?? "unknown"}",
            $"Version: {snapshot.Version ?? "unknown"}",
            $"Environment: {snapshot.Environment}"
        };

        if (snapshot.Notes.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Notes:");
            lines.AddRange(snapshot.Notes.Select(n => $"- {n}"));
        }

        if (snapshot.ResourceSummary is not null)
        {
            lines.Add(string.Empty);
            lines.Add("## Resource Summary");
            lines.Add("| CPU | Max Workers | Physical MB | Target MB | Committed MB |");
            lines.Add("|---:|---:|---:|---:|---:|");
            lines.Add($"| {snapshot.ResourceSummary.CpuCount} | {snapshot.ResourceSummary.MaxWorkerCount} | {snapshot.ResourceSummary.PhysicalMemoryMb:0.##} | {snapshot.ResourceSummary.TargetMemoryMb:0.##} | {snapshot.ResourceSummary.CommittedMemoryMb:0.##} |");
        }

        if (snapshot.Waits.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("## Wait Stats (Top)");
            lines.Add("| Wait Type | WaitMs | SignalMs | % of Total |");
            lines.Add("|---|---:|---:|---:|");
            foreach (var wait in snapshot.Waits)
            {
                lines.Add($"| {wait.Name} | {wait.WaitMsPerSec:0.##} | {wait.SignalMsPerSec:0.##} | {wait.PercentOfTotal:0.##}% |");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildDatabaseMarkdown(DatabaseSnapshot db)
    {
        var lines = new List<string>
        {
            "# Database Overview",
            $"名前: {db.Name}",
            $"互換性レベル: {db.CompatibilityLevel}",
            $"リカバリモデル: {db.RecoveryModel ?? "不明"}",
            $"ログ再利用待ち: {db.LogReuseWaitDescription ?? "不明"}",
            $"読み取り専用: {(db.IsReadOnly ? "はい" : "いいえ")}",
            $"暗号化: {(db.IsEncrypted ? "はい" : "いいえ")}",
            $"HADR有効: {(db.IsHadrEnabled ? "はい" : "いいえ")}",
            string.Empty,
            "## サイズ情報 (MB)",
            $"データ: {db.SizeInfo.DataSizeMb:0.##}",
            $"ログ: {db.SizeInfo.LogSizeMb:0.##}",
            $"空き: {db.SizeInfo.FreeSpaceMb:0.##}"
        };

        if (db.Files.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("## ファイル情報");
            lines.Add("| 論理名 | 種別 | サイズMB | 使用MB | 空きMB | AutoGrowth | MaxSize | パス |");
            lines.Add("|---|---|---:|---:|---:|---|---|---|");
            foreach (var file in db.Files)
            {
                var free = Math.Max(0, file.SizeMb - file.UsedMb);
                lines.Add($"| {file.LogicalName} | {file.Type} | {file.SizeMb:0.##} | {file.UsedMb:0.##} | {free:0.##} | {file.GrowthDescription} | {file.MaxSizeDescription} | {file.PhysicalPath} |");
            }
        }

        if (db.Notes.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("## Notes");
            lines.AddRange(db.Notes.Select(n => $"- {n}"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildIndexMarkdown(DatabaseSnapshot db)
    {
        var lines = new List<string>
        {
            "# Index Usage",
            "| オブジェクト | インデックス | Seeks | Scans | Lookups | Updates |",
            "|---|---|---:|---:|---:|---:|"
        };

        foreach (var idx in db.IndexInsights.Usage)
        {
            lines.Add($"| {idx.ObjectName} | {idx.IndexName} | {idx.Seeks} | {idx.Scans} | {idx.Lookups} | {idx.Updates} |");
        }

        lines.Add(string.Empty);
        lines.Add("# Missing Index");
        lines.Add("| オブジェクト | 影響 | 定義 |");
        lines.Add("|---|---|---|");

        foreach (var miss in db.IndexInsights.Missing)
        {
            lines.Add($"| {miss.ObjectName} | {miss.Impact} | {miss.Definition} |");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private async Task<string> BuildTopQueriesMarkdownAsync(DatabaseSnapshot db, string dbDir, CancellationToken cancellationToken)
    {
        var lines = new List<string>
        {
            "# Top Queries",
            "| クエリID | CPU(ms) | Duration(ms) | LogicalReads | Writes | ExecCount | LastExec | SQL |",
            "|---|---:|---:|---:|---:|---:|---|---|"
        };

        foreach (var q in db.TopQueries)
        {
            var sqlDisplay = string.IsNullOrWhiteSpace(q.SqlText) ? "(no text)" : q.SqlText.Replace("\r", " ").Replace("\n", " ");
            if (sqlDisplay.Length > 80) sqlDisplay = sqlDisplay.Substring(0, 80) + "...";

            var link = await SaveLongSqlIfNeededAsync(q, dbDir, cancellationToken);
            if (!string.IsNullOrEmpty(link))
            {
                sqlDisplay = $"[全文]({link})";
            }

            lines.Add($"| {q.QueryId} | {q.CpuTimeMs:0.##} | {q.DurationMs:0.##} | {q.LogicalReads} | {q.Writes} | {q.ExecutionCount} | {q.LastExecutionTime} | {sqlDisplay} |");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildStatsMarkdown(DatabaseSnapshot db)
    {
        var lines = new List<string>
        {
            "# Stats and Params",
            "| 統計名 | 最終更新 | Rows | Sampled | Modifications | メモ |",
            "|---|---|---:|---:|---:|---|"
        };

        foreach (var s in db.Stats)
        {
            lines.Add($"| {s.StatName} | {s.LastUpdated} | {s.Rows} | {s.RowsSampled} | {s.ModificationCounter} | {BuildStatNotes(s)} |");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildBackupMarkdown(DatabaseSnapshot db)
    {
        var lines = new List<string>
        {
            "# Backup and Maintenance",
        };

        if (!db.BackupMaintenance.IsSupported)
        {
            lines.Add(db.BackupMaintenance.NotAvailableReason ?? "取得できませんでした。");
            return string.Join(Environment.NewLine, lines);
        }

        lines.AddRange(new[]
        {
            "## Backup History (Top)",
            "| 種別 | 開始 | 終了 | サイズMB | 所要時間 |",
            "|---|---|---|---:|---|"
        });

        if (db.BackupMaintenance.Backups.Count == 0)
        {
            lines.Add("| (履歴なし) | - | - | - | - |");
        }
        else
        {
            foreach (var b in db.BackupMaintenance.Backups)
            {
                lines.Add($"| {b.Type} | {b.StartTime} | {(b.Duration.HasValue ? b.StartTime + b.Duration : (DateTime?)null)} | {b.SizeMb:0.##} | {b.Duration} |");
            }
        }

        lines.Add(string.Empty);
        lines.Add("## DBCC CHECKDB 実行状況 (推定)");
        lines.Add("| ジョブ名 | 最終成功 | 最終失敗 | エラー |");
        lines.Add("|---|---|---|---|");
        if (db.BackupMaintenance.CheckDb.Count == 0)
        {
            lines.Add("| (情報なし) | - | - | - |");
        }
        else
        {
            foreach (var check in db.BackupMaintenance.CheckDb)
            {
                lines.Add($"| {check.JobName} | {check.LastSuccess} | {check.LastFailure} | {check.ErrorMessage ?? string.Empty} |");
            }
        }

        lines.Add(string.Empty);
        lines.Add("## Agent Jobs (CHECKDB等推定)");
        lines.Add("| ジョブ名 | 最終実行 | 成功 | メモ |");
        lines.Add("|---|---|---|---|");
        if (db.BackupMaintenance.AgentJobs.Count == 0)
        {
            lines.Add("| (情報なし) | - | - | - |");
        }
        else
        {
            foreach (var job in db.BackupMaintenance.AgentJobs)
            {
                lines.Add($"| {job.Name} | {job.LastRun} | {(job.LastRunSucceeded ? "成功" : "失敗")} | {job.Note ?? string.Empty} |");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildTableMarkdown(TableSnapshot table, string histogramRelativePath)
    {
        var lines = new List<string>
        {
            $"# {table.Schema}.{table.TableName}",
            $"行数: {table.RowCount:N0}",
            $"データサイズ(MB): {table.DataSizeMb:0.##}",
            $"インデックスサイズ(MB): {table.IndexSizeMb:0.##}",
            string.Empty,
            "## 列定義",
            "| 列 | 型 | NULL可 | 既定値 |",
            "|---|---|---|---|"
        };

        foreach (var col in table.Columns)
        {
            lines.Add($"| {col.Name} | {col.DataType} | {(col.IsNullable ? "YES" : "NO")} | {col.DefaultValue} |");
        }

        lines.Add(string.Empty);
        lines.Add("## インデックス");
        lines.Add("| 名前 | クラスタ | ユニーク | キー列 | INCLUDE |");
        lines.Add("|---|---|---|---|---|");
        foreach (var idx in table.Indexes)
        {
            lines.Add($"| {idx.Name} | {(idx.IsClustered ? "Yes" : "No")} | {(idx.IsUnique ? "Yes" : "No")} | {string.Join(", ", idx.KeyColumns)} | {string.Join(", ", idx.IncludeColumns)} |");
        }

        lines.Add(string.Empty);
        lines.Add("## 制約");
        lines.Add("| 名前 | 種別 |");
        lines.Add("|---|---|");
        foreach (var c in table.Constraints)
        {
            lines.Add($"| {c.Name} | {c.Type} |");
        }

        lines.Add(string.Empty);
        lines.Add("## トリガ");
        lines.Add("| 名前 | 有効 |");
        lines.Add("|---|---|");
        foreach (var tr in table.Triggers)
        {
            lines.Add($"| {tr.Name} | {(tr.IsEnabled ? "Yes" : "No")} |");
        }

        lines.Add(string.Empty);
        lines.Add("## 統計");
        lines.Add("| 統計名 | 最終更新 | Rows | Sampled | Modifications | ヒスト | メモ |");
        lines.Add("|---|---|---:|---:|---:|---|---|");
        foreach (var st in table.Stats)
        {
            var histLink = st.HistogramDetail.Count > 0 ? $"[詳細]({histogramRelativePath})" : string.Empty;
            var note = BuildStatNotes(st);
            lines.Add($"| {st.StatName} | {st.LastUpdated} | {st.Rows} | {st.RowsSampled} | {st.ModificationCounter} | {histLink} | {note} |");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private string GetDatabaseDirectory(string dbName, string outputRoot)
    {
        if (_dbDirCache.TryGetValue(dbName, out var cached))
        {
            return cached;
        }

        var dir = _fileSystem.BuildSafeDirectory(outputRoot, dbName, _outputOptions.FileNameMaxLength, "database");
        Directory.CreateDirectory(dir);
        _dbDirCache[dbName] = dir;
        return dir;
    }

    private async Task<string?> SaveLongSqlIfNeededAsync(QueryInsight query, string dbDir, CancellationToken cancellationToken)
    {
        if (!_outputOptions.LongSqlExternal) return null;
        if (string.IsNullOrWhiteSpace(query.SqlText)) return null;
        if (query.SqlText.Length < 500) return null;

        var queriesDir = Path.Combine(dbDir, "Queries");
        Directory.CreateDirectory(queriesDir);

        var id = BuildStableQueryId(query);
        var filePath = _fileSystem.BuildSafeFilePath(queriesDir, id, ".sql", _outputOptions.FileNameMaxLength, "query");
        await _fileSystem.WriteTextAsync(filePath, query.SqlText, cancellationToken);

        var relative = Path.GetRelativePath(dbDir, filePath).Replace("\\", "/");
        query.SqlTextPath = relative;
        return relative;
    }

    private static string BuildStableQueryId(QueryInsight query)
    {
        var key = query.QueryId + (query.SqlText ?? string.Empty);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash[..8]); // 8 bytes = 16 hex chars
    }

    private static string BuildStatNotes(StatsInsight stat)
    {
        var notes = new List<string>();
        if (stat.RowsEstimated)
        {
            notes.Add("行数をテーブル値で推定");
        }
        if (stat.HistogramUnavailable)
        {
            notes.Add("ヒスト未取得");
        }

        return notes.Count == 0 ? string.Empty : string.Join(", ", notes);
    }
}
