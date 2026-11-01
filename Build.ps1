param([switch]$RestoreSdk)
$ErrorActionPreference = 'Stop'
$version = '1.0.4258.31'
$sdk = Join-Path $PSScriptRoot '.sdk'
if ($RestoreSdk) {
    New-Item -ItemType Directory -Path $sdk -Force | Out-Null
    $archive = Join-Path $sdk 'webview2.zip'
    Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$version/microsoft.web.webview2.$version.nupkg" -OutFile $archive
    Expand-Archive -LiteralPath $archive -DestinationPath $sdk -Force
}
$core = Join-Path $sdk 'lib\net462\Microsoft.Web.WebView2.Core.dll'
$forms = Join-Path $sdk 'lib\net462\Microsoft.Web.WebView2.WinForms.dll'
$loader = Join-Path $sdk 'runtimes\win-x64\native\WebView2Loader.dll'
if (!(Test-Path $core)) { throw 'Run .\Build.ps1 -RestoreSdk once to download the pinned Microsoft WebView2 SDK from NuGet.' }
$build = Join-Path $PSScriptRoot 'build'
$web = Join-Path $build 'web'
New-Item -ItemType Directory -Path $web -Force | Out-Null
Copy-Item $core,$forms,$loader -Destination $build -Force
Copy-Item (Join-Path $PSScriptRoot 'index.html'),(Join-Path $PSScriptRoot 'style.css'),(Join-Path $PSScriptRoot 'app.js') -Destination $web -Force
if (Test-Path (Join-Path $PSScriptRoot 'enum-labels.tsv')) { Copy-Item (Join-Path $PSScriptRoot 'enum-labels.tsv') -Destination $build -Force }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.Web.Extensions.dll "/reference:$core" "/reference:$forms" "/out:$build\CrownWorkshop.exe" "$PSScriptRoot\CrownSave.cs" "$PSScriptRoot\Desktop.cs"
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Host "Built $build\CrownWorkshop.exe"
