param(
    [string]$Destination = (Join-Path $PSScriptRoot "..\artifacts\ffmpeg"),
    [string]$CacheDirectory = (Join-Path $PSScriptRoot "..\.cache\ffmpeg")
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$manifestPath = Join-Path $repositoryRoot "dependencies\ffmpeg.json"
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$archivePath = Join-Path $CacheDirectory $manifest.archiveName

New-Item -ItemType Directory -Path $CacheDirectory -Force | Out-Null
if (-not (Test-Path -LiteralPath $archivePath)) {
    Write-Host "Downloading pinned FFmpeg $($manifest.version)..." -ForegroundColor Cyan
    Invoke-WebRequest -UseBasicParsing -Uri $manifest.downloadUrl -OutFile $archivePath
}

$actualHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualHash -ne $manifest.sha256.ToLowerInvariant()) {
    throw "FFmpeg checksum mismatch. Expected $($manifest.sha256), received $actualHash."
}

$extractRoot = Join-Path $CacheDirectory "extracted-$($manifest.version)"
if (-not (Test-Path -LiteralPath $extractRoot)) {
    Expand-Archive -LiteralPath $archivePath -DestinationPath $extractRoot -Force
}

$ffmpeg = Get-ChildItem -LiteralPath $extractRoot -Recurse -Filter "ffmpeg.exe" | Select-Object -First 1
$ffprobe = Get-ChildItem -LiteralPath $extractRoot -Recurse -Filter "ffprobe.exe" | Select-Object -First 1
if ($null -eq $ffmpeg -or $null -eq $ffprobe) {
    throw "The verified FFmpeg archive did not contain ffmpeg.exe and ffprobe.exe."
}

New-Item -ItemType Directory -Path (Join-Path $Destination "bin") -Force | Out-Null
Copy-Item -LiteralPath $ffmpeg.FullName -Destination (Join-Path $Destination "bin\ffmpeg.exe") -Force
Copy-Item -LiteralPath $ffprobe.FullName -Destination (Join-Path $Destination "bin\ffprobe.exe") -Force

$licenseFiles = Get-ChildItem -LiteralPath $extractRoot -Recurse -File |
    Where-Object { $_.Name -match '^(LICENSE|COPYING|README)(\..+)?$' }
$licenseDestination = Join-Path $Destination "licenses"
New-Item -ItemType Directory -Path $licenseDestination -Force | Out-Null
foreach ($file in $licenseFiles) {
    Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $licenseDestination $file.Name) -Force
}

$record = @"
FFmpeg distribution record
==========================
Version: $($manifest.version)
License: $($manifest.license)
Binary package: $($manifest.downloadUrl)
Verified SHA-256: $actualHash
Corresponding FFmpeg source: $($manifest.sourceUrl)
Build scripts and configuration: $($manifest.buildProjectUrl)

Lightflow Studio invokes FFmpeg and FFprobe as separate command-line programs.
The included FFmpeg binaries remain licensed by their respective copyright holders.
"@
[IO.File]::WriteAllText((Join-Path $Destination "SOURCE-AND-LICENSE.txt"), $record)
Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $Destination "ffmpeg-package.json") -Force

Write-Host "Verified FFmpeg prepared at: $Destination" -ForegroundColor Green
