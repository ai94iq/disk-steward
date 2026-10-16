@echo off
setlocal EnableExtensions
cd /d "%~dp0"
set "RESULT=0"

dotnet run --project "src\MoleWin.App\MoleWin.App.csproj" -c Debug -v q
if %ERRORLEVEL% neq 0 set "RESULT=1"

if "%RESULT%"=="1" echo [run] FAILED
if "%RESULT%"=="1" if /i not "%~1"=="--no-pause" pause
exit /b %RESULT%
