[CmdletBinding()]
param(
    [string]$OutputDirectory,
    [string]$InnoCompilerPath,
    [string]$WebView2BootstrapperPath
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$artifactsDirectory = Join-Path $repositoryRoot "artifacts"
$publishDirectory = Join-Path $artifactsDirectory "publish\win-x64"
$cacheDirectory = Join-Path $artifactsDirectory "cache"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $artifactsDirectory "installer"
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $publishDirectory, $cacheDirectory, $OutputDirectory | Out-Null

if ([string]::IsNullOrWhiteSpace($InnoCompilerPath)) {
    $compilerCandidates = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )
    $InnoCompilerPath = $compilerCandidates |
        Where-Object { Test-Path -LiteralPath $_ } |
        Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($InnoCompilerPath) -or
    -not (Test-Path -LiteralPath $InnoCompilerPath)) {
    throw "Inno Setup 6 compiler was not found. Install JRSoftware.InnoSetup with winget."
}

if ([string]::IsNullOrWhiteSpace($WebView2BootstrapperPath)) {
    $WebView2BootstrapperPath = Join-Path $cacheDirectory "MicrosoftEdgeWebview2Setup.exe"
    if (-not (Test-Path -LiteralPath $WebView2BootstrapperPath)) {
        Invoke-WebRequest `
            -Uri "https://go.microsoft.com/fwlink/p/?LinkId=2124703" `
            -OutFile $WebView2BootstrapperPath `
            -UseBasicParsing
    }
}

if (-not (Test-Path -LiteralPath $WebView2BootstrapperPath)) {
    throw "WebView2 Evergreen Bootstrapper was not found: $WebView2BootstrapperPath"
}

$projectPath = Join-Path $repositoryRoot "src\QingJian.App\QingJian.App.csproj"
& dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:IncludeAllContentForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$innoDefinition = Join-Path $repositoryRoot "installer\QingJian.iss"
$innoArguments = @(
    "/DPublishDir=$publishDirectory",
    "/DWebView2Bootstrapper=$WebView2BootstrapperPath",
    "/DOutputDir=$OutputDirectory",
    $innoDefinition
)
& $InnoCompilerPath @innoArguments
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
}

$setupPath = Join-Path $OutputDirectory "QingJian-Setup.exe"
if (-not (Test-Path -LiteralPath $setupPath)) {
    throw "Inno Setup did not create the expected installer: $setupPath"
}

Write-Output $setupPath
