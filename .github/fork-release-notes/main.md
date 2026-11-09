## 简体中文

- 「插件」新增「浏览插件」。可搜索扩展、查看兼容性，并在校验通过后于应用内确认安装。插件市场和同站插件包使用直连，也可以从文件安装。
- 「实验性功能」改为可展开的设置，每项功能独立控制。启用外部依赖归档后，可预览并打包项目直接引用的外部本地 UPM 包。归档校验通过后才会移除原项目目录，项目内嵌入式包会随项目保留。
- 改善打开项目页及刷新长列表时的响应。项目、仓库、日志、项目详情和插件页在刷新按钮内显示进度。日志后台读取，仓库内容相同时保留列表和滚动位置。
- 调整更新渠道和自动更新说明的布局，修正保存设置后的更新状态显示。
- 修复覆盖安装后桌面和开始菜单快捷方式有时仍显示旧图标的问题。

**升级说明**　从 VCL Canary 1.35.102 等使用旧标签格式的版本升级时，请下载本页安装包并覆盖安装一次，之后可继续使用应用内更新。

## 日本語

- プラグイン画面に「プラグインを探す」を追加しました。拡張機能を検索して互換性を確認し、ダウンロードの検証後にアプリ内でインストールできます。プラグインマーケットと同じサイトのパッケージには直接接続します。ファイルからのインストールにも対応しています。
- 「実験的な機能」は展開して項目ごとに設定できます。外部依存関係を含める機能を有効にすると、プロジェクトが直接参照する外部ローカル UPM パッケージを確認し、アーカイブに含められます。元のプロジェクトフォルダーはアーカイブの検証後に削除され、埋め込みパッケージはアーカイブ内に保持されます。
- プロジェクト画面を開くときや長い一覧を更新するときの応答を改善しました。プロジェクト、リポジトリ、ログ、プロジェクト詳細、プラグインの更新ボタン内に進行状況が表示されます。ログはバックグラウンドで読み込み、リポジトリの表示内容に変更がない場合は一覧とスクロール位置を維持します。
- 更新チャンネルと自動更新の説明のレイアウトを調整し、設定を保存した後の更新状態の表示を修正しました。
- 上書きインストール後にデスクトップやスタートメニューのショートカットに古いアイコンが残ることがある問題を修正しました。

**更新方法**　VCL Canary 1.35.102 など旧形式のタグが付いたバージョンから更新する場合は、このページのインストーラーで一度上書きインストールしてください。その後はアプリ内で更新できます。

## English

- Added **Browse plugins** to the Plugins page. Find extensions, check compatibility, and confirm installation in the app after the download is verified. The catalog and packages from the same site use a direct connection. You can also install plugins from files.
- Experimental features now have individual switches in an expandable Settings section. Enable external package archiving to preview and include directly referenced local UPM packages. The launcher removes the original project folder after verifying the archive and preserves embedded packages within it.
- Improved responsiveness when opening Projects and refreshing long lists. Refresh indicators now appear inside the buttons on Projects, Repositories, Logs, Project Details, and Plugins. Logs load in the background, and repository lists retain their scroll position when their contents are unchanged.
- Adjusted the layout of update channel and automatic update descriptions, and fixed how update status is displayed after saving settings.
- Fixed desktop and Start menu shortcuts that could still show the old icon after an in-place installation.

**Upgrading**. If you are upgrading from VCL Canary 1.35.102 or another release with an old-format tag, download the installer on this page and run it over your existing installation once. You can then continue updating in the app.
