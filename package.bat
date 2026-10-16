@echo off
setlocal EnableExtensions
cd /d "%~dp0"
set "RESULT=0"

rem Every step's full output lands in artifacts\logs; the screen gets one line per
rem step, or the error lines plus the log path on failure.
if not exist "artifacts\logs" mkdir "artifacts\logs"

call test.bat --no-pause
if %ERRORLEVEL% neq 0 goto :fail
call build.bat --no-pause
if %ERRORLEVEL% neq 0 goto :fail
dotnet build "installer\DiskSteward.Installer.wixproj" -c Release -o "artifacts\installer" -v q -nologo > "artifacts\logs\package.log" 2>&1
if %ERRORLEVEL% neq 0 goto :fail

echo [package] OK: artifacts\installer
goto :end

:fail
set "RESULT=1"
echo [package] FAILED - error lines below, full output in artifacts\logs
call logs.bat package error --no-pause

:end
if /i not "%~1"=="--no-pause" pause
exit /b %RESULT%
