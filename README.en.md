<div align="center">
  <img src="docs/images/logo.png" alt="VCL Canary" width="120">
  <h1>VCL Canary</h1>
  <p>A Windows manager for VRChat projects and VPM packages. Based on <a href="https://github.com/VRLearn-Official/VRCLearn_Launcher">VRCLearn Launcher</a> by VRLearn and maintained independently by AKaNecoo.</p>

  **[Download stable](https://github.com/AKaNecoo/VCL-Canary/releases/latest)** · [Browse Beta / Alpha](https://github.com/AKaNecoo/VCL-Canary/releases) · [Report an issue](https://github.com/AKaNecoo/VCL-Canary/issues)

  [简体中文](README.md) · [日本語](README.ja.md)
</div>

## VCL Canary highlights

- **Project packages.** Available packages appear below installed packages by default. Search each list separately or jump between them. Upgrade packages to newer stable versions in bulk, or reinstall packages with an available source at their locked versions.
- **In-app updates.** Subscribe to the stable, Beta, or Alpha channel. Once an eligible update has downloaded, install it right away or on the next launch.

VCL Canary also manages VPM repositories and dependencies, creates and opens Unity projects, backs up and restores projects, organizes local `.unitypackage` files, supports project templates and local plugins, and handles `vcc://` links. The interface is available in Simplified Chinese, English, and Japanese.

### Try in Alpha

- **In-app plugin catalog.** Open **Plugins → Browse plugins** to find extensions, check compatibility, and download and install them.
- **Archive with external local packages (experimental).** Enable the feature in Settings to preview and include directly referenced local UPM package directories or `.tgz` packages in an archive. The launcher removes the original project directory after verifying the archive.

## Screenshot

![VCL Canary stable project details showing package search, bulk actions, and available packages](docs/images/screenshots-en.png)

## Install and update

Download `VRCLearnLauncher-<version>-win-x64-Setup.exe` from the [latest stable release](https://github.com/AKaNecoo/VCL-Canary/releases/latest). It supports Windows 10 or later on x64 and bundles the .NET 10 desktop runtime. The installer is unsigned, so Windows SmartScreen may show a warning. The release includes a SHA256 checksum file.

Before migrating existing projects, back up your projects and launcher configuration. Start with one project to confirm its Unity version and package dependencies. If you are updating from an older Canary version such as `1.35.108-alpha.8`, run the current installer over your existing installation once. In-app updates are available after that upgrade.

Open **Settings → About** to check for updates, choose a channel, or enable automatic updates. Startup checks may reuse a successful result from the past 24 hours. A manual check runs immediately. The stable channel receives stable releases, Beta also receives Beta releases, and Alpha receives all three. Available updates must be newer than and compatible with the installed build. With automatic updates on, eligible installers download automatically and run on the next launch.

- Selecting **Official** turns off automatic updates. **Download update** opens the official download page.
- With a Chinese display language and no proxy configured in the app, **Download update** for Canary channels opens this repository’s releases page.
- If an in-app download or verification fails, **Download manually** opens the release page for that version.

Configuration, data, and logs are stored in `config\`, `data\`, and `logs\` under `%LocalAppData%\VRCLearnLauncher`. The launcher attempts to migrate older configuration on first run.

## Development and contributions

[Build from source and project layout](docs/development.md) · [Plugin development guide](docs/plugins.md)

## License

[MIT](LICENSE) · [Third-party notices](THIRD-PARTY-NOTICES.txt)

VCL Canary is an independently maintained community fork. It has no affiliation with VRChat Inc. or Unity Technologies.
