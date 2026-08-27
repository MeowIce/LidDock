@echo off
setlocal
set appProject=src\LidDock.App\LidDock.App.csproj
set daemonProject=src\LidDock.Daemon\LidDock.Daemon.csproj
set resourceDir=src\LidDock.Daemon\Resources
set outputDir=publish

taskkill /F /IM LidDock.exe >nul 2>&1
taskkill /F /IM LidDock.Daemon.exe >nul 2>&1
taskkill /F /IM LidDock.App.exe >nul 2>&1
taskkill /F /IM LidDock.UI.exe >nul 2>&1

if not exist "%resourceDir%" mkdir "%resourceDir%"

echo [1/2] Compiling Fluent WPF UI Payload...
dotnet publish %appProject% -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o "%resourceDir%"
if %ERRORLEVEL% neq 0 (
    echo UI Payload build failed.
    exit /b %ERRORLEVEL%
)

if exist "%resourceDir%\LidDock.App.exe" (
    move /Y "%resourceDir%\LidDock.App.exe" "%resourceDir%\LidDock.UI.exe" >nul
)

echo [2/2] Compiling Native AOT Single-File Executable...
dotnet publish %daemonProject% -c Release -r win-x64 -o %outputDir%
if %ERRORLEVEL% neq 0 (
    echo Native AOT executable build failed.
    exit /b %ERRORLEVEL%
)

echo Cleaning up intermediate build files...
del /F /Q "%resourceDir%\*.*" >nul 2>&1
del /F /Q "%outputDir%\LidDock.Daemon.exe" >nul 2>&1
del /F /Q "%outputDir%\LidDock.App.exe" >nul 2>&1
del /F /Q "%outputDir%\*.pdb" >nul 2>&1
del /F /Q "%outputDir%\*.txt" >nul 2>&1

echo =======================================================
echo Build completed successfully.
echo Single output executable located at:
echo   %outputDir%\LidDock.exe
echo =======================================================
endlocal
