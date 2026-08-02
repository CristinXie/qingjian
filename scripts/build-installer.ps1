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

if ([string]::IsNullOrWhiteSpace($InnoCompilerPath)) {
    $innoToolDirectory = Join-Path $artifactsDirectory "tools\innosetup-6.7.3"
    $localCompilerPath = Join-Path $innoToolDirectory "tools\ISCC.exe"
    if (-not (Test-Path -LiteralPath $localCompilerPath)) {
        $innoPackagePath = Join-Path $cacheDirectory "tools.innosetup.6.7.3.nupkg"
        Invoke-WebRequest `
            -Uri "https://api.nuget.org/v3-flatcontainer/tools.innosetup/6.7.3/tools.innosetup.6.7.3.nupkg" `
            -OutFile $innoPackagePath `
            -UseBasicParsing

        $expectedPackageHash = "F780898E402FF80612CC8D9FCB8C6E02932BD1CB4C900FFDAA31F9341CFB49F4"
        $actualPackageHash = (Get-FileHash -LiteralPath $innoPackagePath -Algorithm SHA256).Hash
        if ($actualPackageHash -ne $expectedPackageHash) {
            throw "Downloaded Inno Setup package hash does not match the approved package."
        }

        $innoZipPath = Join-Path $cacheDirectory "tools.innosetup.6.7.3.zip"
        Copy-Item -LiteralPath $innoPackagePath -Destination $innoZipPath -Force
        New-Item -ItemType Directory -Force -Path $innoToolDirectory | Out-Null
        Expand-Archive -LiteralPath $innoZipPath -DestinationPath $innoToolDirectory -Force
    }

    $InnoCompilerPath = $localCompilerPath
}

if (-not (Test-Path -LiteralPath $InnoCompilerPath)) {
    throw "Inno Setup 6 compiler was not found: $InnoCompilerPath"
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
