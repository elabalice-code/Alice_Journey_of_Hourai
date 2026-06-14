@echo off
setlocal

set "PROJECT_ROOT=%~dp0"
set "PROJECT_ROOT=%PROJECT_ROOT:~0,-1%"

if defined GODOT_EXE (
    set "GODOT=%GODOT_EXE%"
) else (
    set "GODOT=%PROJECT_ROOT%\..\buildtools\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64.exe"
)

if not exist "%GODOT%" (
    set "GODOT=%PROJECT_ROOT%\..\buildtools\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe"
)

if not exist "%GODOT%" (
    echo Godot executable not found.
    echo Set GODOT_EXE or install Godot under:
    echo %PROJECT_ROOT%\..\buildtools\Godot_v4.5.1-stable_mono_win64
    exit /b 1
)

if not exist "%PROJECT_ROOT%\project.godot" (
    echo project.godot not found beside start.bat.
    exit /b 1
)

echo Project: %PROJECT_ROOT%
echo Godot:   %GODOT%
echo.

start "" "%GODOT%" --path "%PROJECT_ROOT%" %*
