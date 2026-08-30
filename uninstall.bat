@echo off
setlocal
echo =======================================================
echo              LidDock Clean Uninstaller
echo =======================================================
echo.
echo Terminating running LidDock processes...
taskkill /F /IM LidDock.exe /T >nul 2>&1
taskkill /F /IM LidDock.UI.exe /T >nul 2>&1

if exist "publish\LidDock.exe" (
    "publish\LidDock.exe" --uninstall --silent
) else if exist "LidDock.exe" (
    "LidDock.exe" --uninstall --silent
) else if exist "%APPDATA%\LidDock\LidDock.exe" (
    "%APPDATA%\LidDock\LidDock.exe" --uninstall --silent
) else (
    reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "LidDock" /f >nul 2>&1
    reg delete "HKCU\Software\LidDock" /f >nul 2>&1
    if exist "%LOCALAPPDATA%\LidDock" rd /s /q "%LOCALAPPDATA%\LidDock" >nul 2>&1
    if exist "%APPDATA%\LidDock" rd /s /q "%APPDATA%\LidDock" >nul 2>&1
)

echo.
echo LidDock has been completely removed from this system.
echo Original power scheme and startup settings restored.
echo =======================================================
pause
