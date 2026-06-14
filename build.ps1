param(
    [ValidateSet("Check", "Import", "Smoke", "Tools", "Export", "All")]
    [string]$Mode = "Check",

    [string]$GodotExe,
    [string]$Preset,
    [string]$OutputPath,
    [switch]$DebugExport,
    [switch]$SkipSmoke,
    [switch]$SkipTools,
    [switch]$SkipToolSelfTest
)

$ErrorActionPreference = "Stop"

function Resolve-ProjectRoot {
    return $PSScriptRoot
}

function Resolve-GodotExe {
    param([string]$RequestedExe, [string]$ProjectRoot)

    $candidates = @()
    if ($RequestedExe) {
        $candidates += $RequestedExe
    }
    if ($env:GODOT_EXE) {
        $candidates += $env:GODOT_EXE
    }

    $workspaceRoot = (Resolve-Path (Join-Path $ProjectRoot "..\..")).Path
    $candidates += Join-Path $workspaceRoot "0_Codes\buildtools\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe"
    $candidates += Join-Path (Split-Path -Parent $ProjectRoot) "buildtools\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe"
    $candidates += Join-Path (Split-Path -Parent $ProjectRoot) "Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe"

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw "Godot executable not found. Pass -GodotExe or set GODOT_EXE."
}

function Invoke-Logged {
    param(
        [string]$Title,
        [string]$FilePath,
        [string[]]$Arguments
    )

    $buildLogDir = Join-Path $PSScriptRoot "BuildLogs"
    New-Item -ItemType Directory -Force -Path $buildLogDir | Out-Null
    $safeTitle = $Title -replace '[\\/:*?"<>| ]+', "_"
    $logPath = Join-Path $buildLogDir "$safeTitle.log"

    Write-Host ""
    Write-Host "== $Title =="
    Write-Host "$FilePath $($Arguments -join ' ')"
    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        & $FilePath @Arguments 2>&1 | ForEach-Object { "$_" } | Tee-Object -FilePath $logPath
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    if ($exitCode -ne 0) {
        throw "$Title failed with exit code $exitCode. See $logPath"
    }

    $errorLines = Select-String -LiteralPath $logPath -Pattern "SCRIPT ERROR:", "ERROR:" -SimpleMatch |
        Where-Object {
            $_.Line -notmatch "RID allocations of type" -and
            $_.Line -notmatch "ObjectDB instances leaked"
        }
    if ($errorLines) {
        $first = $errorLines | Select-Object -First 1
        throw "$Title completed with logged errors. First error: $($first.Line). See $logPath"
    }
}

function Test-ProjectShape {
    param([string]$ProjectRoot)

    $required = @(
        "project.godot",
        "CoreEngine\Game.tscn",
        "CoreEngine\Scripts\Core\Workbench.gd",
        "addons\MetroidvaniaSystem\Nodes\Singleton.tscn"
    )

    foreach ($relativePath in $required) {
        $path = Join-Path $ProjectRoot $relativePath
        if (-not (Test-Path -LiteralPath $path)) {
            throw "Required project file is missing: $relativePath"
        }
    }

    Write-Host "Project shape OK: $ProjectRoot"
}

function Clear-StaleEditorState {
    param([string]$ProjectRoot)

    $editorDir = Join-Path $ProjectRoot ".godot\editor"
    if (-not (Test-Path -LiteralPath $editorDir)) {
        return
    }

    $cacheFiles = @(
        "editor_layout.cfg",
        "project_metadata.cfg",
        "script_editor_cache.cfg"
    )

    foreach ($cacheFile in $cacheFiles) {
        $path = Join-Path $editorDir $cacheFile
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Force
            Write-Host "Removed stale editor cache: .godot/editor/$cacheFile"
        }
    }
}

function Invoke-GodotImport {
    param([string]$GodotExe, [string]$ProjectRoot)

    Clear-StaleEditorState -ProjectRoot $ProjectRoot

    Invoke-Logged "Godot resource import" $GodotExe @(
        "--headless",
        "--import",
        "--path", $ProjectRoot
    )
}

function Invoke-GodotSmoke {
    param([string]$GodotExe, [string]$ProjectRoot)

    Invoke-Logged "Godot smoke run" $GodotExe @(
        "--headless",
        "--path", $ProjectRoot,
        "--quit-after", "3"
    )
}

function Get-ToolManifest {
    param([string]$ProjectRoot)

    $manifestPath = Join-Path $ProjectRoot "GodotTools\tools.json"
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        throw "GodotTools manifest not found: $manifestPath"
    }

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if (-not $manifest.tools -or $manifest.tools.Count -eq 0) {
        throw "GodotTools manifest has no tools: $manifestPath"
    }

    return $manifest
}

function Resolve-ToolToken {
    param(
        [string]$Value,
        [string]$ProjectRoot,
        [object]$Manifest
    )

    if ($null -eq $Value) {
        return ""
    }

    $toolsRootRelative = "GodotTools"
    if ($Manifest.toolsRoot) {
        $toolsRootRelative = [string]$Manifest.toolsRoot
    }
    $outputRootRelative = "GodotTools-Build"
    if ($Manifest.outputRoot) {
        $outputRootRelative = [string]$Manifest.outputRoot
    }

    $toolsRoot = Join-Path $ProjectRoot ($toolsRootRelative -replace '/', '\')
    $outputRoot = Join-Path $ProjectRoot ($outputRootRelative -replace '/', '\')

    return $Value.
        Replace("{projectRoot}", $ProjectRoot).
        Replace("{toolsRoot}", $toolsRoot).
        Replace("{outputRoot}", $outputRoot)
}

function Resolve-ProjectPath {
    param(
        [string]$ProjectRoot,
        [string]$RelativePath
    )

    if ([System.IO.Path]::IsPathRooted($RelativePath)) {
        return $RelativePath
    }
    return Join-Path $ProjectRoot ($RelativePath -replace '/', '\')
}

function Get-ToolEntries {
    param([string]$ProjectRoot)

    $manifest = Get-ToolManifest -ProjectRoot $ProjectRoot
    $ids = @{}
    $entries = @()

    foreach ($tool in $manifest.tools) {
        if (-not $tool.id) {
            throw "GodotTools manifest contains a tool without id."
        }
        if ($ids.ContainsKey($tool.id)) {
            throw "Duplicate GodotTools id in manifest: $($tool.id)"
        }
        $ids[$tool.id] = $true

        if (-not $tool.name) {
            throw "GodotTools manifest entry '$($tool.id)' has no name."
        }
        if (-not $tool.project) {
            throw "GodotTools manifest entry '$($tool.id)' has no project."
        }

        $projectPath = Resolve-ProjectPath -ProjectRoot $ProjectRoot -RelativePath $tool.project
        if (-not (Test-Path -LiteralPath $projectPath)) {
            throw "Tool project missing for '$($tool.id)': $projectPath"
        }

        $selfTestArgs = @()
        if ($tool.selfTest -and $tool.selfTest.args) {
            foreach ($arg in $tool.selfTest.args) {
                $selfTestArgs += Resolve-ToolToken -Value ([string]$arg) -ProjectRoot $ProjectRoot -Manifest $manifest
            }
        }

        $entries += [pscustomobject]@{
            Id = [string]$tool.id
            Name = [string]$tool.name
            Category = [string]$tool.category
            Project = $projectPath
            SelfTestKind = if ($tool.selfTest) { [string]$tool.selfTest.kind } else { "" }
            SelfTestArgs = $selfTestArgs
        }
    }

    return $entries | Sort-Object Project
}

function Invoke-ToolBuild {
    param([string]$ProjectRoot)

    $tools = Get-ToolEntries -ProjectRoot $ProjectRoot
    foreach ($tool in $tools) {
        Write-Host ""
        Write-Host "== Tool restore: $($tool.Name) =="
        Write-Host $tool.Project
        dotnet restore $tool.Project
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet restore failed for $($tool.Name)"
        }

        Write-Host "== Tool build: $($tool.Name) =="
        dotnet build $tool.Project -c Release --no-restore
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed for $($tool.Name)"
        }
    }
}

function Invoke-ToolSelfTest {
    param([string]$ProjectRoot)

    $tools = Get-ToolEntries -ProjectRoot $ProjectRoot
    foreach ($tool in $tools) {
        if (-not $tool.SelfTestKind) {
            Write-Host "Skipping tool without self-test: $($tool.Name)"
            continue
        }
        if ($tool.SelfTestKind -ne "dotnet-run") {
            throw "Unsupported self-test kind for $($tool.Name): $($tool.SelfTestKind)"
        }

        Write-Host ""
        Write-Host "== Tool agent self-test: $($tool.Name) =="
        $runArgs = @("run", "--project", $tool.Project, "-c", "Release", "--no-build", "--") + $tool.SelfTestArgs
        & dotnet @runArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Tool agent self-test failed for $($tool.Name)"
        }
    }
}

function Invoke-ToolsUpdate {
    param(
        [string]$ProjectRoot,
        [bool]$SkipSelfTest
    )

    Invoke-ToolBuild -ProjectRoot $ProjectRoot
    if (-not $SkipSelfTest) {
        Invoke-ToolSelfTest -ProjectRoot $ProjectRoot
    }
}

function Invoke-GodotExport {
    param(
        [string]$GodotExe,
        [string]$ProjectRoot,
        [string]$Preset,
        [string]$OutputPath,
        [bool]$DebugExport
    )

    $presetsPath = Join-Path $ProjectRoot "export_presets.cfg"
    if (-not (Test-Path -LiteralPath $presetsPath)) {
        throw "export_presets.cfg not found. Create an export preset in Godot first, or run -Mode Check/Tools."
    }
    if (-not $Preset) {
        throw "Export mode requires -Preset."
    }
    if (-not $OutputPath) {
        $safePreset = $Preset -replace '[\\/:*?"<>| ]+', "_"
        $OutputPath = Join-Path $ProjectRoot "Builds\$safePreset\$safePreset.exe"
    }

    $outputDir = Split-Path -Parent $OutputPath
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

    $exportFlag = if ($DebugExport) { "--export-debug" } else { "--export-release" }
    Invoke-Logged "Godot export" $GodotExe @(
        "--headless",
        "--path", $ProjectRoot,
        $exportFlag, $Preset, $OutputPath
    )
}

$projectRoot = Resolve-ProjectRoot
$godot = Resolve-GodotExe -RequestedExe $GodotExe -ProjectRoot $projectRoot

Write-Host "Project: $projectRoot"
Write-Host "Godot:   $godot"
Write-Host "Mode:    $Mode"

Test-ProjectShape -ProjectRoot $projectRoot

switch ($Mode) {
    "Check" {
        Invoke-GodotImport -GodotExe $godot -ProjectRoot $projectRoot
        if (-not $SkipTools) {
            Invoke-ToolsUpdate -ProjectRoot $projectRoot -SkipSelfTest ([bool]$SkipToolSelfTest)
        }
        if (-not $SkipSmoke) {
            Invoke-GodotSmoke -GodotExe $godot -ProjectRoot $projectRoot
        }
    }
    "Import" {
        Invoke-GodotImport -GodotExe $godot -ProjectRoot $projectRoot
    }
    "Smoke" {
        Invoke-GodotSmoke -GodotExe $godot -ProjectRoot $projectRoot
    }
    "Tools" {
        Invoke-ToolsUpdate -ProjectRoot $projectRoot -SkipSelfTest ([bool]$SkipToolSelfTest)
    }
    "Export" {
        Invoke-GodotImport -GodotExe $godot -ProjectRoot $projectRoot
        if (-not $SkipTools) {
            Invoke-ToolsUpdate -ProjectRoot $projectRoot -SkipSelfTest ([bool]$SkipToolSelfTest)
        }
        Invoke-GodotExport -GodotExe $godot -ProjectRoot $projectRoot -Preset $Preset -OutputPath $OutputPath -DebugExport ([bool]$DebugExport)
    }
    "All" {
        Invoke-GodotImport -GodotExe $godot -ProjectRoot $projectRoot
        if (-not $SkipTools) {
            Invoke-ToolsUpdate -ProjectRoot $projectRoot -SkipSelfTest ([bool]$SkipToolSelfTest)
        }
        if (-not $SkipSmoke) {
            Invoke-GodotSmoke -GodotExe $godot -ProjectRoot $projectRoot
        }
        if ($Preset) {
            Invoke-GodotExport -GodotExe $godot -ProjectRoot $projectRoot -Preset $Preset -OutputPath $OutputPath -DebugExport ([bool]$DebugExport)
        }
    }
}

Write-Host ""
Write-Host "Build script completed."
