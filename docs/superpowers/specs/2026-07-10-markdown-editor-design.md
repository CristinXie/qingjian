# QingJian Markdown Editor Design

## 1. Product Goal

QingJian needs richer note writing while keeping the app lightweight and local-first. The Markdown editor feature upgrades the note body from plain text to a full Markdown editing experience, so users can write structured notes, checklists, code snippets, links, tables, and quoted content inside the existing notes interface.

The editor should feel like a normal note editor first. Markdown is the storage and power-user format, not a barrier before users can write.

## 2. Scope

This feature includes:

1. Replacing the current plain body `TextBox` with a WebView2-hosted Markdown editor.
2. Using a mature JavaScript Markdown editor, with Toast UI Editor as the first implementation choice.
3. Defaulting the editor to WYSIWYG mode.
4. Allowing users to switch between WYSIWYG mode and Markdown source mode.
5. Saving note content as Markdown text in the existing `Notes.Content` field.
6. Syncing editor changes back to the current WPF ViewModel so existing auto-save behavior still works.
7. Loading the selected note's Markdown content into the editor when selection changes.
8. Supporting common Markdown features: headings, bold, italic, strikethrough, lists, task lists, blockquotes, code, code blocks, links, network images, tables, and horizontal rules.
9. Keeping the existing note title behavior unchanged.

## 3. Non-Goals

This feature does not include:

1. Local image paste, drag-and-drop image import, or attachment storage.
2. Cloud image upload.
3. Syncing notes across devices.
4. Markdown export or import.
5. Changing the database schema.
6. Making note titles Markdown-enabled.
7. Rewriting the full app shell in web technology.
8. Building a custom Markdown editor from scratch.
9. Desktop transparent todo or schedule widgets.
10. Global hotkeys or quick note popup windows.

## 4. Technology Stack

The Windows app remains a WPF app on .NET 8.

Additional editor technology:

1. `Microsoft.Web.WebView2` for hosting the editor inside WPF.
2. Toast UI Editor for the first complete Markdown editor implementation.
3. Local static editor assets bundled with the application instead of relying on a CDN at runtime.
4. WebView2 host-object or message passing for synchronization between JavaScript and WPF.

Toast UI Editor is chosen because it provides a ready WYSIWYG plus Markdown source editing experience with broad Markdown support. Milkdown remains a future alternative if the product later needs deeper Typora-style customization.

## 5. UI Behavior

The main window keeps the existing two-pane layout:

1. Left pane: note list and new-note button.
2. Right pane: delete action, title editor, and Markdown body editor.

The body editor behavior:

1. The editor opens in WYSIWYG mode by default.
2. The editor exposes a visible way to switch between WYSIWYG mode and Markdown source mode.
3. The editor uses the selected note's current Markdown content as its initial value.
4. When no note is selected, the editor area follows the existing empty state behavior.
5. Existing title placeholder behavior remains unchanged.
6. The app should still open directly to the notes interface; there is no landing or instructional screen.

The first version can use the editor's built-in toolbar and mode switcher if they fit the app visually. Styling can be refined after functionality is stable.

## 6. Data Flow

The database continues to store Markdown in `Notes.Content`.

Selection flow:

1. User selects a note in the WPF note list.
2. `MainViewModel.SelectedNote` changes.
3. The WPF view sends the selected note's `Content` value into the WebView2 editor.
4. The editor updates its document without treating the load operation as a user edit.

Edit flow:

1. User edits the body in WYSIWYG or Markdown source mode.
2. The JavaScript editor emits a content-changed event.
3. WebView2 sends the current Markdown string to WPF.
4. WPF updates `SelectedNote.Content`.
5. The existing ViewModel auto-save debounce saves the selected note.

Close flow:

1. Before the window closes, WPF asks the editor for the latest Markdown content if needed.
2. WPF updates the selected note.
3. The existing final save behavior persists the selected note.

The implementation must avoid feedback loops where loading a selected note into the editor immediately triggers an unnecessary save.

## 7. Asset Strategy

Editor web assets should live inside the application project, for example:

```text
src/QingJian.App/EditorAssets/
  index.html
  editor-host.js
  editor-host.css
  vendor/
```

Runtime should load the local `index.html` from packaged application assets. The app should not require internet access to open or edit notes.

Network images inside Markdown are allowed because the user explicitly inserts external image URLs. If a network image cannot load, the note content should still remain intact as Markdown.

## 8. Error Handling

Expected handling:

1. If WebView2 initialization fails, show a clear editor-unavailable message instead of crashing the app.
2. If the editor web page fails to load, keep the note list and title editor usable.
3. If JavaScript sends malformed or empty messages, ignore them safely.
4. If content sync fails during editing, preserve the last known WPF `SelectedNote.Content`.
5. If a note is switched quickly while the editor is still loading, the latest selected note wins.

The first version does not need a full recovery UI. It does need to avoid data loss and unhandled crashes.

## 9. Testing Strategy

Automated tests should focus on the WPF-side behavior that can be tested reliably:

1. Existing service and repository tests should continue to pass without schema changes.
2. ViewModel tests should continue to verify content changes trigger auto-save.
3. Any new editor bridge class should be unit tested for message parsing and selection-change behavior.
4. Tests should cover blank Markdown content, normal Markdown content, and quick selected-note changes if bridge logic is extracted.

Manual verification should cover:

1. App launches successfully.
2. Existing notes load.
3. New notes can be created.
4. Markdown can be edited in WYSIWYG mode.
5. Markdown source mode can be opened and edited.
6. Switching notes loads the correct content.
7. Closing and reopening the app restores formatted Markdown content.
8. Common Markdown features render correctly.

## 10. Acceptance Criteria

This feature is accepted when:

1. The app runs as a WPF Windows app.
2. The note body editor is WebView2-hosted.
3. The editor defaults to WYSIWYG mode.
4. The user can switch to Markdown source mode.
5. The saved note content remains Markdown text.
6. Existing notes with plain text still open and save correctly.
7. Common Markdown syntax works in the editor.
8. Network image Markdown is supported.
9. Local image paste or attachment storage is not required.
10. Existing auto-save still persists body changes.
11. Existing title behavior remains unchanged.
12. Automated tests pass.

## 11. Implementation Notes

Implementation should happen on a feature branch and stay incremental:

1. Add WebView2 package and local editor assets.
2. Introduce a small WPF editor host control or view-level bridge.
3. Wire selected-note loading into the editor.
4. Wire editor change messages back to `SelectedNote.Content`.
5. Keep auto-save in `MainViewModel`; do not duplicate persistence logic in the editor host.
6. Add tests around bridgeable logic where practical.
7. Run build and tests before merging.
