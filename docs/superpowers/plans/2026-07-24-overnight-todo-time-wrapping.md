# Overnight Todo Time Wrapping Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show an overnight todo's next-day end time on a second line so the complete time remains visible in narrow preview and edit-list columns.

**Architecture:** Keep the existing shared `TodoTimeDisplayConverter` as the single formatting boundary. Change only its overnight string to contain a line-feed; `TodoPreviewDisplayConverter` and all four existing XAML bindings inherit the behavior without duplicated UI logic.

**Tech Stack:** .NET 8, WPF, xUnit

## Global Constraints

- Work only in `C:\Users\Cristin\Desktop\VibeCoding\qingjian-ui-polish` on `feature/ui-polish`.
- Do not modify or merge `develop`.
- Run .NET commands serially with `-c Release`.
- Format overnight time as `23:00-次日\n01:00`.
- Keep same-day ranges, single times, missing times, date attribution, persistence, column widths, and popover dimensions unchanged.
- Apply the shared format to the eight-day preview, today's desktop list, calendar-date preview, and edit-todo list.

---

### Task 1: Wrap the overnight end time

**Files:**
- Modify: `tests/QingJian.App.Tests/TodoWidgets/TodoTimeDisplayConverterTests.cs`
- Modify: `tests/QingJian.App.Tests/TodoWidgets/TodoPreviewDisplayConverterTests.cs`
- Modify: `src/QingJian.App/TodoWidgets/TodoTimeDisplayConverter.cs`

**Interfaces:**
- Consumes: `TodoTimeRangeRules.IsOvernight(TimeOnly startTime, TimeOnly endTime)`
- Produces: `TodoTimeDisplayConverter.Convert(...)` returning `23:00-次日\n01:00` for overnight ranges
- Preserves: `TodoPreviewDisplayConverter.Convert(...)` combining the formatted time and todo text

- [ ] **Step 1: Change the converter expectations first**

In `TodoTimeDisplayConverterTests.Convert_LabelsOvernightRangeEndAsNextDay`, use:

```csharp
Assert.Equal("23:00-次日\n01:00", text);
```

In `TodoPreviewDisplayConverterTests.Convert_PrefixesOvernightRangeWithNextDayLabel`, use:

```csharp
Assert.Equal("23:00-次日\n01:00 夜间值班", Convert(todo));
```

- [ ] **Step 2: Run focused tests and verify RED**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --filter "FullyQualifiedName~TodoTimeDisplayConverterTests|FullyQualifiedName~TodoPreviewDisplayConverterTests"
```

Expected: the two overnight assertions fail because the converter still returns `23:00-次日 01:00`; same-day, single-time, and no-time tests remain passing.

- [ ] **Step 3: Add the line-feed in the shared converter**

In `TodoTimeDisplayConverter.Convert`, keep existing branches and change only the overnight expression:

```csharp
return TodoTimeRangeRules.IsOvernight(todo.StartTime.Value, todo.EndTime.Value)
    ? $"{start}-次日\n{end}"
    : $"{start}-{end}";
```

- [ ] **Step 4: Run focused tests and verify GREEN**

Run the Step 2 command again.

Expected: all `TodoTimeDisplayConverterTests` and `TodoPreviewDisplayConverterTests` pass.

- [ ] **Step 5: Run XAML binding tests**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --filter FullyQualifiedName~TodoWidgetWindowXamlTests
```

Expected: all tests pass, confirming the eight-day preview uses `TodoPreviewDisplayConverter` and the other three lists use `TodoTimeDisplayConverter`.

- [ ] **Step 6: Commit the behavior change**

```powershell
git add src\QingJian.App\TodoWidgets\TodoTimeDisplayConverter.cs tests\QingJian.App.Tests\TodoWidgets\TodoTimeDisplayConverterTests.cs tests\QingJian.App.Tests\TodoWidgets\TodoPreviewDisplayConverterTests.cs
git commit -m "fix: wrap overnight todo end times"
```

### Task 2: Full verification and isolation check

**Files:**
- Verify: all files changed by Task 1

**Interfaces:**
- Consumes: completed overnight time wrapping behavior
- Produces: a verified clean `feature/ui-polish` worktree without changes to `develop`

- [ ] **Step 1: Run the complete Release test suite**

```powershell
dotnet test -c Release
```

Expected: zero failed and zero skipped tests.

- [ ] **Step 2: Build the solution**

```powershell
dotnet build -c Release --no-restore
```

Expected: zero warnings and zero errors.

- [ ] **Step 3: Check formatting and branch isolation**

Run each command from the feature worktree:

```powershell
git diff --check develop..HEAD
git status --short --branch
git rev-parse develop
git -C C:\Users\Cristin\Desktop\VibeCoding\qingjian status --short --branch
```

Expected: no whitespace errors, a clean `feature/ui-polish` worktree, `develop` still at `ba51b89cb6738f0dbb059e37bc40248e4bbd24b6`, and the main worktree still on `develop` with only its pre-existing untracked `outputs/` directory.
