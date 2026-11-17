# Agent Guidelines

> **Language requirement:** This `AGENTS.md` file must always be written and maintained in **English**.

## Code Formatting

- Always run the CSharpier formatter across the entire repository after every code change:
  ```powershell
  dotnet tool restore
  dotnet csharpier format .
  ```
- Respect `.csharpierignore` (for example, `*.xaml` files are intentionally excluded from CSharpier formatting).

## Coding Standards & Best Practices

- Follow Microsoft's recommended coding conventions and best practices for **.NET**, **C#**, and **WinUI 3 / Windows App SDK** (naming conventions, `async`/`await` patterns, resource/handle disposal, nullability, and trimming/CsWinRT compatibility).
- Always proactively recommend fixes, improvements, or potential edge-case issues to the user whenever they are viable and make sense for the task or codebase.
