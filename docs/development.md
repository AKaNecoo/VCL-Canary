# 开发指南 / 開発ガイド / Development guide

[简体中文](../README.md) · [日本語](../README.ja.md) · [English](../README.en.md)

## 构建与运行 / ビルドと実行 / Build and run

在 Windows 10 或更高版本的 x64 系统上安装 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)，从仓库根目录运行以下命令。

Windows 10 以降の x64 環境に [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) をインストールし、リポジトリのルートから以下のコマンドを実行します。

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) on Windows 10 or later on x64 and run these commands from the repository root.

```powershell
dotnet restore src/VRCLearn.App/VRCLearn.App.csproj
dotnet build src/VRCLearn.App/VRCLearn.App.csproj -c Release
dotnet run --project src/VRCLearn.App/VRCLearn.App.csproj
```

## 项目结构 / プロジェクト構成 / Project layout

| 路径 / パス / Path | 简体中文 | 日本語 | English |
| --- | --- | --- | --- |
| `src/VRCLearn.App` | WPF 界面、视图模型、主题与本地化 | WPF 画面、ビューモデル、テーマ、ローカライズ | WPF UI, view models, themes, and localization |
| `src/VRCLearn.Core` | 与平台无关的业务逻辑 | プラットフォーム非依存のロジック | Platform-independent application logic |
| `src/VRCLearn.Infrastructure` | Windows API、文件系统、HTTP 和存储 | Windows API、ファイルシステム、HTTP、ストレージ | Windows APIs, filesystem, HTTP, and storage |
| `src/VRCLearn.Plugin.Sdk` | 插件接口 | プラグインのインターフェース | Plugin contracts |
| `src/VRCLearn.UpdateHelper` | 启动器更新助手 | ランチャー更新用ヘルパー | Launcher update helper |
| `plugin-template` | 可复制的插件模板 | プラグインのひな型 | Starter template for plugins |
| `build` | 安装包构建文件 | インストーラーのビルド用ファイル | Installer build files |

`App` → `Infrastructure` → `Core` · [插件开发 / プラグイン開発 / Plugin development](plugins.md)

## 发布流程 / リリース / Releases

| 分支 / ブランチ / Branch | 渠道 / チャンネル / Channel | 触发 / トリガー / Trigger |
| --- | --- | --- |
| `dev` | Alpha | 推送 / プッシュ / Push |
| `beta` | Beta | 推送 / プッシュ / Push |
| `main` | 正式版 / 安定版 / Stable | 手动 / 手動 / Manual |

[发布工作流 / リリースワークフロー / Release workflow](../.github/workflows/fork-release.yml) · [版本系列配置 / バージョン系列の設定 / Version series](../.github/canary-build-series.json)

发布使用自包含的 `net10.0-windows` / `win-x64` 构建。三个渠道共用第三段的递增序列，产品版本为三段数字。预发布标签及安装包文件名带 `-alpha` 或 `-beta`。发布页提供安装包和 SHA256 校验文件。

リリースには自己完結型の `net10.0-windows` / `win-x64` ビルドを使用します。製品バージョンは 3 つの数値で表し、3 チャンネルで最後の数値の連番を共有します。プレリリースのタグとインストーラーのファイル名には `-alpha` または `-beta` が付きます。各リリースにインストーラーと SHA256 チェックサムファイルが添付されます。

Releases use self-contained `net10.0-windows` / `win-x64` builds. The three channels share one increasing patch sequence and use three-part product versions. Pre-release version labels include `-alpha` or `-beta` in tags and installer filenames. Each release includes the installer and a SHA256 checksum file.
