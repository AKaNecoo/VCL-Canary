<div align="center">
  <img src="docs/images/logo.png" alt="VCL Canary" width="120">
  <h1>VCL Canary</h1>
  <p>VCL Canary は、VRLearn が開発した <a href="https://github.com/VRLearn-Official/VRCLearn_Launcher">VRCLearn Launcher</a> をベースに、AKaNecoo が独立してメンテナンスしているコミュニティ版です。Windows で VRChat プロジェクトと VPM パッケージを管理できます。</p>

  [ダウンロード](https://github.com/AKaNecoo/VCL-Canary/releases) · [简体中文](README.md) · [English](README.en.md) · [不具合報告](https://github.com/AKaNecoo/VCL-Canary/issues)
</div>

<p align="center">
  <a href="https://github.com/AKaNecoo/VCL-Canary/actions/workflows/fork-release.yml"><img src="https://github.com/AKaNecoo/VCL-Canary/actions/workflows/fork-release.yml/badge.svg" alt="ビルド"></a>
  <a href="https://github.com/AKaNecoo/VCL-Canary/releases"><img src="https://img.shields.io/github/v/release/AKaNecoo/VCL-Canary?display_name=tag&sort=semver" alt="リリース"></a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%2B%20x64-0078D4" alt="対応 OS">
  <a href="LICENSE"><img src="https://img.shields.io/github/license/AKaNecoo/VCL-Canary" alt="ライセンス"></a>
</p>

VCL Canary では、VPM リポジトリ、パッケージ、Unity プロジェクトをまとめて管理できます。C# と WPF で開発された独立したアプリケーションで、VRChat Inc. および Unity Technologies との提携はありません。

> [!WARNING]
> すでに多数のプロジェクトを管理している場合は、本ツールへの移行をおすすめしません。公式の VCC や ALCOM など、現在お使いのツールを引き続きご利用ください。

## スクリーンショット

<p align="center">
  <img src="docs/images/screenshots-en.png" alt="プロジェクト一覧と詳細画面（英語表示）">
</p>

## 主な機能

- **リポジトリ** — VPM リポジトリの追加・管理、パッケージの検索・インストール・更新、依存関係の解決
- **プロジェクト** — 一覧表示、新規作成、お気に入り、アーカイブ。指定したバージョンの Unity で起動。バックアップ、復元、エクスポート
- **試験的なアーカイブ** — 「設定 → 試験的な機能」で「外部ローカルパッケージも含めてアーカイブ」を有効にすると、アーカイブの確認画面でそのオプションを選べます。対象パッケージを事前に確認でき、アーカイブの検証後に元のプロジェクトフォルダーを削除します。
- **リソース** — ローカルの `.unitypackage` ファイルを管理。サムネイル、タグ、検索、説明書に対応
- **ランチャーのバックアップ** — 設定のみ、またはデータを含む全体のバックアップを作成し、指定したフォルダーに復元
- **テンプレート** — `.vrclearn-template` 形式のプロジェクトテンプレートをインポート・作成
- **プラグイン** — 「プラグインを探す」から対応する拡張機能を探してインストールできます。ローカルの `.vclplugin` ファイルの選択やドラッグ＆ドロップにも対応します。新しくインストールしたプラグインは手動で有効にします。[プラグインガイド](docs/plugins.md)
- **Windows との連携** — `vcc://` のディープリンクとファイルの関連付け
- **ランチャーの更新** — 選択したチャンネルの更新を確認し、アプリ内でダウンロードやインストールができます。詳しくは[更新方法](#更新方法)を参照してください。
- **表示言語** — 簡体字中国語、英語、日本語。カスタム言語パックにも対応

## インストール

[GitHub Releases](https://github.com/AKaNecoo/VCL-Canary/releases) から `VRCLearnLauncher-<バージョン>-win-x64-Setup.exe` をダウンロードして実行します。

- Windows 10 以降（x64）
- .NET 10 デスクトップランタイムはインストーラーに含まれます。

インストーラーにはコード署名がないため、初回実行時に Windows SmartScreen の警告が表示される場合があります。「詳細情報」を選択してから「実行」を押してください。

## 更新方法

「設定 → このアプリについて」から、更新の確認や更新チャンネルの変更ができます。起動時の自動確認では、過去 24 時間以内に成功した確認結果を再利用する場合があります。手動で確認するとすぐに再チェックします。

- **更新チャンネル**。安定版では安定版のみ、Beta では Beta と安定版、Alpha では 3 チャンネルすべての更新を受け取れます。インストール済みのバージョンより新しい更新が対象です。
- **アプリ内でのインストール**。「自動更新」を有効にすると、アプリ内でインストールできる更新が自動でダウンロードされ、ランチャーの次回起動時にインストールされます。ダウンロード後に「今すぐ更新」を選ぶこともできます。
- **ブラウザーからのダウンロード**。「公式版」を選んで「更新をダウンロード」を押すと、公式ダウンロードページが開きます。表示言語が中国語で、アプリ内にプロキシを設定していない場合は、VCL Canary のリリースページが開きます。アプリ内でのダウンロードや検証に失敗した場合は、「手動ダウンロード」から対象バージョンの GitHub リリースページを開けます。

## ビルド

必要な環境

- Windows 10 以降（x64）
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

依存関係を復元し、アプリケーションをビルドします。

```powershell
dotnet restore src/VRCLearn.App/VRCLearn.App.csproj
dotnet build src/VRCLearn.App/VRCLearn.App.csproj -c Release
```

ソースから起動する場合は次のコマンドを実行します。

```powershell
dotnet run --project src/VRCLearn.App/VRCLearn.App.csproj
```

配布ビルドは `net10.0-windows` / `win-x64` を対象とする自己完結型です。

GitHub Actions は `dev` へのプッシュで Alpha、`beta` へのプッシュで Beta を公開します。安定版は保護された `main` から手動で公開します。インストーラーと SHA256 チェックサムファイルは各リリースに添付されます。

製品のバージョン番号は `1.35.109` のように 3 つの数値で表します。先頭の 2 つは上流と共通で、最後の数値は Alpha、Beta、安定版で共通の連番です。リリースタグやインストーラーのファイル名では、プレリリースに `-alpha` または `-beta` が付きます。

`1.35.108-alpha.8` など旧形式のタグが付いた VCL Canary から更新する場合は、[VCL Canary のリリースページ](https://github.com/AKaNecoo/VCL-Canary/releases)からインストーラーをダウンロードし、一度上書きインストールしてください。その後はアプリ内で更新できます。

## プロジェクト構成

| パス | 内容 |
| --- | --- |
| `src/VRCLearn.App` | WPF の画面、ビューモデル、ローカライズ、テーマ |
| `src/VRCLearn.Core` | プラットフォームに依存しない処理。基本クラスライブラリ（BCL）のみ使用 |
| `src/VRCLearn.Infrastructure` | Windows API、ファイルシステム、HTTP、データ保存 |
| `src/VRCLearn.Plugin.Sdk` | プラグイン用インターフェース。プラグインが参照する唯一のアセンブリ |
| `plugin-template` | コピーして使えるプラグイン用のプロジェクトテンプレート |

依存関係は `App` → `Infrastructure` → `Core` の順です。プラグインは `VRCLearn.Plugin.Sdk` のみを参照します。パッケージ化の方法は [プラグイン開発ガイド](docs/plugins.md) を参照してください。

## データフォルダー

データは `%LocalAppData%\VRCLearnLauncher` に保存されます。設定は `config\`、データは `data\`、ログは `logs\` にあります。初回起動時に旧バージョンの設定を自動で移行します。

## ライセンス

[MIT ライセンス](LICENSE)。サードパーティーのコンポーネントとライセンスは [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) を参照してください。
