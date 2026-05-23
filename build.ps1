param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
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

$cmake = Resolve-CommandPath 'cmake'
$pnpm = Resolve-CommandPath 'pnpm'
$dotnet = Resolve-CommandPath 'dotnet'
$generator = 'Visual Studio 17 2022'

Invoke-Step 'Install web-panel dependencies' {
    & $pnpm --dir $webPanelDir install --frozen-lockfile
}

Invoke-Step 'Build web-panel' {
    & $pnpm --dir $webPanelDir run build
}

Invoke-Step 'Configure asar-fuses-bypass' {
    & $cmake -S $asarFusesSourceDir -B $asarFusesBuildDir -G $generator -A x64
}

Invoke-Step 'Build asar-fuses-bypass' {
    & $cmake --build $asarFusesBuildDir --config $Configuration
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
