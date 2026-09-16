# QingJian / 青简

[![CI](https://github.com/CristinXie/qingjian/actions/workflows/ci.yml/badge.svg)](https://github.com/CristinXie/qingjian/actions/workflows/ci.yml)

QingJian is a local-first Windows notes and desktop todo app. It is built with WPF and .NET 8, and keeps notes, settings, and attachments on the local machine.

青简是一款本地优先的 Windows 笔记与桌面待办应用，基于 WPF 和 .NET 8 构建。笔记、设置和附件默认保存在本机。

## Features / 功能

- Markdown and WYSIWYG editing, autosave, search, favorites, folders, batch actions, and a 30-day recycle bin.
- Markdown 与所见即所得编辑、自动保存、搜索、收藏夹、文件夹、批量操作和 30 天回收站。
- Global quick notes with a configurable shortcut (`Ctrl + Alt + N` by default).
- 全局快速笔记，快捷键可配置（默认 `Ctrl + Alt + N`）。
- A configurable desktop todo widget with 8-day, today-list, and monthly-calendar views.
- 可配置的桌面待办小组件，支持八日、今日列表和月历视图。
- System tray, Windows startup, single-instance activation, and a self-contained x64 installer.
- 系统托盘、Windows 开机启动、单实例激活及 x64 安装包。
- Local image attachments through toolbar upload, drag-and-drop, and paste.
- 支持通过工具栏、拖放和粘贴添加本地图片附件。

## Tech stack / 技术栈

WPF · .NET 8 · SQLite · Entity Framework Core · WebView2 · Toast UI Editor · Markdig

## Quick start / 快速开始

Requirements / 环境要求：Windows、.NET 8 SDK，以及 Microsoft Edge WebView2 Runtime。

```powershell
dotnet restore
dotnet test -c Release --no-restore
dotnet build -c Release --no-restore
dotnet run --project src/QingJian.App/QingJian.App.csproj
```

WPF 的测试和构建请依次执行，避免生成文件锁定。

## Repository layout / 目录结构

```text
.
├── src/QingJian.App/       # WPF application / 应用程序
│   ├── Data/               # SQLite context and repositories / 数据访问
│   ├── Services/           # Application use cases / 业务服务
│   ├── ViewModels/         # MVVM state and commands / MVVM 状态
│   ├── Views/              # Main and auxiliary WPF windows / 界面
│   ├── QuickNotes/         # Quick-note workflow / 快速笔记
│   ├── TodoWidgets/        # Desktop todo widget / 桌面待办
│   ├── Settings/           # Preferences and startup / 设置
│   ├── Hotkeys/ Tray/      # Global shortcut and tray integration
│   ├── EditorAssets/       # WebView2 editor host and vendor assets
│   └── Assets/ Resources/  # App icon and shared WPF resources
├── tests/QingJian.App.Tests/ # Unit and XAML tests / 单元与界面测试
├── docs/                   # Status, design notes, and implementation plans
├── assets/branding/        # Source artwork used by the project
├── installer/              # Inno Setup definition
├── scripts/                # Build and packaging scripts
├── QingJian.sln
└── Directory.Build.props
```

`docs/README.md` is the documentation index. `docs/project-status.md` contains the current architecture, deferred work, and handoff notes.

`docs/README.md` 是文档索引；`docs/project-status.md` 记录当前架构、暂缓事项和交接信息。

## Data and releases / 数据与发布

User data is stored under `%LOCALAPPDATA%\QingJian`:

用户数据默认保存在 `%LOCALAPPDATA%\QingJian`：

- `qingjian.db` — notes and todos / 笔记与待办数据库
- `settings.json` — preferences / 设置
- `attachments/` — local images / 本地图片

Download the latest Windows installer from [GitHub Releases](https://github.com/CristinXie/qingjian/releases). To build it locally:

```powershell
.\scripts\build-installer.ps1
```

## Contributing / 参与开发

Please read [CONTRIBUTING.md](CONTRIBUTING.md), [docs/README.md](docs/README.md), and [docs/project-status.md](docs/project-status.md) before making changes. Start feature work from `develop` in a `feature/*` branch, add tests for behavior changes, and target pull requests at `develop`.

提交修改前请先阅读 [CONTRIBUTING.md](CONTRIBUTING.md)、[docs/README.md](docs/README.md) 和 [docs/project-status.md](docs/project-status.md)。功能开发从 `develop` 创建 `feature/*` 分支；行为变更请补充测试，Pull Request 目标分支为 `develop`。

## License / 许可证

[MIT](LICENSE)
