@echo off
set DOTNET_ENVIRONMENT=Development
echo Building... > debug_log.txt
dotnet build >> debug_log.txt 2>&1
if %errorlevel% neq 0 (
    echo Build failed >> debug_log.txt
    exit /b %errorlevel%
)
echo Build success. Running... >> debug_log.txt
bin\Debug\net10.0\yQuant.App.Dashboard.exe >> debug_log.txt 2>&1
