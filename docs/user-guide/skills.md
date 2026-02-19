# Agent Skills

The `WorkflowFramework.Extensions.Agents.Skills` package implements the [Agent Skills](https://agentskills.io) standard — a convention for packaging reusable agent instructions as `SKILL.md` files with YAML frontmatter.

## Installation

```bash
dotnet add package WorkflowFramework.Extensions.Agents.Skills
```

## Key Types

| Type | Purpose |
|------|---------|
| `SkillDefinition` | Parsed skill with metadata and body |
| `SkillLoader` | Parses `SKILL.md` files (YAML frontmatter + markdown body) |
| `SkillDiscovery` | Scans standard and custom paths for skills |
| `SkillToolProvider` | Exposes skills as invocable tools |
| `SkillContextSource` | Injects skill instructions into agent prompts |

## SKILL.md Format

A skill file consists of YAML frontmatter followed by markdown instructions:

```markdown
---
name: code-review
description: Reviews code for bugs, style issues, and best practices
license: MIT
compatibility: Claude, GPT-4, Qwen
allowed-tools:
  - read_file
  - list_directory
metadata:
  author: team
  version: 1.0
---

# Code Review Skill

When asked to review code:

1. Read the file with `read_file`
2. Check for common bugs and anti-patterns
3. Verify naming conventions and code style
4. Suggest improvements with specific line references
5. Rate severity: critical / warning / info
```

## SkillLoader

Parses `SKILL.md` content or files:

```csharp
// Parse from file
SkillDefinition skill = SkillLoader.ParseFile("./skills/code-review/SKILL.md");

// Parse from string
SkillDefinition skill = SkillLoader.Parse(markdownContent);

Console.WriteLine(skill.Name);           // "code-review"
Console.WriteLine(skill.Description);    // "Reviews code for bugs..."
Console.WriteLine(skill.License);        // "MIT"
Console.WriteLine(skill.AllowedTools);   // ["read_file", "list_directory"]
Console.WriteLine(skill.Body);           // The markdown body after frontmatter
Console.WriteLine(skill.SourcePath);     // File path (when parsed from file)
```

## SkillDiscovery

Scans directories for `SKILL.md` files. Standard paths follow the Agent Skills convention:

```csharp
var discovery = new SkillDiscovery(
    scanStandardPaths: true,
    additionalPaths: new[] { "./project-skills", "/shared/skills" }
);

// Discover all skills
IReadOnlyList<SkillDefinition> skills = discovery.DiscoverAll();

// Or scan a specific directory
IReadOnlyList<SkillDefinition> projectSkills = discovery.ScanDirectory("./project-skills");
```

**Standard paths scanned** (when `scanStandardPaths: true`):
- `~/.agents/skills/`
- `~/.claude/skills/`
- `./.agents/skills/`
- `./.claude/skills/`

Each path is searched recursively for `SKILL.md` files.

## SkillToolProvider

Exposes discovered skills as tools in the `ToolRegistry`. When invoked, a skill tool returns its body (the markdown instructions):

```csharp
var skills = discovery.DiscoverAll();
var toolProvider = new SkillToolProvider(skills);

registry.Register(toolProvider);

// Skills appear as tools
var tools = await toolProvider.ListToolsAsync();
// [{ Name: "code-review", Description: "Reviews code for bugs..." }]

// Invoking returns the skill body
var result = await toolProvider.InvokeToolAsync("code-review", "{}");
// result.Content = "# Code Review Skill\n\nWhen asked to review code:..."
```

## SkillContextSource

Injects skill instructions directly into agent prompts as context:

```csharp
var contextSource = new SkillContextSource(skills);

var workflow = new WorkflowBuilder()
    .AgentLoop(provider, registry, options =>
    {
        options.ContextSources.Add(contextSource);
    })
    .Build();
```

Each skill becomes a `ContextDocument` with:
- `Name` = skill name
- `Content` = skill body (markdown)
- `Source` = file path
- `Metadata["description"]` = skill description

## DI Registration

```csharp
services.AddAgentSkills(options =>
{
    options.ScanStandardPaths = true;
    options.AutoDiscover = true;
    options.AdditionalPaths.Add("./custom-skills");
});

services.AddAgentTooling(); // Auto-discovers SkillToolProvider
```

`AddAgentSkills()` registers:
- `SkillOptions` and `SkillDiscovery` as singletons
- Auto-discovered `IReadOnlyList<SkillDefinition>`
- `SkillToolProvider` as `IToolProvider`
- `SkillContextSource` as `IContextSource`

## Complete Example

```csharp
// Directory structure:
// ./skills/
//   code-review/SKILL.md
//   summarize/SKILL.md
//   translate/SKILL.md

var discovery = new SkillDiscovery(
    scanStandardPaths: false,
    additionalPaths: new[] { "./skills" }
);

var skills = discovery.DiscoverAll();
Console.WriteLine($"Found {skills.Count} skills");

var registry = new ToolRegistry();
registry.Register(new SkillToolProvider(skills));
registry.Register(new FileSystemToolProvider());

var workflow = new WorkflowBuilder()
    .AgentLoop(provider, registry, options =>
    {
        options.SystemPrompt = "You have access to specialized skills. " +
            "Use them to guide your work on complex tasks.";
        options.ContextSources.Add(new SkillContextSource(skills));
        options.MaxIterations = 20;
    })
    .Build();

await workflow.RunAsync(context);
```
