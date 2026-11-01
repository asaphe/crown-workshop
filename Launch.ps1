param([switch]$PrepareOnly, [switch]$AcceptDownload)
$ErrorActionPreference = 'Stop'
try {
    if (![Environment]::Is64BitOperatingSystem) { throw 'Crown Workshop requires 64-bit Windows.' }
    $exe = Join-Path $PSScriptRoot 'build\CrownWorkshop.exe'
    $sdk = Join-Path $PSScriptRoot '.sdk\lib\net462\Microsoft.Web.WebView2.Core.dll'
    $restore = !(Test-Path -LiteralPath $sdk)
    if ($restore -and !$AcceptDownload) {
        Write-Host 'First-time setup downloads the Microsoft WebView2 SDK from NuGet.'
        Write-Host 'It stays in this folder. No Python or system-wide installation is needed.'
        $answer = Read-Host 'Download and prepare Crown Workshop? [Y/N]'
        if ($answer -notmatch '^(y|yes)$') { exit 0 }
    }
    $inputs = 'CrownSave.cs','Desktop.cs','Build.ps1','index.html','app.js','style.css'
    $needsBuild = $restore -or !(Test-Path -LiteralPath $exe)
    if (!$needsBuild) {
        $built = (Get-Item -LiteralPath $exe).LastWriteTimeUtc
        foreach ($file in $inputs) {
            if ((Get-Item -LiteralPath (Join-Path $PSScriptRoot $file)).LastWriteTimeUtc -gt $built) { $needsBuild = $true }
        }
        foreach ($file in 'Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll','web\index.html','web\app.js','web\style.css') {
            if (!(Test-Path -LiteralPath (Join-Path $PSScriptRoot "build\$file"))) { $needsBuild = $true }
        }
    }
    if ($needsBuild) {
        Write-Host 'Preparing Crown Workshop...'
        & (Join-Path $PSScriptRoot 'Build.ps1') -RestoreSdk:$restore
    }
    if (!$PrepareOnly) { Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -WindowStyle Hidden }
    exit 0
} catch {
    Write-Host "Crown Workshop could not start: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host 'For first-time setup, check your internet connection. Close Crown Workshop before updating its files.'
    exit 1
}
