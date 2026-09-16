# Documentation / 文档

This directory keeps contributor-facing documentation separate from application code.

本目录集中存放面向贡献者的文档，与应用代码分离。

## Start here / 从这里开始

- [Project status](project-status.md) — current architecture, completed behavior, deferred work, validation baseline, and handoff notes.
- [Contributing guide](../CONTRIBUTING.md) — local setup, branch workflow, pull requests, and coding conventions.
- [Changelog](../CHANGELOG.md) — user-visible release history.

## Design history / 设计记录

`superpowers/specs/` contains feature specifications and `superpowers/plans/` contains implementation plans. These files are historical design records; new feature work should update the status document when it is merged.

`superpowers/specs/` 保存功能规格，`superpowers/plans/` 保存实现计划。这些文件是历史设计记录；新功能合并后请同步更新项目状态文档。

## Architecture at a glance / 架构速览

```text
WPF views → ViewModels → Services → Repositories → SQLite
                 │            │
                 ├── EditorAssets (WebView2 + Toast UI Editor)
                 ├── TodoWidgets (desktop widget)
                 └── Settings / Hotkeys / Tray (Windows integration)
```

Application code lives in `src/QingJian.App/`; matching behavior tests live in `tests/QingJian.App.Tests/`.

应用代码位于 `src/QingJian.App/`，对应的行为测试位于 `tests/QingJian.App.Tests/`。
