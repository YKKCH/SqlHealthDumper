using Markdig;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using SqlHealthDumper.Options;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SqlHealthDumper.Dashboard;

/// <summary>
/// serve サブコマンドで起動するローカル Web ダッシュボード。
/// </summary>
public sealed class DashboardServer
{
    public async Task RunAsync(ServeOptions options, CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://{options.Host}:{options.Port}");

        builder.Services.ConfigureHttpJsonOptions(opts =>
        {
            opts.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            opts.SerializerOptions.WriteIndented = false;
        });

        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        builder.Services.AddSingleton(new SnapshotCatalog(options.RootPath));
        builder.Services.AddSingleton(provider =>
        {
            var catalog = provider.GetRequiredService<SnapshotCatalog>();
            return new SnapshotFileService(catalog, pipeline);
        });

        var app = builder.Build();

        ConfigureStaticFiles(app);
        MapApiEndpoints(app);

        var url = $"http://{options.Host}:{options.Port}/";
        Console.WriteLine($"Snapshot dashboard listening on {url}");
        Console.WriteLine($"Serving snapshots under: {options.RootPath}");

        if (options.OpenBrowser)
        {
            TryOpenBrowser(url);
        }

        await app.RunAsync();
    }

    private static void ConfigureStaticFiles(WebApplication app)
    {
        var webRoot = Path.Combine(AppContext.BaseDirectory, "WebDashboard");
        if (!Directory.Exists(webRoot))
        {
            Console.Error.WriteLine("警告: WebDashboard フォルダが見つかりません。UI を表示できません。");
            return;
        }

        var provider = new PhysicalFileProvider(webRoot);
        app.UseDefaultFiles(new DefaultFilesOptions
        {
            FileProvider = provider,
            DefaultFileNames = new List<string> { "index.html" }
        });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = provider,
            ContentTypeProvider = new FileExtensionContentTypeProvider()
        });
    }

    private static void MapApiEndpoints(WebApplication app)
    {
        app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

        app.MapGet("/api/snapshots", (HttpContext context, SnapshotCatalog catalog) =>
        {
            var refresh = context.Request.Query.ContainsKey("refresh");
            var snapshots = catalog.GetSnapshots(refresh);
            return Results.Ok(snapshots);
        });

        app.MapGet("/api/snapshots/{id}/files", (string id, SnapshotFileService fileService) =>
        {
            var files = fileService.ListFiles(id);
            return files is null ? Results.NotFound() : Results.Ok(files);
        });

        app.MapGet("/api/snapshots/{id}/file", async (string id, HttpContext context, SnapshotFileService fileService, CancellationToken ct) =>
        {
            var relativePath = context.Request.Query["path"].ToString();
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return Results.BadRequest(new { error = "path is required" });
            }

            var content = await fileService.ReadFileAsync(id, relativePath, ct);
            return content is null ? Results.NotFound() : Results.Ok(content);
        });
    }

    private static void TryOpenBrowser(string url)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ブラウザの起動に失敗しました: {ex.Message}");
        }
    }
}
