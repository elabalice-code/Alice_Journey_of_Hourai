@echo off
setlocal enabledelayedexpansion

echo ========================================
echo   MapEditorTool Build Script
echo ========================================
echo.

REM Set paths
set "SOLUTION_DIR=%~dp0"
set "SOLUTION_FILE=%SOLUTION_DIR%MapEditorTool.sln"
set "OUTPUT_DIR=%SOLUTION_DIR%MapEditorTool\bin\Release"
set "PACKAGE_DIR=%SOLUTION_DIR%ReleasePackage"
set "LEGACY_MAPEDITOR_DIR=%SOLUTION_DIR%..\MapEditor"

REM Try MSBuild paths in order: VS2022 Community (64-bit preferred), then BuildTools
set "MSBUILD="
if exist "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" (
    set "MSBUILD=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe"
) else if exist "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" (
    set "MSBUILD=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
) else if exist "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe" (
    set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe"
) else if exist "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\MSBuild.exe" (
    set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
)

if "!MSBUILD!"=="" (
    echo [ERROR] MSBuild not found. Please install Visual Studio 2022 or Build Tools.
    pause
    exit /b 1
)
echo [INFO] Using MSBuild: !MSBUILD!
echo.

REM Step 1: Clean build
echo [1/3] Cleaning previous build...
if exist "!OUTPUT_DIR!\" (
    rmdir /s /q "!OUTPUT_DIR!"
    echo        Cleaned: !OUTPUT_DIR!
) else (
    echo        No previous build to clean.
)
echo.

REM Step 2: Build Release
echo [2/3] Building Release ^| Any CPU...
call "!MSBUILD!" "!SOLUTION_FILE!" /t:Build /p:Configuration=Release /p:Platform="Any CPU" /verbosity:minimal
if !ERRORLEVEL! NEQ 0 (
    echo.
    echo [ERROR] Build failed with exit code !ERRORLEVEL!
    pause
    exit /b !ERRORLEVEL!
)
echo        Build succeeded.
echo.

REM Step 3: Copy to ReleasePackage
echo [3/3] Preparing ReleasePackage...
if exist "!PACKAGE_DIR!\" (
    rmdir /s /q "!PACKAGE_DIR!"
)
mkdir "!PACKAGE_DIR!"

REM Copy all build outputs
if exist "!OUTPUT_DIR!\*" (
    xcopy "!OUTPUT_DIR!\*" "!PACKAGE_DIR!\" /e /y /q
    echo        Copied build output to ReleasePackage.
) else (
    echo [WARNING] No build output found at !OUTPUT_DIR!
)

REM Copy bundled video tools from the legacy MapEditor until MapEditorTool owns its own tool drop.
for %%F in (ffmpeg.exe ffprobe.exe ffplay.exe) do (
    if exist "!LEGACY_MAPEDITOR_DIR!\%%F" (
        copy /y "!LEGACY_MAPEDITOR_DIR!\%%F" "!PACKAGE_DIR!\%%F" >nul
        echo        Copied: %%F
    ) else (
        echo [WARNING] Missing bundled video tool: %%F
    )
)

echo.
echo ========================================
echo   Build Complete ^!
echo   Output: !PACKAGE_DIR!
echo ========================================

endlocal
exit /b 0
