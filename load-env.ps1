# load-env.ps1
# PowerShell script to load environment variables from .env.local
# Usage: . .\load-env.ps1 [-Persistent]

param(
    [string]$EnvFile = ".env.local",
    [Alias("p")]
    [switch]$Persistent
)

if (Test-Path $EnvFile) {
    Write-Host "Loading environment variables from $EnvFile..." -ForegroundColor Cyan
    if ($Persistent) {
        Write-Host "  (Persistent mode enabled)" -ForegroundColor Magenta
    }
    
    Get-Content $EnvFile | ForEach-Object {
        # Skip comments and empty lines
        if ($_ -match '^([^=#]+)=(.*)$') {
            $key = $matches[1].Trim()
            $value = $matches[2].Trim()
            
            # Remove quotes if present
            $value = $value -replace '^["'']|["'']$', ''
            
            # Always set Process scope (Current session)
            [Environment]::SetEnvironmentVariable($key, $value, 'Process')
            $msg = "  ✓ $key"
            
            # If persistent, also set User scope
            if ($Persistent) {
                [Environment]::SetEnvironmentVariable($key, $value, 'User')
                $msg += " (and persisted)"
            }
            
            Write-Host $msg -ForegroundColor Gray
        }
    }
    
    Write-Host "`nEnvironment variables loaded successfully!" -ForegroundColor Green
    if ($Persistent) {
        Write-Host "Variables have also been saved to your User profile." -ForegroundColor Green
    }
    Write-Host "You can now run: dotnet run --project src/03.Applications/[YourApp]" -ForegroundColor Yellow
} else {
    Write-Host "Error: $EnvFile not found!" -ForegroundColor Red
    Write-Host "Please create $EnvFile from .env.example template" -ForegroundColor Yellow
    exit 1
}
