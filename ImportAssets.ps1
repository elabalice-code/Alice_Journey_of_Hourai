param(
	[string]$GodotExe,
	[string]$ProjectPath
)

if (-not $ProjectPath -or $ProjectPath.Trim().Length -eq 0) {
	$ProjectPath = Split-Path -Parent $MyInvocation.MyCommand.Path
}

if (-not $GodotExe -or $GodotExe.Trim().Length -eq 0) {
	if ($env:GODOT_EXE -and $env:GODOT_EXE.Trim().Length -gt 0) {
		$GodotExe = $env:GODOT_EXE
	} else {
		$repoRoot = Split-Path -Parent $ProjectPath
		$guess = Join-Path $repoRoot "Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe"
		if (Test-Path $guess) {
			$GodotExe = $guess
		}
	}
}

if (-not $GodotExe -or -not (Test-Path $GodotExe)) {
	throw "Godot 可执行文件未找到。请传入 -GodotExe，或设置环境变量 GODOT_EXE。"
}

if (-not (Test-Path $ProjectPath)) {
	throw "ProjectPath 不存在：$ProjectPath"
}

& $GodotExe --headless --editor --quit --path $ProjectPath
exit $LASTEXITCODE
