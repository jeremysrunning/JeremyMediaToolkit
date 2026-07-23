# Jeremy Media Toolkit — Native Windows v0.2

A native C#/.NET 8 WPF desktop utility for Jeremy Running Photography video workflows.

## Features

- Folder batch encoding with a dropdown of `.cube` LUTs from a configurable folder
- 1080p, 4K UHD, or source-resolution output
- High-quality NVIDIA NVENC H.264 (`p7`, full-resolution multipass, adaptive quantization)
- Normal, salvage, and video-only recovery modes
- Per-file progress, overall progress, and estimated time remaining
- Optional recursive processing and resume/skip support
- FFprobe metadata inspection
- Full decode verification with a CSV report
- Lossless MP4 rewrapping, 1080p editing proxies, and contact sheets
- Experimental Premiere Pro V1 timeline clip exporter
- Accepts a folder path on the command line for future Explorer integration

## Requirements

- Windows 10/11, 64-bit
- NVIDIA GPU and current NVIDIA driver for NVENC encoding
- FFmpeg containing `h264_nvenc`, plus FFprobe
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

For a self-contained, single-file Windows build:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\publish-self-contained.ps1
```

The output is placed in `publish\JeremyMediaToolkit-win-x64` and does not require a separate .NET runtime.

## FFmpeg setup

Install through Windows Package Manager:

```powershell
winget install Gyan.FFmpeg
```

Open a new PowerShell window and verify:

```powershell
ffmpeg -version
ffmpeg -hide_banner -encoders | Select-String h264_nvenc
```

Alternatively, place `ffmpeg.exe` and `ffprobe.exe` under:

```text
JeremyMediaToolkit.exe
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

- Originals are never overwritten. Encoded files go into a new `Toolkit-*` subfolder.
- The LUT dropdown defaults to `J:\Photography\LUTs`. Choose another LUT folder in the app to save it as the new default, or use **Refresh** after adding LUT files to the current folder.
- 4K output is 3840×2160 with aspect-preserving scale and letterbox/pillarbox padding when required.
- Contact sheets sample one frame every ten seconds and use the first 16 samples.
- v0.2 is source-complete; FFmpeg binaries are intentionally not redistributed.
