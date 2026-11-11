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

从上游 1.36.0 起，第三段显示为至少三位，001–099 对应该上游版本的构建 01–99，例如 `1.36.001-alpha`、`1.36.002-beta`、`1.36.003`。上游补丁版本递增时使用下一段百位区间，例如基于 1.36.1 的 `1.36.101`。三个渠道共用序列，失败运行也占用编号。已有版本保留。Windows 程序集的数字段会去除前导零，`1.36.001` 与 `1.36.1` 按相同数值比较。

上流 1.36.0 以降は最後の数値を最低 3 桁で表示し、001–099 がその上流バージョンのビルド 01–99 に対応します。例は `1.36.001-alpha`、`1.36.002-beta`、`1.36.003` です。上流のパッチ番号が上がると次の 100 番台を使用します。上流 1.36.1 の最初の例は `1.36.101` です。3 チャンネルで連番を共有し、失敗した実行も番号を消費します。既存のリリースは保持します。Windows アセンブリの数値フィールドでは先頭のゼロが省略され、`1.36.001` と `1.36.1` は同じ数値として比較されます。

Starting with upstream 1.36.0, the patch component is displayed with at least three digits. Values 001–099 represent builds 01–99 of that upstream version, such as `1.36.001-alpha`, `1.36.002-beta`, and `1.36.003`. An upstream patch increment moves to the next block of 100, such as `1.36.101` for upstream 1.36.1. All channels share the sequence, including failed runs. Existing releases are retained. Windows assembly numeric fields omit leading zeros, so `1.36.001` and `1.36.1` compare as the same version.
