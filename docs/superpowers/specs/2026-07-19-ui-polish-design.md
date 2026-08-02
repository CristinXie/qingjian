# QingJian UI Polish Design

## 1. Goal

Improve the main notes window so it feels calmer, clearer, and more comfortable for long writing sessions, without changing the product model or feature set.

The intended tone is quiet office software:

- low-distraction
- clear hierarchy
- restrained controls
- stable spacing
- no decorative redesign

## 2. Scope

This polish pass includes:

1. Refining the main window layout and spacing.
2. Improving note list readability and selected-state clarity.
3. Making the main actions feel more deliberate.
4. Improving the empty state presentation.
5. Tightening the title/editor relationship on the right side.
6. Adding note update time visibility in the note list.
7. Harmonizing WPF chrome with the hosted Markdown editor.
8. Preserving and visually integrating the existing desktop todo toggle in the main window.

## 3. Non-Goals

This polish pass does not include:

1. New product features.
2. Quick-note redesign.
3. New navigation or tabs.
4. Search, tags, archive, or filters.
5. Theme switching or dark mode.
6. Changes to storage, autosave, or note data model.
7. Changes to Markdown editor capabilities.
8. Tray behavior or hotkey behavior.
9. Changes to desktop todo data, widget interaction, persistence, or visibility behavior.

## 4. Design Direction

The UI should read as a serious desktop writing tool.

Principles:

1. Keep the surface light and calm.
2. Make the active note easy to spot immediately.
3. Prefer clear structure over visual flourish.
4. Use spacing and contrast instead of decoration.
5. Keep controls visible but quiet.

The existing warm palette is acceptable, but the final result should avoid feeling like a stack of cards. The main window should feel more like one organized workspace.

The existing `显示/隐藏桌面待办` action remains available and keeps its current toggle behavior. This polish pass may adjust its visual weight and placement, but does not change the desktop todo widget contract.

## 5. Main Window Changes

### Left Pane

The note list should become easier to scan:

1. Stronger selected state.
2. Better spacing between items.
3. Title and content preview should not compete for attention.
4. Add updated time as secondary metadata.
5. Make the list feel like a structured index rather than a loose stack of notes.

### Right Pane

The editor area should feel more focused:

1. Align title, action row, and editor with a stable vertical rhythm.
2. Give the title field clearer visual separation from the body editor.
3. Keep the editor boundary readable without over-framing it.
4. Preserve the current empty-state behavior while improving positioning and balance.

### Actions

The new-note and delete actions should remain simple, but their visual weight should be adjusted so the primary action is easy to find and the destructive action remains understated.

## 6. Component Boundaries

This work should stay mostly inside the existing WPF UI surface:

1. `MainWindow.xaml` for layout changes.
2. `MainWindow.xaml.cs` only if small view logic changes are needed to support the new presentation.
3. `Styles.xaml` for shared brush and control adjustments.
4. `MainViewModel` only if the note list needs a small presentation-friendly property that is already derived from existing data.

The note update time should be presented as a derived view concern, not a model or storage change.

The existing desktop todo coordinator and main-window toggle handler remain the source of truth for widget visibility.

## 7. Styling Rules

1. Keep typography readable and compact.
2. Avoid large rounded, floating card treatment.
3. Use borders and spacing to separate regions.
4. Use muted metadata for secondary text.
5. Keep button styling consistent with the existing app, but tighten its visual rhythm.
6. Do not introduce a new visual language that fights the Markdown editor surface.

## 8. Validation

Validation should cover:

1. The app still opens directly to the main notes window.
2. The note list remains usable with many notes.
3. The selected note is obvious at a glance.
4. The empty state still offers a clear path to create a note.
5. The title editor and body editor still work as before.
6. The editor host still loads and remains readable.
7. Build and tests still pass.

## 9. Acceptance Criteria

This polish pass is complete when:

1. The main window feels calmer and more structured.
2. Note list items are easier to scan.
3. Updated time is visible in the note list.
4. The selected note is clearly distinguishable.
5. The empty state feels intentional rather than temporary.
6. The editor area has better visual balance.
7. No app behavior beyond presentation has changed.
8. Automated tests and build pass.
9. The existing main-window action can still show and hide the desktop todo widget.
