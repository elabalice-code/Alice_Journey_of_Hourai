param()

$ErrorActionPreference = "Stop"

$projectRoot = $PSScriptRoot
$toolHubProject = Join-Path $projectRoot "GodotTools\ToolHub\ToolHub\ToolHub.csproj"
if (-not (Test-Path -LiteralPath $toolHubProject)) {
    throw "ToolHub project not found: $toolHubProject"
}

$NoBuild = $false
$ToolArgs = @()
foreach ($arg in $args) {
    if ($arg -eq "-NoBuild" -or $arg -eq "--NoBuild" -or $arg -eq "/NoBuild") {
        $NoBuild = $true
        continue
    }
    $ToolArgs += $arg
}
if (-not $ToolArgs -or $ToolArgs.Count -eq 0) {
    $ToolArgs = @("list")
}

$hasGodotRoot = $false
foreach ($arg in $ToolArgs) {
    if ($arg -eq "--godotRoot" -or $arg -eq "--godot-root") {
        $hasGodotRoot = $true
        break
    }
}
if (-not $hasGodotRoot) {
    $ToolArgs += @("--godotRoot", $projectRoot)
}

$dotnetArgs = @("run", "--project", $toolHubProject, "-c", "Release")
if ($NoBuild) {
    $dotnetArgs += "--no-build"
}
$dotnetArgs += "--"
$dotnetArgs += $ToolArgs

& dotnet @dotnetArgs
exit $LASTEXITCODE
