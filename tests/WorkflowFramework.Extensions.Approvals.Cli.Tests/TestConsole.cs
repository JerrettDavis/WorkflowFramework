using System.Text;
using WorkflowFramework.Extensions.Approvals.Cli.Commands;

namespace WorkflowFramework.Extensions.Approvals.Cli.Tests;

/// <summary>
/// A test double for <see cref="IConsole"/> that captures output in a
/// <see cref="StringBuilder"/> for assertion.
/// </summary>
internal sealed class TestConsole : IConsole
{
    private readonly StringBuilder _sb = new();

    /// <summary>Gets all text written to this console.</summary>
    public string Output => _sb.ToString();

    /// <inheritdoc />
    public void WriteLine(string value)
    {
        _sb.AppendLine(value);
    }

    /// <inheritdoc />
    public void Write(string value)
    {
        _sb.Append(value);
    }

    /// <inheritdoc />
    public TextWriter Out => new StringWriter(_sb);
}
