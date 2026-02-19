# Dynamic Expressions

The `Extensions.Expressions` package provides runtime expression evaluation and template rendering.

## Installation

```bash
dotnet add package WorkflowFramework.Extensions.Expressions
```

## SimpleExpressionEvaluator

A lightweight evaluator supporting variables, comparisons, boolean logic, arithmetic, and literals:

```csharp
using WorkflowFramework.Extensions.Expressions;

var evaluator = new SimpleExpressionEvaluator();
var vars = new Dictionary<string, object?> { ["age"] = 25, ["threshold"] = 18 };

var result = await evaluator.EvaluateAsync<bool>("age >= threshold", vars);
// true
```

### Supported Operations

- **Comparisons:** `==`, `!=`, `>`, `<`, `>=`, `<=`
- **Boolean:** `&&`, `||`
- **Arithmetic:** `+`, `-`, `*`, `/`
- **Literals:** numbers, `true`/`false`, `null`, quoted strings
- **Variables:** any key from the variables dictionary

## TemplateEngine

Renders strings with `{{expression}}` placeholders:

```csharp
var engine = new TemplateEngine();
var vars = new Dictionary<string, object?> { ["name"] = "Alice", ["total"] = 42.5 };

var output = await engine.RenderAsync("Hello {{name}}, your total is {{total}}.", vars);
// "Hello Alice, your total is 42.5."
```

## IfExpression Builder

Use expressions directly in workflow branching:

```csharp
using WorkflowFramework.Extensions.Expressions;

var workflow = new WorkflowBuilder()
    .IfExpression("amount > 1000")
        .Step(requireApproval)
    .Else()
        .Step(autoApprove)
    .EndIf()
    .Build();
```

The expression is evaluated against the workflow context properties at runtime.

## Custom Evaluators

Implement `IExpressionEvaluator` for more advanced scenarios (e.g., C# scripting, JavaScript):

```csharp
public interface IExpressionEvaluator
{
    string Name { get; }
    Task<T?> EvaluateAsync<T>(string expression, IDictionary<string, object?> variables, CancellationToken ct = default);
    Task<object?> EvaluateAsync(string expression, IDictionary<string, object?> variables, CancellationToken ct = default);
}
```
