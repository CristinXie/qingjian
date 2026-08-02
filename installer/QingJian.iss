#ifndef PublishDir
  #error PublishDir must be provided by build-installer.ps1
#endif
#ifndef WebView2Bootstrapper
  #error WebView2Bootstrapper must be provided by build-installer.ps1
#endif
#ifndef OutputDir
  #error OutputDir must be provided by build-installer.ps1
#endif

#define AppName "QingJian"
#define AppVersion "0.1.0"

[Setup]
AppId={{6DCE9EF8-0E5B-4705-86CB-4D397226C321}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=QingJian
DefaultDirName={localappdata}\Programs\QingJian
DefaultGroupName=QingJian
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=QingJian-Setup
SetupIconFile={#SourcePath}..\src\QingJian.App\Assets\qingjian.ico
UninstallDisplayIcon={app}\QingJian.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=force
RestartApplications=no
AppMutex=Local\QingJian.6DCE9EF8-0E5B-4705-86CB-4D397226C321.Primary

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："; Flags: checkedonce

[Files]
Source: "{#PublishDir}\QingJian.App.exe"; DestDir: "{app}"; DestName: "QingJian.exe"; Flags: ignoreversion
Source: "{#WebView2Bootstrapper}"; DestDir: "{tmp}"; DestName: "MicrosoftEdgeWebview2Setup.exe"; Flags: deleteafterinstall

[Icons]
Name: "{autoprograms}\QingJian"; Filename: "{app}\QingJian.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\QingJian"; Filename: "{app}\QingJian.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{tmp}\MicrosoftEdgeWebview2Setup.exe"; Parameters: "/silent /install"; StatusMsg: "正在安装 Microsoft Edge WebView2 Runtime..."; Flags: waituntilterminated; Check: not IsWebView2RuntimeInstalled
Filename: "{app}\QingJian.exe"; Description: "运行 QingJian"; Flags: nowait postinstall skipifsilent

[Code]
function RuntimeVersionExists(RootKey: Integer): Boolean;
var
  Version: String;
begin
  Result := RegQueryStringValue(
    RootKey,
    'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}',
    'pv',
    Version) and (Version <> '');
end;

function IsWebView2RuntimeInstalled(): Boolean;
begin
  Result := RuntimeVersionExists(HKLM32) or RuntimeVersionExists(HKCU32);
end;
