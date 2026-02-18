# Contributing to WorkflowFramework

Thank you for your interest in contributing! This document provides guidelines for contributing to WorkflowFramework.

## Getting Started

1. Fork the repository
2. Clone your fork: `git clone https://github.com/YOUR-USERNAME/WorkflowFramework.git`
3. Create a branch: `git checkout -b feature/my-feature`
4. Make your changes
5. Run tests: `dotnet test -c Release`
6. Push and create a Pull Request

## Development Environment

- .NET 8.0, 9.0, or 10.0 SDK
- Any IDE (Visual Studio, Rider, VS Code)

## Coding Standards

- Follow existing code style and naming conventions
- Use `ConfigureAwait(false)` on all `await` calls in library code
- Add XML documentation comments to all public APIs
- Keep methods focused and small
- Prefer immutability where practical

## Pull Request Process

1. Ensure all tests pass (`dotnet test -c Release`)
2. Add tests for new functionality
3. Update documentation if needed
4. Use [conventional commits](https://www.conventionalcommits.org/):
   - `feat:` for new features
   - `fix:` for bug fixes
   - `docs:` for documentation changes
   - `test:` for test additions
   - `chore:` for maintenance tasks

## Reporting Issues

- Use the issue templates provided
- Include reproduction steps for bugs
- Specify your .NET version and OS

## Code of Conduct

This project follows the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md).
