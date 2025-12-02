@echo off
set DOTNET_ENVIRONMENT=Development
echo Building Gateway... > gateway_log.txt
dotnet build >> gateway_log.txt 2>&1
if %errorlevel% neq 0 (
    echo Build failed >> gateway_log.txt
    exit /b %errorlevel%
)
echo Build success. Running Gateway... >> gateway_log.txt
bin\Debug\net10.0\yQuant.App.BrokerGateway.exe >> gateway_log.txt 2>&1
