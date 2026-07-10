# QingJian Markdown Editor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the plain note body editor with a WebView2-hosted Toast UI Markdown editor that defaults to WYSIWYG mode, preserves Markdown source editing, and continues using the existing local auto-save flow.

**Architecture:** WPF remains the application shell and source of truth for note selection and persistence. A small editor bridge normalizes WebView2 messages and selection-change state, while local HTML/CSS/JS assets host Toast UI Editor inside the right-pane body area. `MainViewModel` keeps owning note content and auto-save; the editor host only syncs Markdown strings in and out.

**Tech Stack:** WPF, .NET 8, SQLite/EF Core, `Microsoft.Web.WebView2`, Toast UI Editor static assets, xUnit.

## Global Constraints

1. The Windows app remains a WPF app on .NET 8.
2. Use `Microsoft.Web.WebView2` for hosting the editor inside WPF.
3. Use Toast UI Editor for the first complete Markdown editor implementation.
4. Bundle local static editor assets with the application instead of relying on a CDN at runtime.
5. Save note content as Markdown text in the existing `Notes.Content` field.
6. Default the editor to WYSIWYG mode.
7. Allow users to switch between WYSIWYG mode and Markdown source mode.
8. Support network image Markdown links; do not implement local image paste, drag-and-drop image import, or attachment storage.
9. Keep the existing note title behavior unchanged.
10. Do not change the database schema.
11. Run `dotnet test` and `dotnet build` sequentially, not in parallel.

---

## File Structure

Create:

1. `src/QingJian.App/Editor/EditorMessage.cs`  
   Parses and represents messages sent from the WebView2 editor page to WPF.
2. `src/QingJian.App/Editor/MarkdownEditorState.cs`  
   Tracks selected note identity, current Markdown, and whether editor loads should suppress change echo.
3. `src/QingJian.App/EditorAssets/index.html`  
   Local WebView2 entry page.
4. `src/QingJian.App/EditorAssets/editor-host.js`  
   Initializes Toast UI Editor and posts Markdown changes to WPF.
5. `src/QingJian.App/EditorAssets/editor-host.css`  
   Light styling to make Toast UI fit QingJian's editor area.
6. `src/QingJian.App/EditorAssets/vendor/`  
   Local Toast UI Editor runtime files copied from the npm package.
7. `tests/QingJian.App.Tests/Editor/EditorMessageTests.cs`  
   Unit tests for message parsing.
8. `tests/QingJian.App.Tests/Editor/MarkdownEditorStateTests.cs`  
   Unit tests for selection-load and edit-echo state.

Modify:

1. `src/QingJian.App/QingJian.App.csproj`  
   Add WebView2 dependency and copy editor assets to output.
2. `src/QingJian.App/Views/MainWindow.xaml`  
   Replace body `TextBox` with a WebView2 host and fallback message.
3. `src/QingJian.App/Views/MainWindow.xaml.cs`  
   Initialize WebView2, load local assets, sync selected notes into the editor, receive Markdown change messages, and save latest editor content on close.
4. `src/QingJian.App/Resources/Styles.xaml`  
   Add a small fallback text style only if needed.
5. `README.md`  
   Document WebView2 runtime requirement and Markdown editor behavior.

---

### Task 1: Add Testable Editor Message Parsing

**Files:**
- Create: `src/QingJian.App/Editor/EditorMessage.cs`
- Create: `tests/QingJian.App.Tests/Editor/EditorMessageTests.cs`

**Interfaces:**
- Produces: `EditorMessage.TryParse(string? json, out EditorMessage message)`
- Produces: `EditorMessage.Type: string`
- Produces: `EditorMessage.Markdown: string`
- Consumes: `System.Text.Json`

- [ ] **Step 1: Write the failing tests**

Create `tests/QingJian.App.Tests/Editor/EditorMessageTests.cs`:

```csharp
using QingJian.App.Editor;
using Xunit;

namespace QingJian.App.Tests.Editor;

public sealed class EditorMessageTests
{
    [Fact]
    public void TryParse_ReturnsMarkdownChangedMessage()
    {
        var parsed = EditorMessage.TryParse(
            """{"type":"markdownChanged","markdown":"# Title\n\nBody"}""",
            out var message);

        Assert.True(parsed);
        Assert.Equal("markdownChanged", message.Type);
        Assert.Equal("# Title\n\nBody", message.Markdown);
    }

    [Fact]
    public void TryParse_AllowsEmptyMarkdown()
    {
        var parsed = EditorMessage.TryParse(
            """{"type":"markdownChanged","markdown":""}""",
            out var message);

        Assert.True(parsed);
        Assert.Equal("markdownChanged", message.Type);
        Assert.Equal(string.Empty, message.Markdown);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("""{"markdown":"Body"}""")]
    [InlineData("""{"type":"unknown","markdown":"Body"}""")]
    public void TryParse_RejectsMalformedOrUnsupportedMessages(string? json)
    {
        var parsed = EditorMessage.TryParse(json, out var message);

        Assert.False(parsed);
        Assert.Equal(EditorMessage.Empty, message);
    }
}
```

- [ ] **Step 2: Run the focused tests and verify they fail**

Run:

```powershell
dotnet test --filter FullyQualifiedName~EditorMessageTests
```

Expected: fail because `QingJian.App.Editor.EditorMessage` does not exist.

- [ ] **Step 3: Implement minimal parsing**

Create `src/QingJian.App/Editor/EditorMessage.cs`:

```csharp
using System.Text.Json;

namespace QingJian.App.Editor;

public readonly record struct EditorMessage(string Type, string Markdown)
{
    public const string MarkdownChangedType = "markdownChanged";

    public static EditorMessage Empty { get; } = new(string.Empty, string.Empty);

    public static bool TryParse(string? json, out EditorMessage message)
    {
        message = Empty;

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!root.TryGetProperty("type", out var typeElement) ||
                typeElement.GetString() is not { } type ||
                type != MarkdownChangedType)
            {
                return false;
            }

            var markdown = root.TryGetProperty("markdown", out var markdownElement)
                ? markdownElement.GetString() ?? string.Empty
                : string.Empty;

            message = new EditorMessage(type, markdown);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
```

- [ ] **Step 4: Run the focused tests and verify they pass**

Run:

```powershell
dotnet test --filter FullyQualifiedName~EditorMessageTests
```

Expected: pass.

- [ ] **Step 5: Commit**

Run:

```powershell
git add src\QingJian.App\Editor\EditorMessage.cs tests\QingJian.App.Tests\Editor\EditorMessageTests.cs
git commit -m "test: cover markdown editor messages"
```

---

### Task 2: Add Editor State Guard for Selection Loads

**Files:**
- Create: `src/QingJian.App/Editor/MarkdownEditorState.cs`
- Create: `tests/QingJian.App.Tests/Editor/MarkdownEditorStateTests.cs`

**Interfaces:**
- Produces: `MarkdownEditorState.CurrentNoteId: string?`
- Produces: `MarkdownEditorState.CurrentMarkdown: string`
- Produces: `MarkdownEditorState.BeginLoad(string? noteId, string? markdown): string`
- Produces: `MarkdownEditorState.EndLoad(): void`
- Produces: `MarkdownEditorState.TryApplyEditorMarkdown(string markdown, out string normalizedMarkdown): bool`

- [ ] **Step 1: Write the failing tests**

Create `tests/QingJian.App.Tests/Editor/MarkdownEditorStateTests.cs`:

```csharp
using QingJian.App.Editor;
using Xunit;

namespace QingJian.App.Tests.Editor;

public sealed class MarkdownEditorStateTests
{
    [Fact]
    public void BeginLoad_StoresSelectedNoteAndNormalizesNullMarkdown()
    {
        var state = new MarkdownEditorState();

        var markdown = state.BeginLoad("note-1", null);

        Assert.Equal("note-1", state.CurrentNoteId);
        Assert.Equal(string.Empty, markdown);
        Assert.Equal(string.Empty, state.CurrentMarkdown);
    }

    [Fact]
    public void TryApplyEditorMarkdown_IgnoresEchoWhileLoading()
    {
        var state = new MarkdownEditorState();
        state.BeginLoad("note-1", "Original");

        var applied = state.TryApplyEditorMarkdown("Echo", out var markdown);

        Assert.False(applied);
        Assert.Equal("Original", state.CurrentMarkdown);
        Assert.Equal("Original", markdown);
    }

    [Fact]
    public void TryApplyEditorMarkdown_AppliesChangeAfterLoadEnds()
    {
        var state = new MarkdownEditorState();
        state.BeginLoad("note-1", "Original");
        state.EndLoad();

        var applied = state.TryApplyEditorMarkdown("Changed", out var markdown);

        Assert.True(applied);
        Assert.Equal("Changed", markdown);
        Assert.Equal("Changed", state.CurrentMarkdown);
    }

    [Fact]
    public void TryApplyEditorMarkdown_DoesNotApplyUnchangedContent()
    {
        var state = new MarkdownEditorState();
        state.BeginLoad("note-1", "Same");
        state.EndLoad();

        var applied = state.TryApplyEditorMarkdown("Same", out var markdown);

        Assert.False(applied);
        Assert.Equal("Same", markdown);
    }
}
```

- [ ] **Step 2: Run the focused tests and verify they fail**

Run:

```powershell
dotnet test --filter FullyQualifiedName~MarkdownEditorStateTests
```

Expected: fail because `MarkdownEditorState` does not exist.

- [ ] **Step 3: Implement the state guard**

Create `src/QingJian.App/Editor/MarkdownEditorState.cs`:

```csharp
namespace QingJian.App.Editor;

public sealed class MarkdownEditorState
{
    private bool _isLoadingFromSelection;

    public string? CurrentNoteId { get; private set; }

    public string CurrentMarkdown { get; private set; } = string.Empty;

    public string BeginLoad(string? noteId, string? markdown)
    {
        _isLoadingFromSelection = true;
        CurrentNoteId = noteId;
        CurrentMarkdown = markdown ?? string.Empty;
        return CurrentMarkdown;
    }

    public void EndLoad()
    {
        _isLoadingFromSelection = false;
    }

    public bool TryApplyEditorMarkdown(string markdown, out string normalizedMarkdown)
    {
        normalizedMarkdown = markdown ?? string.Empty;

        if (_isLoadingFromSelection)
        {
            normalizedMarkdown = CurrentMarkdown;
            return false;
        }

        if (CurrentMarkdown == normalizedMarkdown)
        {
            return false;
        }

        CurrentMarkdown = normalizedMarkdown;
        return true;
    }
}
```

- [ ] **Step 4: Run the focused tests and verify they pass**

Run:

```powershell
dotnet test --filter FullyQualifiedName~MarkdownEditorStateTests
```

Expected: pass.

- [ ] **Step 5: Commit**

Run:

```powershell
git add src\QingJian.App\Editor\MarkdownEditorState.cs tests\QingJian.App.Tests\Editor\MarkdownEditorStateTests.cs
git commit -m "test: cover markdown editor state"
```

---

### Task 3: Add WebView2 Package and Local Editor Asset Packaging

**Files:**
- Modify: `src/QingJian.App/QingJian.App.csproj`
- Create: `src/QingJian.App/EditorAssets/index.html`
- Create: `src/QingJian.App/EditorAssets/editor-host.css`
- Create: `src/QingJian.App/EditorAssets/editor-host.js`
- Create: `src/QingJian.App/EditorAssets/vendor/README.md`

**Interfaces:**
- Consumes: WPF project file.
- Produces: WebView2 package reference.
- Produces: editor assets copied to output under `EditorAssets`.

- [ ] **Step 1: Add WebView2 package**

Run:

```powershell
dotnet add src\QingJian.App\QingJian.App.csproj package Microsoft.Web.WebView2
```

Expected: `src\QingJian.App\QingJian.App.csproj` gains a `PackageReference` for `Microsoft.Web.WebView2`.

- [ ] **Step 2: Add asset copy configuration**

Modify `src/QingJian.App/QingJian.App.csproj` so it includes this item group:

```xml
  <ItemGroup>
    <Content Include="EditorAssets\**\*">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
  </ItemGroup>
```

Keep the existing EF Core SQLite package reference.

- [ ] **Step 3: Add local editor page**

Create `src/QingJian.App/EditorAssets/index.html`:

```html
<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8">
  <meta http-equiv="X-UA-Compatible" content="IE=edge">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>QingJian Markdown Editor</title>
  <link rel="stylesheet" href="./vendor/toastui-editor.min.css">
  <link rel="stylesheet" href="./editor-host.css">
</head>
<body>
  <main id="editor"></main>
  <script src="./vendor/toastui-editor-all.min.js"></script>
  <script src="./editor-host.js"></script>
</body>
</html>
```

- [ ] **Step 4: Add host CSS**

Create `src/QingJian.App/EditorAssets/editor-host.css`:

```css
html,
body {
  width: 100%;
  height: 100%;
  margin: 0;
  overflow: hidden;
  background: #f5f3ef;
  color: #25211d;
  font-family: "Microsoft YaHei UI", "Segoe UI", sans-serif;
}

#editor {
  width: 100%;
  height: 100%;
}

.toastui-editor-defaultUI {
  border: 0;
  font-family: "Microsoft YaHei UI", "Segoe UI", sans-serif;
}

.toastui-editor-defaultUI-toolbar {
  border-radius: 0;
}
```

- [ ] **Step 5: Add temporary host JavaScript with WebView2 contract**

Create `src/QingJian.App/EditorAssets/editor-host.js`:

```javascript
(function () {
  let editor = null;
  let isSettingMarkdown = false;

  function postMarkdownChanged() {
    if (isSettingMarkdown || !editor || !window.chrome || !window.chrome.webview) {
      return;
    }

    window.chrome.webview.postMessage({
      type: "markdownChanged",
      markdown: editor.getMarkdown()
    });
  }

  window.qingjianEditor = {
    initialize: function () {
      if (editor || !window.toastui || !window.toastui.Editor) {
        return false;
      }

      editor = new window.toastui.Editor({
        el: document.querySelector("#editor"),
        height: "100%",
        initialEditType: "wysiwyg",
        previewStyle: "vertical",
        usageStatistics: false,
        initialValue: ""
      });

      editor.on("change", postMarkdownChanged);
      return true;
    },

    setMarkdown: function (markdown) {
      if (!editor) {
        return false;
      }

      isSettingMarkdown = true;
      editor.setMarkdown(markdown || "", false);
      isSettingMarkdown = false;
      return true;
    },

    getMarkdown: function () {
      return editor ? editor.getMarkdown() : "";
    }
  };

  window.addEventListener("DOMContentLoaded", function () {
    window.qingjianEditor.initialize();
  });
})();
```

- [ ] **Step 6: Add vendor README before copying real Toast UI files**

Create `src/QingJian.App/EditorAssets/vendor/README.md`:

```markdown
# Editor Vendor Assets

This folder contains local Toast UI Editor runtime assets used by the WebView2 editor.

Required files:

- `toastui-editor-all.min.js`
- `toastui-editor.min.css`

Copy these files from the Toast UI Editor npm package before running the app.
Runtime editing must not depend on a CDN.
```

- [ ] **Step 7: Build and verify packaging configuration**

Run:

```powershell
dotnet build
```

Expected: build succeeds. The app may not run the editor correctly until Task 4 copies the real vendor files.

- [ ] **Step 8: Commit**

Run:

```powershell
git add src\QingJian.App\QingJian.App.csproj src\QingJian.App\EditorAssets
git commit -m "feat: add markdown editor assets"
```

---

### Task 4: Vendor Toast UI Editor Static Assets

**Files:**
- Modify: `src/QingJian.App/EditorAssets/vendor/toastui-editor-all.min.js`
- Modify: `src/QingJian.App/EditorAssets/vendor/toastui-editor.min.css`
- Modify: `src/QingJian.App/EditorAssets/vendor/README.md`

**Interfaces:**
- Produces: `window.toastui.Editor` for `editor-host.js`.
- Consumes: npm package `@toast-ui/editor`.

- [ ] **Step 1: Download Toast UI Editor package into a temporary folder**

Run:

```powershell
$temp = Join-Path $env:TEMP "qingjian-toastui-editor"
Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $temp | Out-Null
Push-Location $temp
npm pack @toast-ui/editor
Pop-Location
```

Expected: a file like `$env:TEMP\qingjian-toastui-editor\toast-ui-editor-*.tgz` exists.

- [ ] **Step 2: Extract the npm package**

Run:

```powershell
$temp = Join-Path $env:TEMP "qingjian-toastui-editor"
$package = Get-ChildItem -LiteralPath $temp -Filter "*.tgz" | Select-Object -First 1
tar -xzf $package.FullName -C $temp
```

Expected: `$env:TEMP\qingjian-toastui-editor\package\dist` exists.

- [ ] **Step 3: Copy only required runtime files**

Run:

```powershell
$temp = Join-Path $env:TEMP "qingjian-toastui-editor"
$vendor = "C:\Users\Cristin\Desktop\VibeCoding\qingjian\src\QingJian.App\EditorAssets\vendor"
Copy-Item -LiteralPath (Join-Path $temp "package\dist\toastui-editor-all.min.js") -Destination (Join-Path $vendor "toastui-editor-all.min.js") -Force
Copy-Item -LiteralPath (Join-Path $temp "package\dist\toastui-editor.min.css") -Destination (Join-Path $vendor "toastui-editor.min.css") -Force
```

Expected: the two vendor files exist in `src\QingJian.App\EditorAssets\vendor`.

- [ ] **Step 4: Update vendor README with source note**

Modify `src/QingJian.App/EditorAssets/vendor/README.md`:

```markdown
# Editor Vendor Assets

This folder contains local Toast UI Editor runtime assets used by the WebView2 editor.

Runtime files:

- `toastui-editor-all.min.js`
- `toastui-editor.min.css`

Source package:

- npm: `@toast-ui/editor`

Runtime editing must not depend on a CDN.
```

- [ ] **Step 5: Confirm the expected JavaScript symbol is present**

Run:

```powershell
Select-String -Path src\QingJian.App\EditorAssets\vendor\toastui-editor-all.min.js -Pattern "toastui" -Quiet
```

Expected: output is `True`.

- [ ] **Step 6: Build**

Run:

```powershell
dotnet build
```

Expected: build succeeds and editor assets are copied to `src\QingJian.App\bin`.

- [ ] **Step 7: Commit**

Run:

```powershell
git add src\QingJian.App\EditorAssets\vendor
git commit -m "chore: vendor toast ui editor assets"
```

---

### Task 5: Replace Body TextBox with WebView2 Host

**Files:**
- Modify: `src/QingJian.App/Views/MainWindow.xaml`
- Modify: `src/QingJian.App/Views/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `MarkdownEditorState.BeginLoad`, `MarkdownEditorState.EndLoad`, `MarkdownEditorState.TryApplyEditorMarkdown`
- Consumes: `EditorMessage.TryParse`
- Produces: body editor hosted in `MarkdownWebView`

- [ ] **Step 1: Modify XAML to declare WebView2 namespace**

Update the opening `<Window>` in `src/QingJian.App/Views/MainWindow.xaml` to include:

```xml
xmlns:wv2="clr-namespace:Microsoft.Web.WebView2.Wpf;assembly=Microsoft.Web.WebView2.Wpf"
```

- [ ] **Step 2: Replace the body TextBox**

Replace the current `Grid.Row="2"` body `TextBox`:

```xml
<TextBox Grid.Row="2"
         Text="{Binding SelectedNote.Content, UpdateSourceTrigger=PropertyChanged}"
         FontSize="15" />
```

with:

```xml
<Grid Grid.Row="2">
    <wv2:WebView2 x:Name="MarkdownWebView" />
    <Border x:Name="MarkdownEditorFallback"
            Visibility="Collapsed"
            Background="{StaticResource AppBackgroundBrush}"
            BorderBrush="{StaticResource BorderBrush}"
            BorderThickness="1"
            Padding="16">
        <TextBlock Text="Markdown 编辑器暂时不可用，但标题和便签列表仍可使用。"
                   Foreground="{StaticResource MutedTextBrush}"
                   TextWrapping="Wrap" />
    </Border>
</Grid>
```

- [ ] **Step 3: Add using statements in code-behind**

Add these to `src/QingJian.App/Views/MainWindow.xaml.cs`:

```csharp
using System.IO;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using QingJian.App.Editor;
```

- [ ] **Step 4: Add editor fields to `MainWindow`**

Inside `MainWindow`, add:

```csharp
private readonly MarkdownEditorState _editorState = new();
private bool _isEditorReady;
private string? _pendingEditorMarkdown;
```

- [ ] **Step 5: Initialize WebView2 from `OnLoaded`**

Change `OnLoaded` to:

```csharp
private async void OnLoaded(object sender, RoutedEventArgs e)
{
    await InitializeMarkdownEditorAsync();
    await _viewModel.LoadAsync();
    UpdateTitlePlaceholderState();
    await LoadSelectedNoteIntoEditorAsync();
}
```

- [ ] **Step 6: Add WebView2 initialization method**

Add this method to `MainWindow.xaml.cs`:

```csharp
private async Task InitializeMarkdownEditorAsync()
{
    try
    {
        MarkdownWebView.WebMessageReceived += MarkdownWebView_OnWebMessageReceived;
        await MarkdownWebView.EnsureCoreWebView2Async();
        MarkdownWebView.CoreWebView2.NavigationCompleted += MarkdownWebView_OnNavigationCompleted;

        var editorPath = Path.Combine(AppContext.BaseDirectory, "EditorAssets", "index.html");
        MarkdownWebView.Source = new Uri(editorPath);
    }
    catch (Exception)
    {
        MarkdownEditorFallback.Visibility = Visibility.Visible;
        MarkdownWebView.Visibility = Visibility.Collapsed;
    }
}
```

- [ ] **Step 7: Add navigation completion handler**

Add this method:

```csharp
private async void MarkdownWebView_OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
{
    if (!e.IsSuccess)
    {
        MarkdownEditorFallback.Visibility = Visibility.Visible;
        MarkdownWebView.Visibility = Visibility.Collapsed;
        return;
    }

    _isEditorReady = true;

    if (_pendingEditorMarkdown is not null)
    {
        await SetEditorMarkdownAsync(_pendingEditorMarkdown);
        _pendingEditorMarkdown = null;
    }
}
```

- [ ] **Step 8: Load selected notes into the editor when selection changes**

In the existing `_viewModel.PropertyChanged` handler, change the selected-note branch to:

```csharp
if (args.PropertyName == nameof(MainViewModel.SelectedNote))
{
    UpdateTitlePlaceholderState();
    _ = LoadSelectedNoteIntoEditorAsync();
}
```

Add this method:

```csharp
private async Task LoadSelectedNoteIntoEditorAsync()
{
    var markdown = _editorState.BeginLoad(_viewModel.SelectedNote?.Id, _viewModel.SelectedNote?.Content);

    if (!_isEditorReady)
    {
        _pendingEditorMarkdown = markdown;
        return;
    }

    await SetEditorMarkdownAsync(markdown);
    _editorState.EndLoad();
}
```

- [ ] **Step 9: Set editor Markdown via script**

Add this method:

```csharp
private async Task SetEditorMarkdownAsync(string markdown)
{
    if (!_isEditorReady || MarkdownWebView.CoreWebView2 is null)
    {
        _pendingEditorMarkdown = markdown;
        return;
    }

    var json = JsonSerializer.Serialize(markdown);
    await MarkdownWebView.ExecuteScriptAsync($"window.qingjianEditor.setMarkdown({json});");
    _editorState.EndLoad();
}
```

- [ ] **Step 10: Receive editor messages**

Add this method:

```csharp
private void MarkdownWebView_OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
{
    if (_viewModel.SelectedNote is null)
    {
        return;
    }

    if (!EditorMessage.TryParse(e.WebMessageAsJson, out var message))
    {
        return;
    }

    if (!_editorState.TryApplyEditorMarkdown(message.Markdown, out var markdown))
    {
        return;
    }

    _viewModel.SelectedNote.Content = markdown;
}
```

- [ ] **Step 11: Run build**

Run:

```powershell
dotnet build
```

Expected: build succeeds.

- [ ] **Step 12: Commit**

Run:

```powershell
git add src\QingJian.App\Views\MainWindow.xaml src\QingJian.App\Views\MainWindow.xaml.cs
git commit -m "feat: host markdown editor in main window"
```

---

### Task 6: Flush Latest Editor Content on Window Close

**Files:**
- Modify: `src/QingJian.App/Views/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `window.qingjianEditor.getMarkdown()`
- Produces: final editor content copied into `SelectedNote.Content` before `SaveSelectedNoteNowAsync`

- [ ] **Step 1: Add latest-content method**

Add this method to `MainWindow.xaml.cs`:

```csharp
private async Task PullLatestEditorMarkdownAsync()
{
    if (!_isEditorReady || MarkdownWebView.CoreWebView2 is null || _viewModel.SelectedNote is null)
    {
        return;
    }

    var result = await MarkdownWebView.ExecuteScriptAsync("window.qingjianEditor.getMarkdown();");
    var markdown = JsonSerializer.Deserialize<string>(result) ?? string.Empty;

    if (_editorState.TryApplyEditorMarkdown(markdown, out var normalizedMarkdown))
    {
        _viewModel.SelectedNote.Content = normalizedMarkdown;
    }
}
```

- [ ] **Step 2: Call it before final save**

Change `OnClosing` to:

```csharp
private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
{
    await PullLatestEditorMarkdownAsync();
    await _viewModel.SaveSelectedNoteNowAsync();
}
```

- [ ] **Step 3: Build**

Run:

```powershell
dotnet build
```

Expected: build succeeds.

- [ ] **Step 4: Commit**

Run:

```powershell
git add src\QingJian.App\Views\MainWindow.xaml.cs
git commit -m "fix: save latest markdown on close"
```

---

### Task 7: Document Markdown Editor Behavior

**Files:**
- Modify: `README.md`

**Interfaces:**
- Produces: user-facing development notes for WebView2 and Markdown.

- [ ] **Step 1: Update README MVP/features section**

In `README.md`, add this section after `## MVP`:

```markdown
## Markdown Editing

- Note bodies are stored as Markdown text.
- The editor opens in WYSIWYG mode by default.
- Markdown source mode remains available inside the editor.
- Network image Markdown links are supported.
- Local image attachments are not part of the first Markdown editor version.

## Runtime Requirements

- Windows with the Microsoft Edge WebView2 Runtime installed.
- .NET 8 SDK for development.
```

- [ ] **Step 2: Run tests**

Run:

```powershell
dotnet test
```

Expected: all tests pass.

- [ ] **Step 3: Commit**

Run:

```powershell
git add README.md
git commit -m "docs: describe markdown editor behavior"
```

---

### Task 8: Final Verification and Manual Smoke Test

**Files:**
- No required source edits unless verification exposes a defect.

**Interfaces:**
- Consumes: completed Tasks 1-7.
- Produces: verified feature branch ready for review or merge.

- [ ] **Step 1: Run automated tests**

Run:

```powershell
dotnet test
```

Expected: all tests pass.

- [ ] **Step 2: Run build after tests**

Run:

```powershell
dotnet build
```

Expected: build succeeds with 0 errors.

- [ ] **Step 3: Run the app**

Run:

```powershell
dotnet run --project src\QingJian.App\QingJian.App.csproj
```

Expected: QingJian opens to the notes interface.

- [ ] **Step 4: Manually verify basic Markdown editing**

In the running app:

1. Create a new note.
2. Type this content in WYSIWYG mode:

```markdown
# 今日记录

- [ ] 写 Markdown 编辑器
- [x] 保留源码模式

> 这是一段引用

| 项目 | 状态 |
| --- | --- |
| WebView2 | 已接入 |

![网络图片](https://example.com/image.png)
```

3. Switch to Markdown source mode.
4. Confirm the Markdown source is still present.
5. Close the app.
6. Reopen the app with the same `dotnet run` command.
7. Confirm the note content is restored.

- [ ] **Step 5: Commit fixes only if manual verification required source changes**

If no source changes were needed, do not create an empty commit.

If fixes were needed, run `git status --short`, stage only the exact source files changed by the fix, then commit:

```powershell
git commit -m "fix: stabilize markdown editor"
```

---

## Self-Review

Spec coverage:

1. WebView2-hosted body editor: Task 3 and Task 5.
2. Toast UI Editor: Task 3 and Task 4.
3. Default WYSIWYG mode: Task 3 `initialEditType: "wysiwyg"`.
4. Markdown source mode: Task 4 uses Toast UI Editor bundled editor, and Task 8 manually verifies mode switching.
5. Save Markdown text in `Notes.Content`: Task 5 and Task 6.
6. Existing title behavior unchanged: Task 5 only replaces the body editor.
7. Network images only: Task 3/4 support Markdown images; no local attachment tasks exist.
8. No schema changes: no data files are modified.
9. Error handling for WebView2 failure: Task 5 fallback UI.
10. Tests: Tasks 1, 2, 7, and 8.

Placeholder scan:

1. No task contains unfinished-marker text or unspecified implementation placeholders.
2. The only conditional step is Task 8's optional defect-fix commit after manual verification.

Type consistency:

1. `EditorMessage.TryParse` is defined in Task 1 and consumed in Task 5.
2. `MarkdownEditorState` methods are defined in Task 2 and consumed in Task 5 and Task 6.
3. JavaScript methods `setMarkdown` and `getMarkdown` are defined in Task 3 and consumed in Task 5 and Task 6.
