@echo off
setlocal

echo =======================================================
echo          LidDock Installer Builder
echo =======================================================
echo.

set "ISCC_PATH="

where iscc >nul 2>&1
if %errorlevel% equ 0 (
    for /f "delims=" %%i in ('where iscc') do set "ISCC_PATH=%%i"
)

if "%ISCC_PATH%"=="" if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" (
    set "ISCC_PATH=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
)

if "%ISCC_PATH%"=="" if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" (
    set "ISCC_PATH=%ProgramFiles%\Inno Setup 6\ISCC.exe"
)

if "%ISCC_PATH%"=="" if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" (
    set "ISCC_PATH=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
)

if "%ISCC_PATH%"=="" (
    echo Error: Inno Setup 6 compiler ISCC.exe was not found.
    echo Please install Inno Setup 6 from https://jrsoftware.org/isinfo.php
    exit /b 1
)

set "stagingDir=publish\staging"
if exist "%stagingDir%" rd /s /q "%stagingDir%"
mkdir "%stagingDir%"

echo [1/3] Compiling optimized UI payload...
dotnet publish src\LidDock.App\LidDock.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=false -o "%stagingDir%"
if %errorlevel% neq 0 (
    echo Error: Failed to compile UI payload.
    exit /b 1
)

if exist "%stagingDir%\LidDock.App.exe" (
    move /y "%stagingDir%\LidDock.App.exe" "%stagingDir%\LidDock.UI.exe" >nul
)

echo [2/3] Compiling Native AOT Daemon...
dotnet publish src\LidDock.Daemon\LidDock.Daemon.csproj -c Release -r win-x64 -o "%stagingDir%\_daemon"
if %errorlevel% neq 0 (
    echo Error: Failed to compile Native AOT Daemon.
    exit /b 1
)

move /y "%stagingDir%\_daemon\LidDock.exe" "%stagingDir%\LidDock.exe" >nul
rd /s /q "%stagingDir%\_daemon" >nul 2>&1
del /f /q "%stagingDir%\*.pdb" >nul 2>&1

echo [3/3] Compiling high-ratio LZMA2 setup package...
"%ISCC_PATH%" "package\LidDock.iss"

if %errorlevel% neq 0 (
    echo.
    echo Error: Inno Setup packaging failed.
    rd /s /q "%stagingDir%" >nul 2>&1
    exit /b 1
)

rd /s /q "%stagingDir%" >nul 2>&1

echo.
echo =======================================================
echo Installer package generated successfully!
echo Location: publish\LidDock-Setup.exe
echo =======================================================
