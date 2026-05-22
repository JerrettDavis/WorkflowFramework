namespace WorkflowFramework.Extensions.Approvals.Cli.Commands;

/// <summary>
/// An abstraction over standard output that allows CLI commands to be tested without
/// touching <see cref="System.Console"/>.
/// </summary>
public interface IConsole
{
    /// <summary>Writes <paramref name="value"/> followed by the current line terminator.</summary>
    /// <param name="value">The text to write.</param>
    void WriteLine(string value);

    /// <summary>Writes <paramref name="value"/> without appending a line terminator.</summary>
    /// <param name="value">The text to write.</param>
    void Write(string value);

    /// <summary>Gets the underlying <see cref="TextWriter"/> for this console.</summary>
    TextWriter Out { get; }
}

/// <summary>
/// Default implementation of <see cref="IConsole"/> that delegates to
/// <see cref="System.Console"/>.
/// </summary>
public sealed class SystemConsole : IConsole
{
    /// <inheritdoc />
    public void WriteLine(string value) => Console.WriteLine(value);

    /// <inheritdoc />
    public void Write(string value) => Console.Write(value);

    /// <inheritdoc />
    public TextWriter Out => Console.Out;
}
