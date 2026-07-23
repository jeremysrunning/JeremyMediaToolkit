# Lightflow Studio — Native Windows

**Video processing and workflow tools by Jeremy Running Photography.**

Current version: **0.4.0**

Lightflow Studio is a native C#/.NET 8 WPF desktop application for preparing, processing, inspecting, and recovering video media.

## Features

- Folder batch encoding with a collapsed per-file picker, allowing individual videos to be skipped before a batch begins
- Asynchronous file details with resolution, frame rate, duration, size, codec/audio tooltips, and warning badges for media outliers
- Configurable `.cube` LUT library with a friendly selection dropdown
- Dedicated Settings tab for default folders, FFmpeg, batch preferences, and advanced encoding controls
- Named encoding presets with recommended-default restoration and custom overrides
- NVIDIA NVENC H.264/HEVC with quality, bitrate, tuning, multipass, AQ, 8/10-bit, frame-rate, deinterlace, audio, and container controls
- Branded dark-studio interface with card-based workflows and a multi-size Windows application icon
- 1080p, 4K UHD, or source-resolution output
- High-quality NVIDIA NVENC H.264 (`p7`, full-resolution multipass, adaptive quantization)
- Normal, salvage, and video-only recovery modes
- Per-file progress, overall progress, and estimated time remaining
- Remembers the most recently selected LUT and records batch lifecycle summaries
- Protects active encodes with finish-current, close-now, and keep-running close options
- Optional recursive processing and resume/skip support
- FFprobe metadata inspection
- Full decode verification with a CSV report
- Lossless MP4 rewrapping, 1080p editing proxies, and contact sheets
- Experimental Premiere Pro V1 timeline clip exporter
- Accepts a folder path on the command line for future Explorer integration

## Requirements

- Windows 10/11, 64-bit
- NVIDIA GPU and current NVIDIA driver for NVENC encoding
- FFmpeg containing `h264_nvenc` and `hevc_nvenc`, plus FFprobe
- .NET 8 SDK only when building from source

The app searches for FFmpeg in this order:

1. `ffmpeg\bin\ffmpeg.exe` beside the published application
2. Windows `PATH`
3. A location selected with **FFmpeg Settings**

## Build from source

Install the .NET 8 SDK:

```powershell
winget install Microsoft.DotNet.SDK.8
```

Open PowerShell in this folder, then run:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\build.ps1
```

Run the unit test suite with:

```powershell
dotnet test .\LightflowStudio.Tests\LightflowStudio.Tests.csproj
```

## Versioning releases

Lightflow Studio follows semantic versioning: patch releases contain fixes and polish, minor releases add backward-compatible features, and major releases mark breaking changes or major stable milestones.

Change the product version from the repository root before preparing a release:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\set-version.ps1 -Version 0.4.0
```

This updates the authoritative build version and every source-controlled place where the current version is displayed. Automated tests fail if those values drift apart.

For a self-contained, single-file Windows build:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\publish-self-contained.ps1
```

The output is placed in `publish\LightflowStudio-win-x64` and does not require a separate .NET runtime.

## Encoding presets and advanced options

Lightflow Studio ships with four named starting points:

- **Recommended:** H.264 NVENC, P7, constant quality 18, full-resolution multipass, spatial/temporal AQ, and source audio copy.
- **Maximum Quality:** 10-bit HEVC, constant quality 16, full-resolution multipass, and high-bitrate AAC.
- **Fast Preview:** H.264 P4, constant quality 25, quarter-resolution multipass, and lightweight AAC.
- **Efficient HEVC:** HEVC P6, constant quality 21, full-resolution multipass, and AAC.

Settings can customize the codec, container, NVENC preset, tuning, rate-control mode, quality or bitrates, multipass, adaptive quantization, pixel format, frame rate, deinterlacing, audio encoding, sample rate, channels, and fast-start behavior. Invalid combinations are rejected before settings are saved or encoding begins.

The internal settings model reserves CPU, AMD AMF, and Intel Quick Sync backends for future releases. Only NVIDIA NVENC is enabled in this version.
## FFmpeg setup

Install through Windows Package Manager:

```powershell
winget install Gyan.FFmpeg
```

Open a new PowerShell window and verify:

```powershell
ffmpeg -version
ffmpeg -hide_banner -encoders | Select-String "h264_nvenc|hevc_nvenc"
```

Alternatively, place `ffmpeg.exe` and `ffprobe.exe` under:

```text
LightflowStudio.exe
ffmpeg\
  bin\
    ffmpeg.exe
    ffprobe.exe
```

## Recovery modes

- **Normal:** retains all audio streams with stream copy.
- **Salvage audio + video:** discards corrupt packets where possible, rebuilds timestamps, uses the first optional audio stream, and re-encodes it to AAC with async resampling.
- **Video only:** processes the primary video stream and produces no audio.

FFmpeg cannot reconstruct absent media data. Salvage output may contain frozen, duplicated, skipped, silent, or visibly corrupted sections.

## Premiere helper

See `PremiereHelper\README.txt`. Adobe has changed Premiere scripting support over time, so treat this helper as experimental and test it on a duplicate project. An Adobe Media Encoder `.epr` preset is required.

## Notes

- Originals are never overwritten. Encoded files go into a new `Lightflow-*` subfolder. HEVC and non-MP4 jobs receive distinct folder suffixes so skip-existing behavior cannot confuse different output formats.
- The LUT dropdown defaults to `J:\Photography\LUTs`. Choose another LUT folder in Settings, or use **Refresh** after adding LUT files to the current folder.
- Application preferences are saved under `%LOCALAPPDATA%\Jeremy Running Photography\Lightflow Studio\settings.json` and restored at startup.
- 4K output is 3840×2160 with aspect-preserving scale and letterbox/pillarbox padding when required.
- Contact sheets sample one frame every ten seconds and use the first 16 samples.
- FFmpeg binaries are intentionally not redistributed.
