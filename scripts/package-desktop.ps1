$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "TILVEX\TILVEX.csproj"
$webDownloads = Join-Path $root "web\private-downloads"
$publish = Join-Path $webDownloads "publish"
$archive = Join-Path $webDownloads "TILVEX-desktop.zip"

if (Test-Path $webDownloads) {
    Remove-Item -LiteralPath $webDownloads -Recurse -Force
}
New-Item -ItemType Directory -Path $webDownloads | Out-Null

dotnet publish $project --configuration Release --runtime win-x64 --self-contained false --output $publish
Compress-Archive -Path (Join-Path $publish "*") -DestinationPath $archive -CompressionLevel Optimal
Remove-Item -LiteralPath $publish -Recurse -Force

Write-Output "Created $archive"
