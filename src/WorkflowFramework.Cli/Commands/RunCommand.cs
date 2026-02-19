using System.CommandLine;

namespace WorkflowFramework.Cli.Commands;

public static class RunCommand
{
    public static Command Create()
    {
        var fileArg = new Argument<FileInfo>("file", "Path to workflow definition file (YAML or JSON).");
        var command = new Command("run", "Deserialize and execute a workflow.")
        {
            fileArg
        };
        command.SetHandler(HandleAsync, fileArg);
        return command;
    }

    internal static async Task<int> HandleAsync(FileInfo file)
    {
        return await HandleAsync(file, Console.Out, Console.Error);
    }

    internal static async Task<int> HandleAsync(FileInfo file, TextWriter stdout, TextWriter stderr)
    {
        if (!file.Exists)
        {
            await stderr.WriteLineAsync($"Error: File not found: {file.FullName}");
            return 1;
        }

        try
        {
            var content = await File.ReadAllTextAsync(file.FullName);
            var dto = WorkflowFileHelper.Deserialize(file.FullName, content);

            var builder = new Builder.WorkflowBuilder();
            builder.WithName(dto.Name);
            foreach (var step in dto.Steps)
            {
                var stepName = step.Name;
                builder.Step(stepName, _ =>
                {
                    stdout.WriteLine($"  [exec] {stepName}");
                    return Task.CompletedTask;
                });
            }

            var workflow = builder.Build();
            var context = new WorkflowContext();

            await stdout.WriteLineAsync($"Running workflow: {dto.Name} ({dto.Steps.Count} steps)");
            var result = await workflow.ExecuteAsync(context);
            await stdout.WriteLineAsync($"Result: {result.Status}");

            return result.Status == WorkflowStatus.Completed ? 0 : 1;
        }
        catch (Exception ex)
        {
            await stderr.WriteLineAsync($"Error: {ex.Message}");
            return 1;
        }
    }
}
