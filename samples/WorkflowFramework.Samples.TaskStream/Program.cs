using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Samples.TaskStream.Extensions;
using WorkflowFramework.Samples.TaskStream.Models;
using WorkflowFramework.Samples.TaskStream.Store;
using WorkflowFramework.Samples.TaskStream.Workflows;

// ═══════════════════════════════════════════════════════════════
//  TaskStream — Intelligent Task Extraction & Orchestration
// ═══════════════════════════════════════════════════════════════

Console.WriteLine("╔══════════════════════════════════════════════════════╗");
Console.WriteLine("║  🌊 TaskStream — AI-Powered Task Pipeline           ║");
Console.WriteLine("╚══════════════════════════════════════════════════════╝");
Console.WriteLine();

// 1. Sample messages simulating real-world input
var sampleMessages = new List<SourceMessage>
{
    new()
    {
        Source = "chat",
        RawContent = "Schedule team standup Monday 9am, pick up milk on the way home, deploy v2.1 hotfix to staging"
    },
    new()
    {
        Source = "chat",
        RawContent = "Review PR #87 for the auth module, send quarterly report to finance team"
    },
    new()
    {
        Source = "chat",
        RawContent = "Buy birthday present for Sarah, update the CI pipeline to use .NET 10"
    }
};

// 2. Set up DI
var services = new ServiceCollection();
services.AddTaskStream(sampleMessages, args);
using var sp = services.BuildServiceProvider();

// 3. Run the pipeline
Console.WriteLine("▶ Running TaskStream pipeline...");
Console.WriteLine();

var orchestrator = sp.GetRequiredService<TaskStreamOrchestrator>();
var sw = System.Diagnostics.Stopwatch.StartNew();
var result = await orchestrator.ExecuteAsync();
sw.Stop();

Console.WriteLine();
Console.WriteLine($"▶ Pipeline completed in {sw.ElapsedMilliseconds}ms — Status: {result.Status}");
Console.WriteLine();

// 4. Print the markdown report
if (result.Context.Properties.TryGetValue("markdownReport", out var report))
{
    Console.WriteLine("═══════════════════════════════════════════════════════");
    Console.WriteLine(report);
}

// 5. Show store contents
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("📦 Todo Store Contents:");
Console.WriteLine();

var store = sp.GetRequiredService<ITodoStore>();
var allTodos = await store.GetAllAsync();
foreach (var todo in allTodos)
{
    var status = todo.Status == TodoStatus.Completed ? "✅" : "📌";
    Console.WriteLine($"  {status} [{todo.Category}] {todo.Title}");
    if (todo.Enrichments.Count > 0)
    {
        foreach (var (key, value) in todo.Enrichments)
            Console.WriteLine($"       {key}: {value}");
    }
}

// 6. File watcher demo hint
Console.WriteLine();
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("💡 File Watcher Mode:");
Console.WriteLine("   To use the file watcher source, create an 'inbox/' directory");
Console.WriteLine("   and drop .txt or .md files into it. Each file's content will");
Console.WriteLine("   be processed as a new source message.");
Console.WriteLine();
Console.WriteLine("   Example:");
Console.WriteLine("     echo \"Buy groceries and schedule dentist appointment\" > inbox/tasks.txt");
