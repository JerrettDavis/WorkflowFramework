using FluentAssertions;
using WorkflowFramework.Cli.Commands;

namespace WorkflowFramework.Cli.Tests;

public class ListCommandTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public ListCommandTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, true);

    [Fact]
    public async Task List_ValidYaml_PrintsSteps()
    {
        var file = Path.Combine(_tempDir, "test.yaml");
        await File.WriteAllTextAsync(file, """
            name: TestWorkflow
            version: 1
            steps:
              - name: Step1
                type: action
              - name: Step2
                type: action
            """);

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var result = await ListCommand.HandleAsync(new FileInfo(file), stdout, stderr);

        result.Should().Be(0);
        var output = stdout.ToString();
        output.Should().Contain("TestWorkflow");
        output.Should().Contain("Step1");
        output.Should().Contain("Step2");
    }

    [Fact]
    public async Task List_ValidJson_PrintsSteps()
    {
        var file = Path.Combine(_tempDir, "test.json");
        await File.WriteAllTextAsync(file, """
            {
              "name": "JsonWorkflow",
              "version": 1,
              "steps": [
                { "name": "A", "type": "action" },
                { "name": "B", "type": "action" }
              ]
            }
            """);

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var result = await ListCommand.HandleAsync(new FileInfo(file), stdout, stderr);

        result.Should().Be(0);
        stdout.ToString().Should().Contain("JsonWorkflow");
    }

    [Fact]
    public async Task List_MissingFile_ReturnsError()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var result = await ListCommand.HandleAsync(new FileInfo("/nonexistent.yaml"), stdout, stderr);

        result.Should().Be(1);
        stderr.ToString().Should().Contain("File not found");
    }
}

public class ValidateCommandTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public ValidateCommandTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, true);

    [Fact]
    public async Task Validate_ValidFile_ReturnsSuccess()
    {
        var file = Path.Combine(_tempDir, "valid.yaml");
        await File.WriteAllTextAsync(file, """
            name: Valid
            version: 1
            steps:
              - name: Step1
                type: action
            """);

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var result = await ValidateCommand.HandleAsync(new FileInfo(file), stdout, stderr);

        result.Should().Be(0);
        stdout.ToString().Should().Contain("valid");
    }

    [Fact]
    public async Task Validate_EmptySteps_ReturnsError()
    {
        var file = Path.Combine(_tempDir, "empty.yaml");
        await File.WriteAllTextAsync(file, """
            name: Empty
            version: 1
            steps: []
            """);

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var result = await ValidateCommand.HandleAsync(new FileInfo(file), stdout, stderr);

        result.Should().Be(1);
        stderr.ToString().Should().Contain("at least one step");
    }

    [Fact]
    public async Task Validate_MissingFile_ReturnsError()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var result = await ValidateCommand.HandleAsync(new FileInfo("/nonexistent.json"), stdout, stderr);

        result.Should().Be(1);
        stderr.ToString().Should().Contain("File not found");
    }
}

public class RunCommandTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public RunCommandTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, true);

    [Fact]
    public async Task Run_ValidYaml_ExecutesAndCompletes()
    {
        var file = Path.Combine(_tempDir, "run.yaml");
        await File.WriteAllTextAsync(file, """
            name: RunTest
            version: 1
            steps:
              - name: Hello
                type: action
            """);

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var result = await RunCommand.HandleAsync(new FileInfo(file), stdout, stderr);

        result.Should().Be(0);
        var output = stdout.ToString();
        output.Should().Contain("Running workflow: RunTest");
        output.Should().Contain("[exec] Hello");
        output.Should().Contain("Completed");
    }

    [Fact]
    public async Task Run_MissingFile_ReturnsError()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var result = await RunCommand.HandleAsync(new FileInfo("/nonexistent.yaml"), stdout, stderr);

        result.Should().Be(1);
        stderr.ToString().Should().Contain("File not found");
    }
}

public class ScaffoldCommandTests : IDisposable
{
    private readonly string _originalDir = Directory.GetCurrentDirectory();
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public ScaffoldCommandTests()
    {
        Directory.CreateDirectory(_tempDir);
        Directory.SetCurrentDirectory(_tempDir);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDir);
        Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task Scaffold_CreatesYamlFile()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var result = await ScaffoldCommand.HandleAsync("MyWorkflow", stdout, stderr);

        result.Should().Be(0);
        stdout.ToString().Should().Contain("Created MyWorkflow.yaml");
        File.Exists(Path.Combine(_tempDir, "MyWorkflow.yaml")).Should().BeTrue();

        var content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "MyWorkflow.yaml"));
        content.Should().Contain("name: MyWorkflow");
        content.Should().Contain("steps:");
    }

    [Fact]
    public async Task Scaffold_ExistingFile_ReturnsError()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Existing.yaml"), "existing");

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var result = await ScaffoldCommand.HandleAsync("Existing", stdout, stderr);

        result.Should().Be(1);
        stderr.ToString().Should().Contain("already exists");
    }

    [Fact]
    public void GenerateYaml_ContainsName()
    {
        var yaml = ScaffoldCommand.GenerateYaml("Test");
        yaml.Should().Contain("name: Test");
        yaml.Should().Contain("steps:");
    }
}

public class WorkflowFileHelperTests
{
    [Fact]
    public void Deserialize_Json_Works()
    {
        var dto = WorkflowFileHelper.Deserialize("test.json", """
            {
              "name": "Test",
              "steps": [{ "name": "S1", "type": "action" }]
            }
            """);

        dto.Name.Should().Be("Test");
        dto.Steps.Should().HaveCount(1);
    }

    [Fact]
    public void Deserialize_Yaml_Works()
    {
        var dto = WorkflowFileHelper.Deserialize("test.yaml", """
            name: Test
            steps:
              - name: S1
                type: action
            """);

        dto.Name.Should().Be("Test");
        dto.Steps.Should().HaveCount(1);
    }

    [Fact]
    public void Deserialize_UnsupportedExtension_Throws()
    {
        var act = () => WorkflowFileHelper.Deserialize("test.txt", "content");
        act.Should().Throw<InvalidOperationException>().WithMessage("*Unsupported*");
    }
}
