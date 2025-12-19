using System.Collections.Generic;
using SqlHealthDumper.Options;
using SqlHealthDumper.Orchestration;
using SqlHealthDumper.Dashboard;

return await RunAsync(args);

static async Task<int> RunAsync(string[] args)
{
    try
    {
        var cliResult = CliParser.Parse(args);
        return cliResult.Command switch
        {
            CliCommand.Run when cliResult.RunOptions is not null
                => await RunSnapshotAsync(cliResult.RunOptions),
            CliCommand.Serve when cliResult.ServeOptions is not null
                => await RunDashboardAsync(cliResult.ServeOptions),
            _ => throw new InvalidOperationException("有効なコマンドを指定してください。")
        };
    }
    catch (ArgumentException ex)
    {
        Console.Error.WriteLine(ex.Message);
        PrintSampleCommands();
        PrintServeSampleCommands();
        return 1;
    }
}

static async Task<int> RunSnapshotAsync(CliOptions cliOptions)
{
    var validationErrors = CliValidator.Validate(cliOptions);
    if (validationErrors.Count > 0)
    {
        WriteValidationErrors(validationErrors);
        PrintSampleCommands();
        return 1;
    }

    var config = AppConfigFactory.FromCli(cliOptions);
    var runner = new SnapshotRunner();
    Console.WriteLine("SQL Insight Snapshot starting...");
    await runner.RunAsync(config);
    Console.WriteLine("SQL Insight Snapshot finished.");
    return 0;
}

static async Task<int> RunDashboardAsync(ServeOptions serveOptions)
{
    var validationErrors = CliValidator.ValidateServe(serveOptions);
    if (validationErrors.Count > 0)
    {
        WriteValidationErrors(validationErrors);
        PrintServeSampleCommands();
        return 1;
    }

    var server = new DashboardServer();
    await server.RunAsync(serveOptions);
    return 0;
}

static void WriteValidationErrors(List<string> errors)
{
    Console.Error.WriteLine("引数が不足または不正です:");
    foreach (var error in errors)
    {
        Console.Error.WriteLine($"- {error}");
    }
}

static void PrintSampleCommands()
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("サンプルコマンド:");
    Console.Error.WriteLine(CliValidator.BuildSampleCommands());
}

static void PrintServeSampleCommands()
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("Web ダッシュボード例:");
    Console.Error.WriteLine("SqlHealthDumper serve --path ./result --port 5080");
}
