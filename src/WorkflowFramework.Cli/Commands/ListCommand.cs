using System.CommandLine;

namespace WorkflowFramework.Cli.Commands;

public static class ListCommand
{
    public static Command Create()
    {
        var fileArg = new Argument<FileInfo>("file", "Path to workflow definition file (YAML or JSON).");
        var command = new Command("list", "List steps in a workflow definition.")
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

            await stdout.WriteLineAsync($"Workflow: {dto.Name} (v{dto.Version})");
            await stdout.WriteLineAsync($"Steps ({dto.Steps.Count}):");

            PrintSteps(stdout, dto.Steps, indent: 0);
            return 0;
        }
        catch (Exception ex)
        {
            await stderr.WriteLineAsync($"Error: {ex.Message}");
            return 1;
        }
    }

    private static void PrintSteps(TextWriter writer, List<Serialization.StepDefinitionDto> steps, int indent)
    {
        var prefix = new string(' ', indent * 2);
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            writer.WriteLine($"{prefix}  {i + 1}. [{step.Type}] {step.Name}");

            if (step.Steps is { Count: > 0 })
                PrintSteps(writer, step.Steps, indent + 1);
            if (step.Then is not null)
            {
                writer.WriteLine($"{prefix}    Then:");
                PrintSteps(writer, [step.Then], indent + 2);
            }
            if (step.Else is not null)
            {
                writer.WriteLine($"{prefix}    Else:");
                PrintSteps(writer, [step.Else], indent + 2);
            }
        }
    }
}
