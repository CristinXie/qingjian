# Third-Party Notices

QingJian includes or redistributes the following third-party software. Their
licenses remain applicable to the corresponding components.

## Runtime Components

| Component | Version | License | Upstream |
| --- | --- | --- | --- |
| Markdig | 1.3.2 | BSD-2-Clause | [xoofx/markdig](https://github.com/xoofx/markdig) |
| Microsoft.EntityFrameworkCore.Sqlite | 8.0.11 | MIT | [dotnet/efcore](https://github.com/dotnet/efcore) |
| Microsoft.Web.WebView2 | 1.0.4078.44 | See the package license | [NuGet package](https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4078.44) |
| SQLitePCLRaw.bundle_e_sqlite3 | 2.1.6 | Apache-2.0 | [ericsink/SQLitePCL.raw](https://github.com/ericsink/SQLitePCL.raw) |
| Toast UI Editor | 3.2.2 | MIT | [nhn/tui.editor](https://github.com/nhn/tui.editor) |

The WebView2 NuGet package contains its applicable `LICENSE.txt`. The
Microsoft Edge WebView2 Runtime and its Evergreen Bootstrapper are Microsoft
software and remain subject to Microsoft's applicable terms.

The self-contained Windows release also includes .NET runtime components.
Those components are distributed under Microsoft's applicable licenses and
notices from the .NET runtime distribution.

## Development And Test Components

The test project uses xUnit 2.9.2 and related packages under Apache-2.0, along
with Microsoft test infrastructure packages under their respective licenses.
These packages are not required by the installed application.

The installer build uses Inno Setup 6.7.3 as a build tool. Inno Setup is
distributed under its own license; see [jrsoftware.org/isinfo.php](https://jrsoftware.org/isinfo.php).

## Vendored Editor Assets

The files under `src/QingJian.App/EditorAssets/vendor/` are built from Toast UI
Editor 3.2.2 and retain the upstream MIT license notice where supplied.
