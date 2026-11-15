$ErrorActionPreference = "Stop"

$Repo = "mequann/omni-dev"
$InstallDir = if ($env:OMNI_INSTALL_DIR) { $env:OMNI_INSTALL_DIR } else { "$env:USERPROFILE\.omni\bin" }
$ArchiveName = "omni-windows-x64.zip"
$DownloadUrl = "https://github.com/$Repo/releases/latest/download/$ArchiveName"

Write-Host "⚡ Installing Omni for Windows (x64)..." -ForegroundColor Cyan

if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

$TempZip = [System.IO.Path]::GetTempFileName() + ".zip"
$TempExtract = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), [System.Guid]::NewGuid().ToString())

try {
    Invoke-WebRequest -Uri $DownloadUrl -OutFile $TempZip -UseBasicParsing
    Expand-Archive -Path $TempZip -DestinationPath $TempExtract -Force
    Copy-Item -Path "$TempExtract\omni.exe" -Destination "$InstallDir\omni.exe" -Force
    Write-Host "✔ Omni installed successfully to $InstallDir\omni.exe" -ForegroundColor Green

    # Check PATH
    $UserPath = [System.Environment]::GetEnvironmentVariable("Path", "User")
    if ($UserPath -notlike "*$InstallDir*") {
        Write-Host "Adding $InstallDir to your User PATH..." -ForegroundColor Yellow
        [System.Environment]::SetEnvironmentVariable("Path", "$UserPath;$InstallDir", "User")
        $env:Path = "$env:Path;$InstallDir"
    }

    Write-Host "`nYou can now run 'omni --help' from any terminal!" -ForegroundColor Cyan
}
finally {
    if (Test-Path $TempZip) { Remove-Item $TempZip -Force -ErrorAction SilentlyContinue }
    if (Test-Path $TempExtract) { Remove-Item $TempExtract -Recurse -Force -ErrorAction SilentlyContinue }
}

