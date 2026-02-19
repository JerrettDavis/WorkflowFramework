using System.CommandLine;

namespace WorkflowFramework.Cli.Commands;

public static class ValidateCommand
{
    public static Command Create()
    {
        var fileArg = new Argument<FileInfo>("file", "Path to workflow definition file (YAML or JSON).");
        var command = new Command("validate", "Validate a workflow definition.")
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

            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(dto.Name))
                errors.Add("Workflow name is required.");

            if (dto.Steps.Count == 0)
                errors.Add("Workflow must have at least one step.");

            for (var i = 0; i < dto.Steps.Count; i++)
            {
                var step = dto.Steps[i];
                if (string.IsNullOrWhiteSpace(step.Name))
                    errors.Add($"Step {i + 1}: name is required.");
                if (string.IsNullOrWhiteSpace(step.Type))
                    errors.Add($"Step {i + 1} ({step.Name}): type is required.");
            }

            if (errors.Count > 0)
            {
                await stderr.WriteLineAsync($"Validation failed with {errors.Count} error(s):");
                foreach (var error in errors)
                    await stderr.WriteLineAsync($"  - {error}");
                return 1;
            }

            await stdout.WriteLineAsync($"✓ {file.Name} is valid ({dto.Name}, {dto.Steps.Count} steps)");
            return 0;
        }
        catch (Exception ex)
        {
            await stderr.WriteLineAsync($"Error: {ex.Message}");
            return 1;
        }
    }
}
