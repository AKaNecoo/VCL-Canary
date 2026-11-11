## 简体中文

- 项目详情采用单一软件包列表与共用搜索框，标题调整为「管理所有软件包」，移除分区跳转按钮。接入安装和升级所选包，保留稳定版全部升级、预发布版升级和全部重装菜单，操作仍先进入变更预览。
- 手动备份增加「着色器缓存」选项。包含 Library 时跳过无法读取的占用文件并显示数量，文件收集与压缩在后台执行。保留自动备份、项目归档及启动器全量备份。
- 新建项目会读取所选 Unity 编辑器的标准软件包清单；缺失时给出明确提示。修复降级软件包时，传递依赖低于项目声明版本所导致的错误冲突。
- 插件有新版本时，将「打开」按钮替换为「更新」。项目刷新进度跟随列表实际重载状态，仓库页的添加、刷新、导入和导出按钮集中排列。
- 详情页包目录构建与 Unity 编辑器扫描转到后台，并采用备份列表虚拟化和仓库缓存改进。版本切换会说明对其他包的影响，可选择单独变更或联动相关包；升级按钮显示目标版本。新增繁体中文界面。

## 日本語

- プロジェクト詳細のパッケージを共通の検索欄がある一つの一覧に統合し、セクション間の移動ボタンを削除しました。選択したパッケージのインストール・更新を追加し、安定版への一括更新、プレリリース版を含む更新、全パッケージの再インストールメニューを維持します。適用前には変更内容をプレビューできます。
- 手動バックアップに「シェーダーキャッシュ」オプションを追加しました。Library を含める場合は、使用中で読み取れないファイルをスキップして件数を表示し、ファイル収集と圧縮をバックグラウンドで実行します。自動バックアップ、プロジェクトのアーカイブ、ランチャーの完全バックアップも維持します。
- 新規プロジェクトでは、選択した Unity エディターの標準パッケージマニフェストを読み込み、見つからない場合は明確なメッセージを表示します。パッケージのダウングレードで間接依存のバージョンがプロジェクトの宣言より低くなる際の誤った競合を修正しました。
- プラグインの新しいバージョンがある場合、「開く」ボタンを「更新」に切り替えます。プロジェクトの更新進捗を一覧の再読み込み状態に合わせ、リポジトリ画面の追加・更新・インポート・エクスポートボタンをまとめました。
- 詳細画面のカタログ構築と Unity エディター検索をバックグラウンドで行い、バックアップ一覧の仮想化とリポジトリキャッシュを改善しました。バージョン変更時には他のパッケージへの影響を表示し、対象のみの変更か関連パッケージも含む変更を選べます。更新ボタンに更新先バージョンを表示し、繁体字中国語の表示を追加しました。

## English

- Combined installed and available packages into one list with a shared search field and removed section navigation buttons. Added install and upgrade actions for selected packages while retaining stable-only upgrades, prerelease upgrades, and reinstall-all options. Operations still go through the change preview before applying.
- Added a shader cache option to manual backups. When including Library, unreadable files in use are skipped and counted, while file collection and compression run in the background. Automatic backups, project archiving, and full launcher backups remain available.
- New projects read the standard package manifest from the selected Unity editor and show a clear message if it is missing. Fixed false conflicts when a package downgrade brings a transitive dependency below the version declared by the project.
- Replaced the plugin Open button with Update when a new version is available. Project refresh progress now follows the actual list reload state, and repository add, refresh, import, and export buttons are grouped together.
- Moved detail-page catalog construction and Unity editor discovery to the background, virtualized backup lists, and improved repository caching. Version changes explain their impact on other packages and let you change only the target or include related packages. Upgrade buttons show the target version. Added Traditional Chinese.
