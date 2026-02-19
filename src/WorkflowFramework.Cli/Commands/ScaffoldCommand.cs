using System.CommandLine;

namespace WorkflowFramework.Cli.Commands;

public static class ScaffoldCommand
{
    public static Command Create()
    {
        var nameArg = new Argument<string>("name", "Name for the new workflow.");
        var command = new Command("scaffold", "Generate a starter workflow YAML file.")
        {
            nameArg
        };
        command.SetHandler(HandleAsync, nameArg);
        return command;
    }

    internal static async Task<int> HandleAsync(string name)
    {
        return await HandleAsync(name, Console.Out, Console.Error);
    }

    internal static async Task<int> HandleAsync(string name, TextWriter stdout, TextWriter stderr)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            await stderr.WriteLineAsync("Error: Workflow name is required.");
            return 1;
        }

        var yaml = GenerateYaml(name);
        var fileName = $"{name}.yaml";

        if (File.Exists(fileName))
        {
            await stderr.WriteLineAsync($"Error: File already exists: {fileName}");
            return 1;
        }

        await File.WriteAllTextAsync(fileName, yaml);
        await stdout.WriteLineAsync($"Created {fileName}");
        return 0;
    }

    internal static string GenerateYaml(string name)
    {
        return $"""
               name: {name}
               version: 1
               steps:
                 - name: Step1
                   type: action
                 - name: Step2
                   type: action
               """;
    }
}
