# Markdown Plain Text Stats Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the main-window body statistics count rendered Markdown plain text instead of Markdown source syntax.

**Architecture:** Keep the existing converter and binding flow. Replace only `NoteNavigationHelper.FormatBodyStats` input normalization with Markdig plain-text rendering, then count normalized plain-text lines and non-newline characters.

**Tech Stack:** .NET 8 WPF, xUnit, Markdig

## Global Constraints

- Work only in `C:\Users\Cristin\Desktop\VibeCoding\qingjian-ui-polish` on `feature/ui-polish`.
- Do not modify or merge `develop`.
- Run .NET commands serially with `-c Release`.
- Keep quick-note plain-text statistics unchanged.

---

### Task 1: Lock the Markdown plain-text behavior with tests

**Files:**
- Modify: `tests/QingJian.App.Tests/ViewModels/NoteNavigationHelperTests.cs`

**Interfaces:**
- Consumes: `NoteNavigationHelper.FormatBodyStats(string? markdown)`
- Produces: Regression expectations for plain-text line and character counts

- [ ] **Step 1: Replace the raw-Markdown theory with plain-text cases**

Add cases proving that headings and emphasis markers are excluded, link labels remain while targets are excluded, image syntax is excluded, and visible list/code content remains.

- [ ] **Step 2: Run the focused test and verify it fails**

Run: `dotnet test tests/QingJian.App.Tests/QingJian.App.Tests.csproj -c Release --filter FullyQualifiedName~NoteNavigationHelperTests.FormatBodyStats`

Expected: FAIL because the existing method counts raw Markdown syntax.

### Task 2: Parse Markdown into plain text before counting

**Files:**
- Modify: `src/QingJian.App/QingJian.App.csproj`
- Modify: `src/QingJian.App/ViewModels/NoteNavigationHelper.cs`

**Interfaces:**
- Consumes: Markdig `Markdown.ToPlainText(string, MarkdownPipeline, MarkdownParserContext?)`
- Produces: Existing `FormatBodyStats(string?)` output using rendered plain text

- [ ] **Step 1: Add Markdig**

Add `PackageReference Include="Markdig" Version="1.3.2"` to the application project.

- [ ] **Step 2: Implement the minimal conversion**

Build one static advanced-extension pipeline, call `Markdown.ToPlainText`, normalize CRLF/CR to LF, trim renderer boundary newlines, and count lines plus non-newline characters.

- [ ] **Step 3: Run focused tests**

Run: `dotnet test tests/QingJian.App.Tests/QingJian.App.Tests.csproj -c Release --filter FullyQualifiedName~NoteNavigationHelperTests.FormatBodyStats`

Expected: all focused cases pass.

### Task 3: Verify and commit

**Files:**
- Verify all modified production, test, and documentation files

**Interfaces:**
- Consumes: completed implementation
- Produces: verified branch commit

- [ ] **Step 1: Run the complete test suite**

Run: `dotnet test -c Release`

Expected: all tests pass with zero failures.

- [ ] **Step 2: Build the solution**

Run: `dotnet build -c Release --no-restore`

Expected: zero warnings and zero errors.

- [ ] **Step 3: Check branch isolation and diff quality**

Run: `git diff --check`, `git status --short --branch`, and `git rev-parse develop`.

- [ ] **Step 4: Commit**

Commit production and test changes with message `fix: count markdown body as plain text`.
