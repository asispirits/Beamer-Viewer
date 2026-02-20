# Beamer Viewer

Windows desktop viewer built with WPF (.NET 8) and WebView2.

## Requirements

- .NET SDK 8+
- NuGet access

## Build (Debug)

```bash
dotnet build Beamer_viewer.csproj -nologo
```

## Publish Windows EXE (Release)

```bash
dotnet publish Beamer_viewer.csproj -c Release -r win-x64 --self-contained true -nologo
```

Published output:

`bin/Release/net8.0-windows/win-x64/publish/Beamer_viewer.exe`

## Run on Windows

Copy the `publish` folder to a Windows machine and run:

`Beamer_viewer.exe`

## Notes

- App manifest: `app.manifest`
- Config file template: `Beamerviewer.config.json`
