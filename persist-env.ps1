# persist-env.ps1
# Reads .env.local and sets environment variables PERMANENTLY for the current user.
# You only need to run this once (or whenever .env.local changes).

param([string]$EnvFile = ".env.local")

if (Test-Path $EnvFile) {
    Write-Host "Reading $EnvFile and setting permanent environment variables..." -ForegroundColor Cyan
    
    Get-Content $EnvFile | ForEach-Object {
        # Skip comments and empty lines
        if ($_ -match '^([^=#]+)=(.*)$') {
            $key = $matches[1].Trim()
            $value = $matches[2].Trim()
            
            # Remove quotes if present
            $value = $value -replace '^["'']|["'']$', ''
            
            # Set variable in User scope (Permanent)
            [Environment]::SetEnvironmentVariable($key, $value, 'User')
            Write-Host "  ✓ Set $key (User Scope)" -ForegroundColor Gray
        }
    }
    
    Write-Host "`nEnvironment variables have been set permanently for your user." -ForegroundColor Green
    Write-Host "You may need to restart your terminal/IDE for changes to take effect." -ForegroundColor Yellow
} else {
    Write-Host "Error: $EnvFile not found!" -ForegroundColor Red
    exit 1
}
