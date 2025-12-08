using System.Collections.Generic;
using SqlHealthDumper.Options;
using SqlHealthDumper.Orchestration;

try
{
    var cliOptions = CliParser.Parse(args);
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
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    PrintSampleCommands();
    return 1;
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
