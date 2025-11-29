# load-env.ps1
# PowerShell script to load environment variables from .env.local
# Usage: . .\load-env.ps1

param([string]$EnvFile = ".env.local")

if (Test-Path $EnvFile) {
    Write-Host "Loading environment variables from $EnvFile..." -ForegroundColor Cyan
    
    Get-Content $EnvFile | ForEach-Object {
        # Skip comments and empty lines
        if ($_ -match '^([^=#]+)=(.*)$') {
            $key = $matches[1].Trim()
            $value = $matches[2].Trim()
            
            # Remove quotes if present
            $value = $value -replace '^["'']|["'']$', ''
            
            Write-Host "  ✓ $key" -ForegroundColor Gray
            [Environment]::SetEnvironmentVariable($key, $value, 'Process')
        }
    }
    
    Write-Host "`nEnvironment variables loaded successfully!" -ForegroundColor Green
    Write-Host "You can now run: dotnet run --project src/03.Applications/[YourApp]" -ForegroundColor Yellow
} else {
    Write-Host "Error: $EnvFile not found!" -ForegroundColor Red
    Write-Host "Please create $EnvFile from .env.example template" -ForegroundColor Yellow
    exit 1
}
