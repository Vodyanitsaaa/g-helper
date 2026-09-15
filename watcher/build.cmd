@echo off
cd /d "%~dp0"
echo Building GHelperWatcher (Native Win32 Release)...
cargo build --release
if %ERRORLEVEL% NEQ 0 (
    echo Build failed!
    exit /b %ERRORLEVEL%
)
echo Copying binary to D:\softwares\G-Helper\GHelperWatcher.exe...
copy /y "target\release\GHelperWatcher.exe" "D:\softwares\G-Helper\GHelperWatcher.exe"
echo Done!
