<div align="center">
  <img src="docs/images/logo.png" alt="VCL Canary" width="120">
  <h1>VCL Canary</h1>
  <p>适用于 Windows 的 VRChat 项目与 VPM 软件包管理器。基于 VRLearn 开发的 <a href="https://github.com/VRLearn-Official/VRCLearn_Launcher">VRCLearn Launcher</a>，由 AKaNecoo 独立维护。</p>

  **[下载正式版](https://github.com/AKaNecoo/VCL-Canary/releases/latest)** · [查看 Beta / Alpha](https://github.com/AKaNecoo/VCL-Canary/releases) · [问题反馈](https://github.com/AKaNecoo/VCL-Canary/issues)

  [English](README.en.md) · [日本語](README.ja.md)
</div>

## VCL Canary 的特色

- **项目软件包管理**　可安装的软件包默认显示在已安装的软件包下方。两组列表可分别搜索和相互跳转。支持批量升级到较新的稳定版，或按当前锁定版本重新安装有来源的软件包。
- **应用内更新**　可订阅正式版、Beta 或 Alpha。支持应用内安装的更新下载完成后，可立即安装或留到下次启动时安装。

也支持管理 VPM 仓库和软件包依赖、新建和启动 Unity 项目、备份与还原、管理本地 `.unitypackage`、使用项目模板、安装本地插件和接收 `vcc://` 链接。界面支持简体中文、英文和日文。

### Alpha 试用

- **应用内插件市场**　从「插件 → 浏览插件」搜索扩展、查看兼容性并下载安装。
- **归档时包含外部本地软件包（实验性）**　在设置中启用后，可在归档时预览并打包项目直接引用的外部本地 UPM 目录包或 `.tgz` 包。归档校验通过后才会移除原项目目录。

## 截图

![VCL Canary 正式版的项目详情，显示软件包搜索、批量操作和可安装的软件包](docs/images/screenshots-zh.png)

## 安装与更新

从[最新正式版](https://github.com/AKaNecoo/VCL-Canary/releases/latest)下载 `VRCLearnLauncher-<版本>-win-x64-Setup.exe`。适用于 Windows 10 或更高版本的 x64 系统，安装包内置 .NET 10 桌面运行时。安装包暂未签名，Windows SmartScreen 可能提示。发布页附有 SHA256 校验文件。

已有项目迁移前，请先备份项目和启动器配置，再用一个项目确认 Unity 版本和软件包依赖。从旧格式版本（如 `1.35.108-alpha.8`）升级到当前 Canary，请下载新版安装包并覆盖安装一次，之后可继续使用应用内更新。

在「设置 → 关于」检查更新、选择渠道或开启自动更新。启动时可复用 24 小时内的成功检查结果，手动检查会立即重查。正式版渠道接收正式版，Beta 渠道也接收 Beta，Alpha 渠道接收全部三个渠道。可用更新须比当前版本更新，并与已安装构建兼容。开启自动更新后，符合应用内安装条件的更新会自动下载，在下次启动时安装。

- 选择「官方版」会关闭自动更新。「下载更新」打开官方版下载页。
- 使用中文界面且未配置应用代理时，Canary 渠道的「下载更新」打开本仓库发布页。
- 应用内下载或校验失败时，可用「手动下载」打开目标版本的发布页。

配置、数据和日志分别位于 `%LocalAppData%\VRCLearnLauncher` 下的 `config\`、`data\` 和 `logs\`。首次运行会尝试迁移旧版配置。

## 开发与贡献

[源码构建与项目结构](docs/development.md) · [插件开发指南](docs/plugins.md)

## 许可

[MIT](LICENSE) · [第三方许可](THIRD-PARTY-NOTICES.txt)

VCL Canary 是独立维护的社区分支，与 VRChat Inc. 和 Unity Technologies 无隶属关系。
