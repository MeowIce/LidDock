@echo off
setlocal

echo =======================================================
echo          LidDock Installer Builder
echo =======================================================
echo.

if not exist "publish\LidDock.exe" (
    echo Compiling LidDock.exe release payload...
    call .\build.bat
)

set "ISCC_PATH="

where iscc >nul 2>&1
if %errorlevel% equ 0 (
    for /f "delims=" %%i in ('where iscc') do set "ISCC_PATH=%%i"
)

if "%ISCC_PATH%"=="" if exist "%LOCALAPPDATA%\Programs\Antigravity IDE\resources\app\node_modules\innosetup\bin\ISCC.exe" (
    set "ISCC_PATH=%LOCALAPPDATA%\Programs\Antigravity IDE\resources\app\node_modules\innosetup\bin\ISCC.exe"
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

echo Found Inno Setup Compiler at:
echo   "%ISCC_PATH%"
echo.
echo Compiling LidDock-Setup package...
"%ISCC_PATH%" "package\LidDock.iss"

if %errorlevel% neq 0 (
    echo.
    echo Error: Inno Setup packaging failed.
    exit /b 1
)

echo.
echo =======================================================
echo Installer package generated successfully!
echo Location: publish\LidDock-Setup.exe
echo =======================================================
