# Overnight Todos Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Accept desktop todo ranges that cross midnight, display their next-day end clearly, and keep all date grouping tied to the start date.

**Architecture:** Keep the persisted `TodoItem.Date`, `StartTime`, and `EndTime` fields unchanged. Add one shared rules helper that derives validity and overnight state from the two times, then use it in input validation, service normalization, and display formatting.

**Tech Stack:** .NET 8, WPF, EF Core SQLite, xUnit

## Global Constraints

- Work only in `C:\Users\Cristin\Desktop\VibeCoding\qingjian-ui-polish` on `feature/ui-polish`.
- Do not modify or merge `develop`.
- Run .NET commands serially with `-c Release`.
- `TodoItem.Date` always means the start date.
- `StartTime < EndTime` is same-day, `StartTime > EndTime` is overnight, and equal times are invalid.
- Overnight todos appear only on the start date and never duplicate on the end date.
- Do not add database columns, migrations, date pickers, or an overnight toggle.

---

### Task 1: Shared overnight rules and draft validation

**Files:**
- Create: `src/QingJian.App/TodoWidgets/TodoTimeRangeRules.cs`
- Create: `tests/QingJian.App.Tests/TodoWidgets/TodoTimeRangeRulesTests.cs`
- Modify: `src/QingJian.App/TodoWidgets/TodoWidgetDraftParser.cs`
- Modify: `tests/QingJian.App.Tests/TodoWidgets/TodoWidgetDraftParserTests.cs`

**Interfaces:**
- Produces: `TodoTimeRangeRules.IsValidRange(TimeOnly startTime, TimeOnly endTime)`
- Produces: `TodoTimeRangeRules.IsOvernight(TimeOnly startTime, TimeOnly endTime)`
- Updates: both `ValidateQuickAddDraft` input forms accept overnight ranges and reject equal times

- [ ] **Step 1: Write failing rules and parser tests**

Add tests equivalent to:

```csharp
[Theory]
[InlineData(9, 10, true, false)]
[InlineData(23, 1, true, true)]
[InlineData(8, 8, false, false)]
public void ClassifiesTimeRanges(int startHour, int endHour, bool valid, bool overnight)
{
    var start = new TimeOnly(startHour, 0);
    var end = new TimeOnly(endHour, 0);

    Assert.Equal(valid, TodoTimeRangeRules.IsValidRange(start, end));
    Assert.Equal(overnight, TodoTimeRangeRules.IsOvernight(start, end));
}

[Fact]
public void ValidateQuickAddDraft_AcceptsOvernightRange()
{
    var date = new DateOnly(2026, 7, 23);
    var result = TodoWidgetDraftParser.ValidateQuickAddDraft(date, "值班", "23:00", "01:00");

    Assert.True(result.IsValid);
    Assert.Equal(date, result.Draft!.Date);
    Assert.Equal(TodoTimeKind.Range, result.Draft.TimeKind);
    Assert.Equal(new TimeOnly(23, 0), result.Draft.StartTime);
    Assert.Equal(new TimeOnly(1, 0), result.Draft.EndTime);
}
```

Add a boundary test for `00:01-00:00`: `IsOvernight` must return true and the derived next-day duration must equal 23 hours 59 minutes, while `00:00-00:00` remains invalid. Add the same overnight acceptance assertion for the separate hour/minute overload. Keep the existing equal-time rejection and rename it so it no longer claims every non-increasing range is invalid.

- [ ] **Step 2: Run focused tests and verify RED**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --filter "FullyQualifiedName~TodoTimeRangeRulesTests|FullyQualifiedName~TodoWidgetDraftParserTests"
```

Expected: compilation fails because `TodoTimeRangeRules` does not exist, or overnight parser assertions fail under the current `startTime < endTime` rule.

- [ ] **Step 3: Add the shared rules and update parser validation**

Create:

```csharp
namespace QingJian.App.TodoWidgets;

public static class TodoTimeRangeRules
{
    public static bool IsValidRange(TimeOnly startTime, TimeOnly endTime)
    {
        return startTime != endTime;
    }

    public static bool IsOvernight(TimeOnly startTime, TimeOnly endTime)
    {
        return startTime > endTime;
    }
}
```

In `TodoWidgetDraftParser.ValidateQuickAddDraft`, replace the same-day-only comparison with:

```csharp
else if (startTime is not null &&
         endTime is not null &&
         TodoTimeRangeRules.IsValidRange(startTime.Value, endTime.Value))
{
    timeKind = TodoTimeKind.Range;
}
```

- [ ] **Step 4: Run focused tests and verify GREEN**

Run the Task 1 focused command again.

Expected: all rules and parser tests pass.

- [ ] **Step 5: Commit Task 1**

```powershell
git add src\QingJian.App\TodoWidgets\TodoTimeRangeRules.cs src\QingJian.App\TodoWidgets\TodoWidgetDraftParser.cs tests\QingJian.App.Tests\TodoWidgets\TodoTimeRangeRulesTests.cs tests\QingJian.App.Tests\TodoWidgets\TodoWidgetDraftParserTests.cs
git commit -m "feat: accept overnight todo drafts"
```

### Task 2: Service-layer creation and update protection

**Files:**
- Modify: `src/QingJian.App/Services/TodoService.cs`
- Modify: `tests/QingJian.App.Tests/Services/TodoServiceTests.cs`

**Interfaces:**
- Consumes: `TodoTimeRangeRules.IsValidRange`
- Produces: create and update support for overnight `TodoDraft` values while preserving `TodoDraft.Date`

- [ ] **Step 1: Write failing service tests**

Add tests equivalent to:

```csharp
[Fact]
public async Task CreateTodoAsync_AcceptsOvernightRangeAndKeepsStartDate()
{
    var service = new TodoService(new InMemoryTodoRepository());
    var date = new DateOnly(2026, 7, 23);

    var todo = await service.CreateTodoAsync(new TodoDraft(
        date, "夜间值班", TodoTimeKind.Range, new TimeOnly(23, 0), new TimeOnly(1, 0)));

    Assert.Equal(date, todo.Date);
    Assert.Equal(new TimeOnly(23, 0), todo.StartTime);
    Assert.Equal(new TimeOnly(1, 0), todo.EndTime);
}

[Fact]
public async Task UpdateTodoAsync_ChangesSameDayRangeToOvernightWithoutChangingStartDate()
{
    var repository = new InMemoryTodoRepository();
    var service = new TodoService(repository);
    var date = new DateOnly(2026, 7, 23);
    var todo = await service.CreateTodoAsync(new TodoDraft(
        date, "值班", TodoTimeKind.Range, new TimeOnly(20, 0), new TimeOnly(21, 0)));

    await service.UpdateTodoAsync(todo, new TodoDraft(
        date, "值班", TodoTimeKind.Range, new TimeOnly(23, 0), new TimeOnly(1, 0)));

    Assert.Equal(date, todo.Date);
    Assert.Equal(new TimeOnly(23, 0), todo.StartTime);
    Assert.Equal(new TimeOnly(1, 0), todo.EndTime);
}
```

Keep equal-time rejection and change the existing decreasing-range rejection to equal times, because decreasing ranges are now valid overnight ranges.

- [ ] **Step 2: Run service tests and verify RED**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --filter FullyQualifiedName~TodoServiceTests
```

Expected: overnight create/update tests fail with the existing “end time later than start time” validation.

- [ ] **Step 3: Use the shared validity rule in service normalization**

Replace the range arm in `NormalizeDraft` with:

```csharp
TodoTimeKind.Range when draft.StartTime is not null &&
                        draft.EndTime is not null &&
                        TodoTimeRangeRules.IsValidRange(draft.StartTime.Value, draft.EndTime.Value) => draft,
```

Change the invalid-range exception text to describe the actual rule:

```csharp
TodoTimeKind.Range => throw new ArgumentException(
    "Time ranges require different start and end times.",
    nameof(draft)),
```

- [ ] **Step 4: Run service tests and verify GREEN**

Run the Task 2 focused command again.

Expected: all `TodoServiceTests` pass.

- [ ] **Step 5: Commit Task 2**

```powershell
git add src\QingJian.App\Services\TodoService.cs tests\QingJian.App.Tests\Services\TodoServiceTests.cs
git commit -m "feat: persist overnight todo ranges"
```

### Task 3: Overnight display and start-date attribution

**Files:**
- Modify: `src/QingJian.App/TodoWidgets/TodoTimeDisplayConverter.cs`
- Modify: `tests/QingJian.App.Tests/TodoWidgets/TodoTimeDisplayConverterTests.cs`
- Modify: `tests/QingJian.App.Tests/TodoWidgets/TodoPreviewDisplayConverterTests.cs`
- Modify: `tests/QingJian.App.Tests/TodoWidgets/TodoItemsForDateConverterTests.cs`
- Modify: `tests/QingJian.App.Tests/Services/TodoServiceTests.cs`

**Interfaces:**
- Consumes: `TodoTimeRangeRules.IsOvernight`
- Produces: `23:00-次日 01:00` for overnight ranges
- Preserves: filtering by `TodoItem.Date` and sorting by `StartTime`

- [ ] **Step 1: Write failing display tests and attribution contract tests**

Add:

```csharp
[Fact]
public void Convert_LabelsOvernightRangeEndAsNextDay()
{
    var converter = new TodoTimeDisplayConverter();
    var todo = new TodoItem
    {
        StartTime = new TimeOnly(23, 0),
        EndTime = new TimeOnly(1, 0)
    };

    var text = converter.Convert(todo, typeof(string), null!, CultureInfo.InvariantCulture);

    Assert.Equal("23:00-次日 01:00", text);
}
```

Add a preview assertion expecting `23:00-次日 01:00 夜间值班`. Add a date-converter assertion that an overnight todo with `Date = 2026-07-23` appears for July 23 and not July 24. Add a service sorting assertion that overnight todos remain ordered by their start time among incomplete timed todos.

- [ ] **Step 2: Run display and attribution tests and verify RED**

Run:

```powershell
dotnet test tests\QingJian.App.Tests\QingJian.App.Tests.csproj -c Release --filter "FullyQualifiedName~TodoTimeDisplayConverterTests|FullyQualifiedName~TodoPreviewDisplayConverterTests|FullyQualifiedName~TodoItemsForDateConverterTests|FullyQualifiedName~TodoServiceTests"
```

Expected: the new display assertions fail because the converter currently renders `23:00-01:00`; attribution and sorting contract assertions may already pass.

- [ ] **Step 3: Label overnight end times**

In `TodoTimeDisplayConverter.Convert`, format the range with:

```csharp
var end = todo.EndTime.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
return TodoTimeRangeRules.IsOvernight(todo.StartTime.Value, todo.EndTime.Value)
    ? $"{start}-次日 {end}"
    : $"{start}-{end}";
```

- [ ] **Step 4: Run focused tests and verify GREEN**

Run the Task 3 focused command again.

Expected: all focused tests pass; same-day, single-time, and no-time expectations remain unchanged.

- [ ] **Step 5: Commit Task 3**

```powershell
git add src\QingJian.App\TodoWidgets\TodoTimeDisplayConverter.cs tests\QingJian.App.Tests\TodoWidgets\TodoTimeDisplayConverterTests.cs tests\QingJian.App.Tests\TodoWidgets\TodoPreviewDisplayConverterTests.cs tests\QingJian.App.Tests\TodoWidgets\TodoItemsForDateConverterTests.cs tests\QingJian.App.Tests\Services\TodoServiceTests.cs
git commit -m "feat: label overnight todo times"
```

### Task 4: Full verification

**Files:**
- Verify: all files changed by Tasks 1-3

**Interfaces:**
- Consumes: completed overnight todo implementation
- Produces: verified, clean `feature/ui-polish` branch

- [ ] **Step 1: Run all tests**

```powershell
dotnet test -c Release
```

Expected: zero failed and zero skipped tests.

- [ ] **Step 2: Build the solution**

```powershell
dotnet build -c Release --no-restore
```

Expected: zero warnings and zero errors.

- [ ] **Step 3: Check diff and branch isolation**

```powershell
git diff --check develop..HEAD
git status --short --branch
git rev-parse develop
git -C C:\Users\Cristin\Desktop\VibeCoding\qingjian status --short --branch
```

Expected: no whitespace errors, clean `feature/ui-polish`, and unchanged clean `develop` at its existing commit.
