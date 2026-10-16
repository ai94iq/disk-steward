@echo off
setlocal EnableExtensions
cd /d "%~dp0"

rem Usage: logs.bat <script> [error|all] [--no-pause]
rem   <script> = log name under artifacts\logs without extension (test, build, package)
rem   default kind: error. Prints matching lines only; "all" types the whole file.

if "%~1"=="" goto :usage
set "LOG=artifacts\logs\%~1.log"
set "KIND=%~2"
if "%KIND%"=="" set "KIND=error"
if not exist "%LOG%" (
    echo [logs] no such log: %LOG%
    if /i not "%~3"=="--no-pause" pause
    exit /b 1
)

if /i "%KIND%"=="all" type "%LOG%" & goto :end

rem Error and warning lines, plus MSB/CS diagnostics with their file paths.
findstr /i /c:"error" /c:"warning" /c:"failed" "%LOG%"
echo.
echo [logs] full log: %LOG%
goto :end

:usage
echo Usage: logs.bat ^<script^> [error^|all] [--no-pause]
exit /b 1

:end
if /i not "%~3"=="--no-pause" pause
exit /b 0
