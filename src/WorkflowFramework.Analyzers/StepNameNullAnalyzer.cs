using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace WorkflowFramework.Analyzers;

/// <summary>
/// Analyzer that warns about common workflow mistakes.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StepNameNullAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Diagnostic ID for step name returning null.</summary>
    public const string DiagnosticId = "WF001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Step.Name should not return null",
        "IStep.Name property should not return null or empty",
        "WorkflowFramework",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);
    }

    private static void AnalyzeProperty(SyntaxNodeAnalysisContext context)
    {
        var property = (PropertyDeclarationSyntax)context.Node;
        if (property.Identifier.Text != "Name") return;

        var symbol = context.SemanticModel.GetDeclaredSymbol(property);
        if (symbol?.ContainingType == null) return;

        var implementsIStep = false;
        foreach (var iface in symbol.ContainingType.AllInterfaces)
        {
            if (iface.Name == "IStep" && iface.ContainingNamespace.ToDisplayString() == "WorkflowFramework")
            {
                implementsIStep = true;
                break;
            }
        }

        if (!implementsIStep) return;

        // Check for null/empty literal returns
        if (property.ExpressionBody?.Expression is LiteralExpressionSyntax literal)
        {
            if (literal.IsKind(SyntaxKind.NullLiteralExpression) ||
                (literal.IsKind(SyntaxKind.StringLiteralExpression) && literal.Token.ValueText == ""))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, property.GetLocation()));
            }
        }
    }
}
