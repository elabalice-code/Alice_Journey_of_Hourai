$ErrorActionPreference = 'Stop'

function Get-GodotRoot([string]$StartDir) {
    $dir = (Resolve-Path $StartDir).Path
    while ($true) {
        if (Test-Path (Join-Path $dir ".godot")) {
            return $dir
        }
        $parent = Split-Path -Parent $dir
        if ($parent -eq $dir -or [string]::IsNullOrWhiteSpace($parent)) {
            throw "Could not find Godot root (folder containing .godot) starting from: $StartDir"
        }
        $dir = $parent
    }
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$godotRoot = Get-GodotRoot $scriptDir
$destRoot = Join-Path $godotRoot "GodotTools-Build\MapEditor"

$projects = Get-ChildItem -Path $scriptDir -Recurse -Filter *.csproj |
    Where-Object { $_.FullName -notmatch '\\bin\\' -and $_.FullName -notmatch '\\obj\\' }

if (-not $projects -or $projects.Count -eq 0) {
    throw "No .csproj found under: $scriptDir"
}

New-Item -ItemType Directory -Force -Path $destRoot | Out-Null

foreach ($proj in $projects) {
    dotnet restore $proj.FullName
    dotnet publish $proj.FullName -c Release -r win-x64 --self-contained false -o $destRoot
}

Write-Host "Build output copied to: $destRoot"
