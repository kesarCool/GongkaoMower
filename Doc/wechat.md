好的，项目现状已经很清楚了。以下是完整方案。

---

# 微信小游戏打包实施方案

## 一、总体路线（建议分四步）

| 阶段 | 目标 | 预计耗时 |
|------|------|----------|
| 1. WebGL 测试 | 在浏览器验证打包+运行 | 0.5-1 天 |
| 2. 接入微信 SDK | 安装 WX SDK，替换占位代码 | 0.5 天 |
| 3. 微信小游戏适配 | 分包、API 替换、首包优化 | 1-3 天 |
| 4. 构建流水线 | 一键出包脚本 | 0.5 天 |

---

## 二、第一阶段：WebGL 测试（强烈推荐先做）

**可以而且应该先打 WebGL 版测试**，原因：

- WebGL 和微信小游戏的底层都是 WebAssembly + JS，95% 的兼容问题在 WebGL 阶段就会暴露
- WebGL 迭代快（浏览器 F5 即可），微信小游戏需要上传/扫码/审核
- 微信 SDK 本质是 WebGL 包 + 一层 JS 桥接，WebGL 能跑通基本就能跑通微信

### 操作步骤

1. **File → Build Settings → WebGL → Switch Platform**
2. **Player Settings 关键配置：**
   - `Player → Resolution → WebGL Template`：选 `Minimal`（不要用 Default，太大）
   - `Player → Publishing Settings → Compression Format`：选 `Gzip`（微信支持）
   - `Player → Other Settings → Color Space`：确认用 `Gamma`（微信不支持 Linear）
   - `Player → Other Settings → Strip Engine Code`：勾选
   - `Player → Other Settings → Managed Stripping Level`：`Medium` 或 `High`
3. **首包体积估算：** 看 `Build/` 目录下的 `.wasm` + `.data` + `.js` 总大小。微信限制首包 4MB（分包后可达 20MB+）

### 这个阶段会暴露的问题

- C# 代码中用了不兼容的 API（System.IO.File、Thread、Socket 等）
- 插件/NPOI 在 WebGL 不可用（NPOI 依赖 System.Drawing，WebGL 不可用）
- 资源引用丢失
- Shader 兼容性（微信只支持 WebGL 1.0/2.0 子集）

---

## 三、第二阶段：接入微信 SDK

### 3.1 注册微信小游戏 AppID（必须先做）

没有 AppID 无法导出 minigame 工程。流程如下：

1. 访问 [微信公众平台](https://mp.weixin.qq.com) → 点击「立即注册」
2. 注册类型选 **小游戏**（不是小程序）
3. 填写主体信息（个人需实名认证，企业需营业执照）
4. 注册完成后进入「开发管理」→「开发设置」
5. 复制 **AppID**（形如 `wx1234567890abcdef`）
6. **开通 Unity 适配插件**：MP 后台 → 能力地图 → 生产提效包 → 快适配 → 开通
   - 不开通会在微信开发者工具中报「插件未授权」
7. 在「开发设置」中添加 **服务器域名白名单**（HTTPS + wss，所有网络请求的目标域名必须配置）

> ⚠️ 个人主体注册约 1-3 个工作日审核；企业主体通常 1 个工作日。

### 3.2 安装 SDK

**当前可用的安装方式**（GitHub 原仓库已被禁用 `2026/05`）：

**方式一：Gitee 镜像（推荐，国内最快）**

```
Unity → Window → Package Manager → + → Add package from git URL:
https://gitee.com/wechat-minigame/minigame-unity-webgl-transform.git
```

**方式二：.unitypackage 导入**

```
下载: https://res.wx.qq.com/wechatgame/product/wasm_plugin/minigame.202302151921.unitypackage
Unity → Assets → Import Package → Custom Package → 选择下载的 .unitypackage
```

> 当前使用版本：WX-WASM-SDK v2023.02（.unitypackage 方式导入至 `Assets/WX-WASM-SDK/`）

**SDK 核心能力：**
- `WX` 静态类提供微信 API（`WX.Login`、`WX.GetSystemInfo`、`WX.ExitMiniProgram` 等）
- `WX.LoadFont(path)` 加载自定义字体
- Editor 中模拟微信环境，WebGL 中调用真接口
- `WXEditorWindow.DoExport` 自动构建 WebGL + 导出 minigame 工程
- 自动生成 `game.json`、`project.config.json` 等微信工程文件

> ⚠️ 此版本 **无 `WX.LoadSubpackage` API**。分包由 `game.json` 配置 + 微信运行时自动加载，`WeChatSubpackagePlaceholder` 无需手动调用。

### 3.3 替换占位代码

`WeChatSubpackagePlaceholder.cs` 已经适配完成：

- **Editor**：模拟延迟后直接返回成功
- **WebGL / 微信运行时**：分包由 `game.json` 配置 + 微信运行时自动加载，无需手动调用 API
- **其他平台**：直接返回成功

分包名称 `"battle"` 需与 `game.json` 中的 `subpackages` 配置保持一致（在 WXEditorWindow 中配置或手动编辑）。

---

## 四、第三阶段：微信小游戏适配

### 4.1 分包策略（核心）

微信限制：首包 ≤ 4MB，总包 ≤ 20MB。你的项目需要规划：

| 包 | 内容 | 预估 |
|----|------|------|
| 首包 | 启动场景(Boot/Login)、核心框架、FlatBuffers schema | < 4MB |
| battle 分包 | Game 场景、BattleLoading、Roguelike 逻辑 | - |
| 资源分包 | 地图 Tilemap、字体、大图 | - |

你已有的 `BattleLoadingSceneController` 就是在做分包加载流程，这个架构是正确的。

### 4.2 API 替换清单

| 原 Unity API | 微信替代方案 |
|---|---|
| `Application.persistentDataPath` | `WX.env.USER_DATA_PATH` |
| `PlayerPrefs` | `WX.Storage` 系列 |
| `Application.Quit()` | `WX.ExitMiniProgram()` |
| 网络请求 `UnityWebRequest` | 可用，但推荐 `WX.Request`（绕过域名限制） |
| `Screen.width/height` | `WX.GetSystemInfoSync().screenWidth/Height` |
| `Input.location` | `WX.GetLocation()` |
| 分享/排行榜/广告 | 分别用 `WX.ShareAppMessage` / `WX.SetUserCloudStorage` / `WX.CreateRewardedVideoAd` |

### 4.3 资源和字体

- **TextMeshPro Fallback Font**：微信环境没有系统字体，必须打包中文字体或使用微信提供的系统字体枚举
- **资源加载**：如果你用 Resources，注意 Resources 中的资源默认进首包，会快速占满 4MB。改用 AssetBundle 或 Addressables 配合分包
- **NPOI 插件**：你 `Assets/Plugins/NPOI/` 是 Editor 用的（Excel 转数据），运行时不需要，确保它不被打包进 WebGL

---

## 五、关键坑点汇总

### 坑点 1：首包 4MB 硬限制

这是最容易卡住的地方。建议从一开始就用 Editor 下的 SDK 工具检查每个资源的归属包。解决方法：
- 纹理用 ASTC 格式（微信 iOS/Android 都支持）
- 音频用 `.mp3` 而不是 `.wav`
- C# 代码 `Strip Engine Code: High`
- 在 `game.json` 中配置 `"subpackages"` 把非启动必需的资源全部分出去

### 坑点 2：WebGL 2.0 / Linear Color Space

微信小游戏目前只支持 WebGL 1.0（部分新设备支持 2.0），**不支持 Linear Color Space**。必须用 Gamma。

### 坑点 3：System.IO 不可用

任何 `File.ReadAllText`、`FileStream`、`Directory` 调用在 WebGL 运行时会抛 `PlatformNotSupportedException`。你的 FlatBuffers 加载如果从文件读，需要改为从 `Resources` 或 `AssetBundle` 或 `WX.GetFileSystemManager()` 读取。

### 坑点 4：C# 内存和 GC

WebGL 的 Mono/IL2CPP 环境下，内存碎片问题更严重。GC 触发的卡顿比原生平台明显得多。建议：
- 对象池（你已经用了 `GameObjectPool`，很好）
- 避免在 Update 中分配临时对象
- 使用 `WX.TriggerGC()` 在加载间隙主动回收

### 坑点 5：字体

中文字体是体积大户。TextMeshPro 如果用完整中文字体文件，轻松 10MB+。解决方案：
- 使用微信系统字体（`WX.GetSystemFont()`）
- 或用 TextMeshPro 的 Fallback + 动态 SDF 生成
- 或只打包常用字（~3500 字的 SDF 约 1-2MB）

### 坑点 6：C# 与 JS 互调延迟

`WX.*` 调用都是 C# → JS 桥接，有 ~1-5ms 延迟。批量操作时合并调用，避免在每帧循环中频繁调用。

### 坑点 7：vConsole 调试

真机调试必须用 `vConsole`。在 `game.json` 中临时加入或代码中 `WX.SetEnableDebug({ enableDebug: true })`，但这会增加包体积，正式发布前记得关。

### 坑点 8：域名白名单

所有网络请求的目标域名必须在微信后台配置白名单，且只支持 HTTPS + wss。`UnityWebRequest` 也会受此限制。

---

## 六、构建脚本

### 6.1 SDK 自带导出（推荐日常使用）

菜单：**微信小游戏 / 转换小游戏** → 填写 AppID 等信息 → 点击「导出WebGL并转换为小游戏」

### 6.2 一键构建（批处理 / CI）

`Assets/Editor/WeChatBuild.cs` — 菜单 **Build / 微信小游戏 - 一键构建**，自动配置 PlayerSettings + 调用 SDK `DoExport`。

### 6.3 仅构建 WebGL（不分包、不导出）

菜单 **Build / WebGL - 配置并构建**，输出到 `Build/WebGL/`，可用本地服务器测试。

---

## 七、建议的执行顺序

1. **现在就做**：打一个 WebGL 包，浏览器中能跑通 Boot → Login → Home → Game 流程
2. **安装 WX SDK**，替换 `WeChatSubpackagePlaceholder` 中的 TODO
3. **配置分包**，把 Game 场景和大地图资源分到 battle 分包
4. **字体适配**，解决中文在微信端的显示
5. **真机测试**，用微信开发者工具 + 真机预览
6. **性能调优**，GC、内存、加载时间

---

要不要我先帮你做第一步——配置 WebGL 构建参数并尝试打一个 WebGL 包试试？这能快速暴露当前工程中存在哪些兼容性问题。