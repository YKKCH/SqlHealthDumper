using SqlHealthDumper.Collectors;
using SqlHealthDumper.Domain;
using SqlHealthDumper.Infrastructure;
using SqlHealthDumper.Options;
using SqlHealthDumper.Rendering;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SqlHealthDumper.Orchestration;

/// <summary>
/// 収集コンポーネントを組み合わせてスナップショットを生成する実行オーケストレーター。
/// </summary>
public sealed class SnapshotRunner
{
    /// <summary>
    /// 必要な依存を初期化し、各コレクターを並列処理で実行して Markdown へ出力する。
    /// </summary>
    public async Task RunAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        var originalRoot = config.Output.OutputRoot;
        OutputPathHelper.EnsureRunDirectory(config);
        RebasePaths(originalRoot, config);

        using var logger = new Logger(config.Logging);
        var fileSystem = new FileSystem();
        var sqlLoader = new SqlLoader(config.QuerySource);

        var connectionFactory = new SqlConnectionFactory();
        var sqlHelper = new SqlExecutionHelper(new RetryPolicy());
        var instanceCollector = new InstanceCollector(sqlLoader, connectionFactory, sqlHelper);
        var databaseCollector = new DatabaseCollector(sqlLoader, connectionFactory, sqlHelper);
        var databaseFileCollector = new DatabaseFileCollector(sqlLoader, connectionFactory, sqlHelper);
        var indexCollector = new IndexUsageCollector(sqlLoader, connectionFactory, sqlHelper);
        var topQueriesCollector = new TopQueriesCollector(sqlLoader, connectionFactory, sqlHelper);
        var statsCollector = new StatsCollector(sqlLoader, connectionFactory, sqlHelper);
        var backupCollector = new BackupCollector(sqlLoader, connectionFactory, sqlHelper);
        var tableCollector = new TableCollector(sqlLoader, connectionFactory, sqlHelper, logger);
        var renderer = new MarkdownRenderer(fileSystem, config.Output);

        Directory.CreateDirectory(config.Output.OutputRoot);
        logger.Info($"出力先: {config.Output.OutputRoot}");

        SnapshotState snapshotState;
        List<FailureEntry> failures;
        if (config.Resume.Enabled)
        {
            snapshotState = await fileSystem.ReadJsonAsync<SnapshotState>(config.Resume.SnapshotStatePath, cancellationToken) ?? new SnapshotState();
            failures = await fileSystem.ReadJsonAsync<List<FailureEntry>>(config.Resume.FailuresPath, cancellationToken) ?? new List<FailureEntry>();
        }
        else
        {
            snapshotState = new SnapshotState();
            failures = new List<FailureEntry>();
        }
        var stateGate = new SemaphoreSlim(1, 1);
        var successCounts = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        logger.Info("インスタンス情報収集中...");
        var instanceResult = await instanceCollector.CollectAsync(config, cancellationToken);
        if (instanceResult.Status != CollectorStatus.Success || instanceResult.Payload is null)
        {
            logger.Error($"Instance collection ended with status {instanceResult.Status}: {instanceResult.Reason}", instanceResult.Error);
            return;
        }
        config.Execution.Capabilities = instanceResult.Payload.Capabilities;

        var databasesResult = await databaseCollector.CollectAsync(config, cancellationToken);
        if (databasesResult.Status == CollectorStatus.Success && databasesResult.Payload is not null)
        {
            instanceResult.Payload.Databases.AddRange(databasesResult.Payload);
        }
        else
        {
            logger.Error($"Database collection ended with status {databasesResult.Status}: {databasesResult.Reason}", databasesResult.Error);
        }

        await renderer.WriteInstanceAsync(instanceResult.Payload, config.Output.OutputRoot, cancellationToken);

        var systemIndexNote = config.Execution.IncludeSystemObjects
            ? "Index Usage 出力には sys.* 等のシステムオブジェクトも含めています (--include-system-objects オプションを指定しました)。"
            : "Index Usage 出力には sys.* 等のシステムオブジェクトは含まれていません (--include-system-objects で含められます)。";

        var dbSemaphore = new SemaphoreSlim(Math.Max(1, config.Execution.MaxParallelism));
        var dbTasks = instanceResult.Payload.Databases.Select(db => ProcessDatabaseAsync(db));
        await Task.WhenAll(dbTasks);

        logger.Info($"Markdown output written to {config.Output.OutputRoot}");
        var successSummary = successCounts.Select(kv => $"{kv.Key}:{kv.Value}").ToList();
        logger.Info($"セクション成功件数: {string.Join(", ", successSummary)}");
        if (failures.Count > 0)
        {
            logger.Error($"失敗セクション: {failures.Count} 件");
            foreach (var f in failures.Take(5))
            {
                logger.Error($"DB:{f.Database} Section:{f.Section} Error:{f.Error}");
            }
            if (failures.Count > 5)
            {
                logger.Info("…省略されています。failures.json を参照してください。");
            }
        }

        async Task ProcessDatabaseAsync(DatabaseSnapshot db)
        {
            await dbSemaphore.WaitAsync(cancellationToken);
            try
            {
                logger.Info($"DB処理開始: {db.Name}");
                var filesResult = await databaseFileCollector.CollectAsync(db.Name, config, cancellationToken);
                if (filesResult.Status == CollectorStatus.Success && filesResult.Payload is not null)
                {
                    db.Files.Clear();
                    db.Files.AddRange(filesResult.Payload);
                }
                else if (filesResult.Status == CollectorStatus.Failed)
                {
                    logger.Error($"Database file collection failed for {db.Name}: {filesResult.Reason}", filesResult.Error);
                }

                await renderer.WriteDatabaseAsync(db, config.Output.OutputRoot, cancellationToken);

                if (config.Execution.Sections.CollectTableMarkdown)
                {
                    await HandleSectionAsync(db, "Tables", async () =>
                    {
                        var tableResult = await tableCollector.CollectAsync(db.Name, config, cancellationToken);
                        if (tableResult.Status == CollectorStatus.Success && tableResult.Payload is not null)
                        {
                            db.Tables.Clear();
                            db.Tables.AddRange(tableResult.Payload);
                            await renderer.WriteTablesAsync(db, config.Output.OutputRoot, cancellationToken);
                            return (true, (string?)null);
                        }
                        logger.Error($"Table collection failed for {db.Name}: {tableResult.Reason}", tableResult.Error);
                        return (false, tableResult.Reason);
                    });
                }

                if (config.Execution.Sections.CollectMissingIndex)
                {
                    await HandleSectionAsync(db, "IndexUsage", async () =>
                    {
                        var indexResult = await indexCollector.CollectAsync(db.Name, config, cancellationToken);
                        if (indexResult.Status == CollectorStatus.Success && indexResult.Payload is not null)
                        {
                            db.IndexInsights = indexResult.Payload;
                            AddNote(db.Notes, systemIndexNote);
                            await renderer.WriteIndexInsightAsync(db, config.Output.OutputRoot, cancellationToken);
                            return (true, (string?)null);
                        }
                        if (indexResult.Status == CollectorStatus.Skipped)
                        {
                            logger.Info($"Index usage skipped for {db.Name}: {indexResult.Reason}");
                            if (!string.IsNullOrWhiteSpace(indexResult.Reason))
                            {
                                db.Notes.Add($"Index usage: {indexResult.Reason}");
                            }
                            await renderer.WriteSectionSkippedAsync(db, "12_index_usage_and_missing.md", "Index Usage", indexResult.Reason ?? "skipped", config.Output.OutputRoot, cancellationToken);
                            return (true, (string?)null);
                        }
                        logger.Error($"Index usage collection failed for {db.Name}: {indexResult.Reason}", indexResult.Error);
                        return (false, indexResult.Reason);
                    });
                }

                if (config.Execution.Sections.CollectTopQueries)
                {
                    await HandleSectionAsync(db, "TopQueries", async () =>
                    {
                        var topResult = await topQueriesCollector.CollectAsync(db.Name, config, cancellationToken);
                        if (topResult.Status == CollectorStatus.Success && topResult.Payload is not null)
                        {
                            db.TopQueries.Clear();
                            db.TopQueries.AddRange(topResult.Payload);
                            await renderer.WriteTopQueriesAsync(db, config.Output.OutputRoot, cancellationToken);
                            return (true, (string?)null);
                        }
                        logger.Error($"Top queries collection failed for {db.Name}: {topResult.Reason}", topResult.Error);
                        return (false, topResult.Reason);
                    });
                }

                if (config.Execution.Sections.CollectStats)
                {
                    await HandleSectionAsync(db, "Stats", async () =>
                    {
                        var statsResult = await statsCollector.CollectAsync(db.Name, config, cancellationToken);
                        if (statsResult.Status == CollectorStatus.Success && statsResult.Payload is not null)
                        {
                            db.Stats.Clear();
                            db.Stats.AddRange(statsResult.Payload);
                            await renderer.WriteStatsAsync(db, config.Output.OutputRoot, cancellationToken);
                            return (true, (string?)null);
                        }
                        if (statsResult.Status == CollectorStatus.Skipped)
                        {
                            logger.Info($"Stats collection skipped for {db.Name}: {statsResult.Reason}");
                            if (!string.IsNullOrWhiteSpace(statsResult.Reason))
                            {
                                db.Notes.Add($"Stats: {statsResult.Reason}");
                            }
                            await renderer.WriteSectionSkippedAsync(db, "14_stats_and_params.md", "Stats and Params", statsResult.Reason ?? "skipped", config.Output.OutputRoot, cancellationToken);
                            return (true, (string?)null);
                        }
                        logger.Error($"Stats collection failed for {db.Name}: {statsResult.Reason}", statsResult.Error);
                        return (false, statsResult.Reason);
                    });
                }

                if (config.Execution.Sections.CollectBackupInfo)
                {
                    await HandleSectionAsync(db, "Backup", async () =>
                    {
                        var backupResult = await backupCollector.CollectAsync(db.Name, config, cancellationToken);
                        if ((backupResult.Status == CollectorStatus.Success || backupResult.Status == CollectorStatus.Skipped) && backupResult.Payload is not null)
                        {
                            db.BackupMaintenance = backupResult.Payload;
                            if (backupResult.Status == CollectorStatus.Skipped && !string.IsNullOrWhiteSpace(backupResult.Reason))
                            {
                                db.Notes.Add($"Backup: {backupResult.Reason}");
                            }
                            await renderer.WriteBackupAsync(db, config.Output.OutputRoot, cancellationToken);
                            return (true, (string?)null);
                        }
                        logger.Error($"Backup collection failed for {db.Name}: {backupResult.Reason}", backupResult.Error);
                        return (false, backupResult.Reason);
                    });
                }
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        static void AddNote(List<string> notes, string message)
        {
            if (!notes.Any(n => string.Equals(n, message, StringComparison.Ordinal)))
            {
                notes.Add(message);
            }
        }

        async Task HandleSectionAsync(DatabaseSnapshot db, string sectionKey, Func<Task<(bool Success, string? Reason)>> action)
        {
            if (config.Resume.Enabled && snapshotState.IsCompleted(db.Name, sectionKey))
            {
                logger.Info($"{sectionKey} skipped (resume): {db.Name}");
                successCounts.AddOrUpdate(sectionKey, 1, (_, v) => v + 1);
                return;
            }

            var result = await action();
            if (result.Success)
            {
                if (config.Resume.Enabled)
                {
                    await stateGate.WaitAsync(cancellationToken);
                    try
                    {
                        snapshotState.MarkCompleted(db.Name, sectionKey);
                        await fileSystem.WriteJsonAsync(config.Resume.SnapshotStatePath, snapshotState, cancellationToken);
                    }
                    finally
                    {
                        stateGate.Release();
                    }
                }
                successCounts.AddOrUpdate(sectionKey, 1, (_, v) => v + 1);
            }
            else
            {
                if (config.Resume.Enabled)
                {
                    await stateGate.WaitAsync(cancellationToken);
                    try
                    {
                        failures.Add(new FailureEntry { Database = db.Name, Section = sectionKey, Error = result.Reason ?? "section failed" });
                        await fileSystem.WriteJsonAsync(config.Resume.FailuresPath, failures, cancellationToken);
                    }
                    finally
                    {
                        stateGate.Release();
                    }
                }
                else
                {
                    failures.Add(new FailureEntry { Database = db.Name, Section = sectionKey, Error = result.Reason ?? "section failed" });
                }
            }
        }
    }

    private static void RebasePaths(string originalRoot, AppConfig config)
    {
        var finalRoot = config.Output.OutputRoot;
        if (string.Equals(originalRoot, finalRoot, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (config.Resume.Enabled)
        {
            config.Resume.SnapshotStatePath = RebasePath(config.Resume.SnapshotStatePath, originalRoot, finalRoot, "snapshot_state.json");
            config.Resume.FailuresPath = RebasePath(config.Resume.FailuresPath, originalRoot, finalRoot, "failures.json");
        }

        if (!string.IsNullOrWhiteSpace(config.Logging.LogFilePath))
        {
            config.Logging.LogFilePath = RebasePath(config.Logging.LogFilePath, originalRoot, finalRoot, "log.txt");
        }
    }

    private static string RebasePath(string path, string originalRoot, string finalRoot, string defaultName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Path.Combine(finalRoot, defaultName);
        }

        if (Path.IsPathRooted(path))
        {
            if (!string.IsNullOrWhiteSpace(originalRoot) &&
                path.StartsWith(originalRoot, StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(finalRoot, Path.GetFileName(path));
            }
            return path;
        }

        return Path.Combine(finalRoot, Path.GetFileName(path));
    }
}
