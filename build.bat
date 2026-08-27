@echo off
setlocal
set targetProject=src\LidDock.App\LidDock.App.csproj
set outputDir=publish

taskkill /F /IM LidDock.App.exe >nul 2>&1

echo Building LidDock Self-Contained Single-File Application...
dotnet publish %targetProject% -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o %outputDir%

if %ERRORLEVEL% equ 0 (
    echo Build completed successfully. Output executable located at: %outputDir%\LidDock.App.exe
) else (
    echo Build failed with error code %ERRORLEVEL%.
)
endlocal
