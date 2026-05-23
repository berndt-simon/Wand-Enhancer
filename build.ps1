param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    # Skip the dockerized native build and reuse an existing version.dll
    # (e.g. a CI artifact already placed in the expected output path).
    [switch]$SkipNativeBuild
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$webPanelDir = Join-Path $repoRoot 'web-panel'
$nativeBuildRoot = Join-Path $repoRoot '.tmp/cmake'
$asarFusesSourceDir = Join-Path $repoRoot 'tools/asar-fuses-bypass'
$asarFusesBuildDir = Join-Path $nativeBuildRoot 'asar-fuses-bypass'
$appProject = Join-Path $repoRoot 'WandEnhancer/WandEnhancer.csproj'

function Resolve-CommandPath {
    param([string]$Name)

    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if (-not $command) {
        throw "Required command not found in PATH: $Name"
    }

    return $command.Source
}

function Invoke-Step {
    param(
        [string]$Label,
        [scriptblock]$Action
    )

    Write-Host "==> $Label" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "Step failed: $Label"
    }
}

$pnpm = Resolve-CommandPath 'pnpm'
$dotnet = Resolve-CommandPath 'dotnet'
$docker = if ($SkipNativeBuild) { $null } else { Resolve-CommandPath 'docker' }

# WandEnhancer.csproj embeds the proxy DLL from
# .tmp/cmake/asar-fuses-bypass/<Debug|Release>/version.dll
$nativeConfig = if ($Configuration -eq 'Debug') { 'Debug' } else { 'Release' }
$nativeOutDir = Join-Path $asarFusesBuildDir $nativeConfig

Invoke-Step 'Install web-panel dependencies' {
    & $pnpm --dir $webPanelDir install --frozen-lockfile
}

Invoke-Step 'Build web-panel' {
    & $pnpm --dir $webPanelDir run build
}

# Cross-compile version.dll on Linux via MinGW-w64 in a container, then export
# just the artifact straight into the path the .csproj embeds.
if ($SkipNativeBuild) {
    $proxyDll = Join-Path $nativeOutDir 'version.dll'
    if (-not (Test-Path $proxyDll)) {
        throw "SkipNativeBuild set but proxy DLL is missing: $proxyDll"
    }
    Write-Host "==> Skipping native build, using $proxyDll" -ForegroundColor Cyan
}
else {
    Invoke-Step 'Build asar-fuses-bypass (docker)' {
        New-Item -ItemType Directory -Force $nativeOutDir | Out-Null
        & $docker build `
            --build-arg "BUILD_TYPE=$nativeConfig" `
            -o "type=local,dest=$nativeOutDir" `
            $asarFusesSourceDir
    }
}

Invoke-Step 'Publish WandEnhancer (self-contained single-file)' {
    & $dotnet publish $appProject `
        -c $Configuration `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true
}

$publishExe = Join-Path $repoRoot "WandEnhancer/bin/$Configuration/net10.0-windows/win-x64/publish/WandEnhancer.exe"

Write-Host ''
Write-Host "Build completed successfully ($Configuration)." -ForegroundColor Green
Write-Host "Output: $publishExe" -ForegroundColor Green
