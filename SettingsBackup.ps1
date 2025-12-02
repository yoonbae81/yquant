$solutionRoot = Get-Location
$zipPath = Join-Path $solutionRoot "SettingsBackup.zip"

$filePatterns = @(
    "appsettings.Development.json",
    "sharedsettings.Development.json",
    "*.ps1",
    "*.sh"
)

$tempDir = Join-Path $env:TEMP "SettingsBackupTemp"

if (Test-Path $tempDir) {
    Remove-Item -Recurse -Force $tempDir
}
New-Item -ItemType Directory -Path $tempDir | Out-Null

foreach ($pattern in $filePatterns) {
    $files = Get-ChildItem -Path $solutionRoot -Recurse -Filter $pattern -File

    foreach ($file in $files) {
        $relativePath = $file.FullName.Substring($solutionRoot.Path.Length).TrimStart('\', '/')

        $destPath = Join-Path $tempDir $relativePath
        $destDir = Split-Path $destPath -Parent
        if (!(Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }
        Copy-Item -Path $file.FullName -Destination $destPath
    }
}

if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}

Compress-Archive -Path (Join-Path $tempDir '*') -DestinationPath $zipPath

Remove-Item -Recurse -Force $tempDir

Write-Host "Completed: $zipPath"
