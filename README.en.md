<div align="center">
  <img src="docs/images/logo.png" alt="VCL Canary" width="120">
  <h1>VCL Canary</h1>
  <p>VCL Canary is an independently maintained community fork of <a href="https://github.com/VRLearn-Official/VRCLearn_Launcher">VRCLearn Launcher</a>, originally developed by VRLearn. VCL Canary is a VRChat project and VPM package manager for Windows, built with .NET 10 and WPF.</p>

  [Download](https://github.com/AKaNecoo/VCL-Canary/releases) · [简体中文](README.md) · [日本語](README.ja.md) · [Issues](https://github.com/AKaNecoo/VCL-Canary/issues)
</div>

<p align="center">
  <a href="https://github.com/AKaNecoo/VCL-Canary/actions/workflows/fork-release.yml"><img src="https://github.com/AKaNecoo/VCL-Canary/actions/workflows/fork-release.yml/badge.svg" alt="Build"></a>
  <a href="https://github.com/AKaNecoo/VCL-Canary/releases"><img src="https://img.shields.io/github/v/release/AKaNecoo/VCL-Canary?display_name=tag&sort=semver" alt="Release"></a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%2B%20x64-0078D4" alt="Platform">
  <a href="LICENSE"><img src="https://img.shields.io/github/license/AKaNecoo/VCL-Canary" alt="License"></a>
</p>

VCL Canary manages VPM repositories, packages and Unity projects in one place. It is an independent C#/WPF application, not affiliated with VRChat Inc. or Unity Technologies.

> [!WARNING]
> If you have already built up a large number of existing projects, we do not recommend switching to this tool. Please keep using the official VCC, ALCOM or similar tools.

## Screenshots

<p align="center">
  <img src="docs/images/screenshots-en.png" alt="Projects and project detail">
</p>

## Features

- **Repositories** — add VPM repositories, search packages, install and upgrade with dependency resolution
- **Projects** — list, create, favorite and archive projects; launch Unity with a selected editor version; back up, restore and export
- **Resources** — manage local unitypackages with covers, tags, search and manuals
- **Launcher backup** — back up configuration only or everything, restore into a chosen directory
- **Templates** — import and create `.vrclearn-template` projects
- **Plugins** — install `.vclplugin` extensions from local files or by drag-and-drop. Plugins are provided by the community and are not bundled (see [docs/plugins.md](docs/plugins.md))
- **Desktop integration** — `vcc://` deep links and file associations
- **Launcher updates** — check the selected channel and download or install updates in the app. See [Updating](#updating).
- **Localization** — Simplified Chinese, English, and Japanese, with custom language packs

## Install

Download `VRCLearnLauncher-<version>-win-x64-Setup.exe` from [Releases](https://github.com/AKaNecoo/VCL-Canary/releases) and run it.

- Windows 10 or later, x64
- The .NET 10 desktop runtime is bundled; no separate installation is required

The installer is not code-signed yet, so Windows SmartScreen may warn on first run. Choose "More info" and then "Run anyway".

## Updating

Open **Settings → About** to check for updates or choose an update channel. The automatic check at startup can reuse a successful result from the past 24 hours. A manual check looks again immediately.

- **Channels.** Stable receives stable releases, Beta receives beta and stable releases, and Alpha receives releases from all three channels. The launcher only offers versions newer than the one installed.
- **In-app installation.** With **Automatic updates** enabled, eligible releases download automatically and install the next time the launcher opens. Once downloaded, you can also select **Update now**.
- **Browser downloads.** When **Official** is selected, **Download update** opens the official download page. With a Chinese display language and no proxy configured in the app, the button opens the VCL Canary releases page. If in-app download or verification fails, select **Download manually** to open that version’s GitHub release.

## Build

Requirements:

- Windows 10 or later, x64
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

Restore and build the application project:

```
dotnet restore src/VRCLearn.App/VRCLearn.App.csproj
dotnet build src/VRCLearn.App/VRCLearn.App.csproj -c Release
```

Run from source:

```
dotnet run --project src/VRCLearn.App/VRCLearn.App.csproj
```

Published builds target `net10.0-windows` / `win-x64` and are self-contained.

GitHub Actions publishes Alpha from `dev` and Beta from `beta` on push. Stable releases from protected `main` are published manually. VCL Canary keeps the upstream major and minor numbers. Alpha, Beta, and stable share one increasing patch sequence. The product version has three parts, such as `1.35.109`. Release tags and installer filenames use names such as `1.35.109-alpha`, `1.35.110`, and `1.35.111-beta`.

If you have an older VCL Canary build with a tag such as `1.35.108-alpha.8`, download the new installer and run it over your existing installation once. In-app updates are available after that upgrade.

## Project layout

| Path | Contents |
|---|---|
| `src/VRCLearn.App` | WPF application: views, view models, localization, themes |
| `src/VRCLearn.Core` | Platform-independent logic, BCL only |
| `src/VRCLearn.Infrastructure` | Windows APIs, file system, HTTP, storage |
| `src/VRCLearn.Plugin.Sdk` | Plugin contracts; the only assembly plugins compile against |
| `plugin-template` | Copyable plugin project template |

Dependency direction: `App` → `Infrastructure` → `Core`; plugins → `VRCLearn.Plugin.Sdk` only. Plugin packaging is documented in [docs/plugins.md](docs/plugins.md).

## Data directory

Data lives in `%LocalAppData%\VRCLearnLauncher`: configuration in `config\`, data in `data\`, and logs in `logs\`. The first run automatically migrates configuration from older versions.

## License

[MIT](LICENSE). Third-party components and their licenses are listed in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).

