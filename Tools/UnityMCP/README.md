# Unity 自动化连接

本工程已安装 Coplay MCP for Unity **10.3.0**，上游固定提交 `ba6881930eadb2a9006d28bff3325ce03b4bfd8a`。

上游源码：https://github.com/CoplayDev/unity-mcp/tree/v10.3.0

## 当前配置

- Unity：2021.3.22f1c1；工程 `G:\Demo\GongkaoMower`。
- 本机 HTTP 地址：`http://127.0.0.1:8767/mcp`。只监听回环地址，不需要云账号/API Key。
- Codex 工程配置：`.codex/config.toml`，服务器名 `unity_gongkaomower`。
- Unity 包：`Tools/UnityMCP/Packages/com.coplaydev.unity-mcp-10.3.0.tgz`，由 `Packages/manifest.json` 引用。保留 tarball，移动工程时应一并携带。
- 服务端、Python 环境、uv、下载缓存、日志位于 `Library/UnityMCPTools`；这些目录已由工程原有 Git 忽略规则排除。
- 自动连接脚本：`Assets/Editor/GongkaoUnityMCPBootstrap.cs`。Unity 导入并编译后自动连接；服务没运行时隐藏启动服务。跳过 batch mode，不改当前场景、游戏 Prefab 或游戏资源。
- 插件和服务端遥测已关闭。没有创建开机任务，没有修改系统 PATH。

插件包含上游 Runtime 辅助程序集；此次未进行微信发布构建，不能据此宣称最终包体增加为零。服务端缓存和 Tools 下的安装归档不在 Assets 内，不作为普通游戏美术资源打包。

## 已验证（2026-10-18）

- Unity 插件和工程 Editor 脚本编译成功。
- Codex CLI 已列出并启用本工程的 `unity_gongkaomower`。
- MCP 握手成功，识别实例 `GongkaoMower@df47de6778f3843d`。
- 从 Unity API 读取工程路径、活动场景及层级成功；活动场景为 `Assets/Scenes/Boot.unity`，未处于运行模式。
- 读取 `CardSelectionPanel.prefab` 信息成功：API 返回 `childCount=51`。
- 读取 `CardView.prefab` 层级成功：14 个对象。
- 截图实际保存成功：`Library/UnityMCPTools/verification/installation-game-view.png`，1080×2160。Boot 场景没有可见 UI，图片为背景色；这只验证截图链路，**不代表技能选卡视觉验收**。
- 只读取控制台，没有清空日志。查询返回两条 HomeTabBar 错误：`CreateTab 无法创建: mode=SceneView, prefab=null, sceneView=null`（178 行）和 `Transform resides in a Prefab asset and cannot be set to prevent data corruption`（133 行）。本次未修复它们，查询结果也不能确定其首次发生时间。

原始验证报告：`Library/UnityMCPTools/verification.json`；连接状态：`Library/UnityMCPTools/editor-connection.json`。

本轮只安装连接工具并读取验证。后续还需要根据设计稿明确设置 UGUI 的 RectTransform、Sprite、TMP 字号/颜色、九宫格和状态，再逐张截图对照；安装 MCP 本身不会自动将效果图转换为一比一界面。

## 日常使用

正常打开工程即可自动连接。Unity 菜单：

- `工具 > Unity 自动化 > 连接本工程`
- `工具 > Unity 自动化 > 暂停自动连接`

本轮客户端已经能通过本机 MCP 操作 Unity。如果当前 Codex 对话的工具列表尚未加载新服务器，可重启 Codex 以加载工程配置；Unity 无需为了这个配置重启。

手动启动本机服务（PowerShell）：

```powershell
& 'G:\Demo\GongkaoMower\Tools\UnityMCP\Start-Server.ps1'
```

只读连接检查：

```powershell
& 'G:\Demo\GongkaoMower\Library\UnityMCPTools\server-venv\Scripts\python.exe' 'G:\Demo\GongkaoMower\Tools\UnityMCP\Inspect-Connection.py'
```

`Verify-Unity.py` 读取当前场景、两个选卡 Prefab 和控制台，并保存当前 Game View 截图；不会打开其他场景或保存 Prefab。

## 删除 Library 或换电脑后

运行 `Install-Dependencies.ps1` 恢复 Python 服务环境，再打开 Unity。脚本当前使用本机 Codex 附带 Python 的绝对路径；换电脑时需将脚本内 `$taskPython` 改为该机器的 Python 3.10+ 路径。服务恢复后，Unity 菜单“连接本工程”可重新连接。

## 停用或卸载

暂停自动连接可通过上述 Unity 菜单完成。完整卸载时，应移除工程 `.codex/config.toml` 中的 `unity_gongkaomower` 段、Package Manager 的 `com.coplaydev.unity-mcp` 依赖、Editor bootstrap 及其 meta。不要用安装前 manifest 整体覆盖后续已添加的其他包。

`manifest.before-unity-mcp.json` 仅为安装时的依赖快照。缓存可以在服务停止后清理。卸载不会要求删除美术资源或重置场景。
