param(
    [switch]$FailOnHigh
)

$ErrorActionPreference = "Stop"

$projectRoot = $PSScriptRoot

function Get-RelativePath {
    param([string]$Path)
    $root = (Resolve-Path -LiteralPath $projectRoot).Path.TrimEnd("\", "/")
    $target = (Resolve-Path -LiteralPath $Path).Path
    if ($target.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $target.Substring($root.Length).TrimStart("\", "/").Replace("\", "/")
    }
    return $target.Replace("\", "/")
}

function New-Rule {
    param(
        [string]$Layer,
        [string]$Root,
        [string]$Severity,
        [string]$Name,
        [string]$Pattern,
        [string]$Note
    )
    return [ordered]@{
        Layer = $Layer
        Root = $Root
        Severity = $Severity
        Name = $Name
        Pattern = $Pattern
        Note = $Note
    }
}

function Is-Allowed {
    param(
        [string]$RelativePath,
        [string]$Line,
        [hashtable]$Rule
    )
    foreach ($allow in $allowList) {
        if ($RelativePath -eq $allow.Path -and $Rule.Name -eq $allow.Rule -and $Line -match $allow.Pattern) {
            return $true
        }
    }
    return $false
}

$rules = @(
    (New-Rule "Signal" "CoreEngine/Scripts/Signal" "High" "Signal scene side effect" "(get_node_or_null|find_children|NodePath|\.set\(|\.call\(|create_timer|ResourceLoader|\bload\(|instantiate\(|queue_free|add_child|remove_child|move_child)" "Signal should build frames/intents from plain data, not inspect or mutate scenes/resources."),
    (New-Rule "Signal" "CoreEngine/Scripts/Signal" "High" "Signal workbench side effect" "(WorkbenchService|register_actor|get_service|\bsend\(|broadcast\()" "Workbench ownership belongs to Actor."),
    (New-Rule "Signal" "CoreEngine/Scripts/Signal" "Medium" "Signal runtime object coupling" "(MetSys|Game|Player|res://CoreEngine/Scripts/(Characters|Game|Objects)/)" "Prefer plain facts or static catalogs when Signal needs runtime context."),
    (New-Rule "Signal" "CoreEngine/Scripts/Signal" "Info" "Signal actor data coupling" "ActorFramework" "Message constants belong to Contract/MessageTypes.gd. Remaining ActorFramework references should be data containers such as QuestData or InventoryData."),

    (New-Rule "Helper" "CoreEngine/Scripts/Helper" "High" "Helper side effect" "(WorkbenchService|register_actor|get_service|\bsend\(|broadcast\(|\.set\(|\.call\(|create_timer|ResourceLoader|\bload\(|instantiate\(|queue_free|add_child|remove_child|move_child)" "Helper should stay closed input/output calculation."),
    (New-Rule "Helper" "CoreEngine/Scripts/Helper" "Medium" "Helper scene tree dependency" "(get_node_or_null|find_children|NodePath)" "Prefer collecting facts at Actor boundary; fact collectors must be explicit and documented."),
    (New-Rule "Helper" "CoreEngine/Scripts/Helper" "Medium" "Helper domain coupling" "(ActorFramework|MetSys|Game|Player)" "Helpers should avoid owning actor data types unless they are thin shape utilities.")
)

$allowList = @(
    @{
        Path = "CoreEngine/Scripts/Helper/Map/MapNodeFacts.gd"
        Rule = "Helper scene tree dependency"
        Pattern = "find_children"
        Why = "Explicit map-node fact collector for Signal input."
    }
)

$findings = New-Object System.Collections.Generic.List[object]
$allowed = New-Object System.Collections.Generic.List[object]

foreach ($rule in $rules) {
    $rootPath = Join-Path $projectRoot $rule.Root
    if (-not (Test-Path -LiteralPath $rootPath)) {
        continue
    }
    $files = Get-ChildItem -LiteralPath $rootPath -Recurse -File -Filter "*.gd"
    foreach ($file in $files) {
        $relativePath = Get-RelativePath $file.FullName
        $lineNumber = 0
        foreach ($line in [System.IO.File]::ReadLines($file.FullName)) {
            $lineNumber += 1
            if ($line.TrimStart().StartsWith("#")) {
                continue
            }
            if ($line -cnotmatch $rule.Pattern) {
                continue
            }
            $entry = [ordered]@{
                Severity = $rule.Severity
                Layer = $rule.Layer
                Rule = $rule.Name
                File = $relativePath
                Line = $lineNumber
                Text = $line.Trim()
                Note = $rule.Note
            }
            if (Is-Allowed $relativePath $line $rule) {
                $allowed.Add([pscustomobject]$entry) | Out-Null
            } else {
                $findings.Add([pscustomobject]$entry) | Out-Null
            }
        }
    }
}

$actorRoot = Join-Path $projectRoot "CoreEngine/Scripts/Actor"
if (Test-Path -LiteralPath $actorRoot) {
    foreach ($file in Get-ChildItem -LiteralPath $actorRoot -Recurse -File -Filter "*.gd") {
        $lineCount = ([System.IO.File]::ReadAllLines($file.FullName)).Count
        if ($lineCount -gt 300 -and $file.Name -ne "TestActor.gd") {
            $findings.Add([pscustomobject][ordered]@{
                Severity = "Low"
                Layer = "Actor"
                Rule = "Large actor"
                File = Get-RelativePath $file.FullName
                Line = 1
                Text = "$lineCount lines"
                Note = "Large actors are candidates for Signal/Helper extraction, but side effects may remain here."
            }) | Out-Null
        }
    }
}

Write-Host "Architecture boundary scan"
Write-Host "Project: $projectRoot"
Write-Host ""

$severityOrder = @("High", "Medium", "Low", "Info")
foreach ($severity in $severityOrder) {
    $group = @($findings | Where-Object { $_.Severity -eq $severity })
    if ($group.Count -eq 0) {
        continue
    }
    Write-Host "== $severity ($($group.Count)) =="
    foreach ($item in $group) {
        Write-Host ("{0}:{1} [{2}] {3}" -f $item.File, $item.Line, $item.Rule, $item.Text)
        Write-Host ("  {0}" -f $item.Note)
    }
    Write-Host ""
}

if ($allowed.Count -gt 0) {
    Write-Host "== Allowed adapters ($($allowed.Count)) =="
    foreach ($item in $allowed) {
        Write-Host ("{0}:{1} [{2}] {3}" -f $item.File, $item.Line, $item.Rule, $item.Text)
    }
    Write-Host ""
}

$highCount = @($findings | Where-Object { $_.Severity -eq "High" }).Count
Write-Host ("Summary: High={0}, Medium={1}, Low={2}, Info={3}, Allowed={4}" -f `
    $highCount, `
    @($findings | Where-Object { $_.Severity -eq "Medium" }).Count, `
    @($findings | Where-Object { $_.Severity -eq "Low" }).Count, `
    @($findings | Where-Object { $_.Severity -eq "Info" }).Count, `
    $allowed.Count)

if ($FailOnHigh -and $highCount -gt 0) {
    exit 1
}
