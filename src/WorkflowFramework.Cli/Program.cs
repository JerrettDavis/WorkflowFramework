using System.CommandLine;
using WorkflowFramework.Cli.Commands;

var rootCommand = new RootCommand("WorkflowFramework CLI — scaffold, run, list, and validate workflows.")
{
    RunCommand.Create(),
    ListCommand.Create(),
    ScaffoldCommand.Create(),
    ValidateCommand.Create()
};

return await rootCommand.InvokeAsync(args);
