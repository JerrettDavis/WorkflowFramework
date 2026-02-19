namespace WorkflowFramework.Extensions.Agents;

/// <summary>
/// Estimates token count for text.
/// </summary>
public interface ITokenEstimator
{
    /// <summary>Estimates the number of tokens in the given text.</summary>
    int EstimateTokens(string text);
}

/// <summary>
/// Default token estimator using chars/4 heuristic.
/// </summary>
public sealed class DefaultTokenEstimator : ITokenEstimator
{
    /// <inheritdoc />
    public int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return (text.Length + 3) / 4;
    }
}
