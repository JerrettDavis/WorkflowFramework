using Xunit;

// Exclude all UI tests from CI (no Playwright/Aspire available)
[assembly: AssemblyTrait("Category", "UI")]
