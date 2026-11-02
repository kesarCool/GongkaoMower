# 微信小游戏打包备忘录

> 最后更新：2026-07-06 | 分支：`feat/WeChatminigame` | Unity 2021.3.22f1c1

---

## 一、SDK 与插件版本

| 组件 | 版本 | 说明 |
|---|---|---|
| WX-WASM-SDK-V2 | 代码 ≈ v0.1.32 | `package.json` 写 `0.1.1` 是历史遗留，实际代码同步至 2026-03 |
| WXPluginVersion | `202603160259` | `WXPluginVersion.cs`，导出时自动替换，**勿手动改** |
| UnityPlugin（运行时） | `1.2.91` | `game.json` → `plugins.UnityPlugin.version`，微信服务端下发 |
| 基础库版本 | `3.15.2` | 开发者工具内 Wechat Lib |
| 插件适配版本 | `1.2.91` | 与 UnityPlugin 一致 |

---

## 二、构建配置文件

### MiniGameConfig.asset

路径：`Assets/WX-WASM-SDK-V2/Editor/MiniGameConfig.asset`

**ProjectConf：**
| 参数 | 值 | 说明 |
|---|---|---|
| projectName | 文字割草机 | |
| Appid | wx27c7354ddadf45da | |
| DST | `G:/Demo/GongkaoMower/Build/minigame` | 构建输出目录 |
| MemorySize | 256 (MB) | `-s TOTAL_MEMORY=256MB` |
| compressDataPackage | 1 | 首包 br 压缩 |
| assetLoadType | 1 | 首包放小游戏分包 |
| maxStorage | 200 (MB) | |
| HideAfterCallMain | 1 | main 调用后隐藏封面 |
| enableIOSPerformancePlus | 1 | iOS 高性能+ |
| enableEmscriptenGLX | 0 | 未启用 Emscripten GLX |
| enableiOSMetal | 0 | 未启用 Metal |

**CompileOptions：**
| 参数 | 值 | 说明 |
|---|---|---|
| **DevelopBuild** | **0** | ⚠️ 取消后出包成功但**加载报错**，见第四节 |
| AutoProfile | 0 | |
| Il2CppOptimizeSize | 1 | IL2CPP 优化体积 |
| **profilingFuncs** | **1** | 保留函数名（帮助 ReplaceRules 匹配） |
| Webgl2 | 1 | WebGL 2.0 |
| DeleteStreamingAssets | 1 | |
| brotliMT | 1 | 多线程 br 压缩 |
| enableWasm2023 | 0 | |

**SDKOptions：**
| 参数 | 值 |
|---|---|
| PreloadWXFont | 1 |
| UseMiniGameChat | 0 |
| UseCompressedTexture | 0 |

### PlayerSettings（ProjectSettings/ProjectSettings.asset）

| 参数 | 值 |
|---|---|
| `webGLMemorySize` | 256 |
| `webGLExceptionSupport` | 0（None） |
| `webGLDebugSymbols` | 1（External） |
| `webGLDataCaching` | 0 |
| `webGLTemplate` | `PROJECT:WXTemplate2020` |
| `webGLCompressionFormat` | 2（Brotli） |
| `webGLLinkerTarget` | 1（Wasm） |
| `webGLThreadsSupport` | 0 |
| `managedStrippingLevel: WebGL` | 3（High） |
| `webGLNameFilesAsHashes` | 0 |

---

## 三、Emscripten 构建参数

动态生成于 `WXConvertCore.cs`，最终 args：

```
-s EXPORTED_FUNCTIONS=_sbrk,_emscripten_stack_get_base,_emscripten_stack_get_end,_main
-s ERROR_ON_UNDEFINED_SYMBOLS=0
-s TOTAL_MEMORY=256MB
-s EXPORTED_RUNTIME_METHODS='["ccall","cwrap","stackTrace","addRunDependency","removeRunDependency","FS_createPath","FS_createDataFile","stackTrace","writeStackCookie","checkStackCookie","lengthBytesUTF8","stringToUTF8"]'
--profiling-funcs
```

> `--profiling-funcs` 仅在 `config.CompileOptions.profilingFuncs=1` 时添加。

---

## 四、⚠️ Development Build 问题

### 现象

- `DevelopBuild: 1` → 打包 + 加载均正常
- `DevelopBuild: 0` → 打包成功，加载时报错：

```
[PLUGIN ERROR] 加载资源异常 Cannot read property 'createDataFile' of undefined
[PLUGIN ERROR] start game fail: TypeError: Cannot read property 'createDataFile' of undefined
[PLUGIN ERROR] still waiting runDependencies, ids= ["wasm-preloadAssets"]
```

### 根因链路

```
取消 Development Build
  → Unity 输出 minified 的 webgl.wasm.framework.unityweb.js
  → WX ReplaceRules 38 条正则全部匹配失败（见 Build/unmatched_rules.log）
  → 微信 UnityPlugin 的 FS 替换层未正确挂载
  → processRawData() 调用 createDataFile 时目标对象为 undefined
  → 首包资源无法写入虚拟文件系统
  → wasm-preloadAssets 永远不被 remove
  → 游戏卡死在启动封面
```

---

### 最终结论：无法解决（2026-07-06）

**经过 5 轮诊断 + 4 次构建修改尝试，确认非 Development Build 加载问题在当前环境（Unity 2021.3 + WX SDK ≈v0.1.32 + UnityPlugin v1.2.91）下无法修复。**

#### 根因

1. 非 Development Build → Unity/Emscripten 压缩 framework JS 变量名
2. WX ReplaceRules（~38 条正则）全部匹配失败 → `Build/unmatched_rules.log`
3. **`processRawData` 源码（通过 `toString()` dump，日志 `127.0.0.1-1783315171251.log`）**：
   ```js
   f.FS.createPath(...);
   f.FS.createDataFile(r,null,s.slice(t,t+n),!0,!0,!0);
   ```
4. **`f` 是 UnityPlugin（v1.2.91）闭包内模块级变量，`f.FS` 应被 ReplaceRules 注入，但在 minified 框架中注入失败 → `f.FS = undefined`**
5. `Module.FS`（WXConvertCore 注入的 stub）和 `f.FS` 是两个完全不同的对象，Plugin 永远不会访问 `Module.FS`

#### 为什么不可修

- `f` 在 Plugin 闭包内 → 外部代码无法写入
- `processRawData` 被 Plugin 内部引用调用 → 无法拦截/替换（已验证，v2/v3 诊断）
- Emscripten 优化级别不可降 → `-O1` 导致 SyntaxError
- Unity Terser 不可跳过 → `AllowDebugging` 无效

#### 全部失败尝试

| # | 方案 | 结果 | 日志 |
|---|---|---|---|
| 1 | `Debug.developerConsoleVisible = false` | ❌ | — |
| 2 | Unity UI Canvas 遮挡 | ❌ 不可行 | — |
| 3 | Emscripten `-O1` | ❌ SyntaxError | `127.0.0.1-1783312622272` |
| 4 | `BuildOptions.AllowDebugging` | ❌ 同上 | `127.0.0.1-1783313242366` |
| 5 | Proxy 拦截 Module 属性 | ❌ 确认 Plugin 不经过 Module | `127.0.0.1-1783314053645` |
| 6 | 替换 `Module.processRawData` | ❌ Plugin 用内部引用 | `127.0.0.1-1783314360933` |
| 7 | 手动调用 `unityFileSystemInit` | ❌ `createDataFile` 未出现 | `127.0.0.1-1783314829157` |
| 8 | dump 源码定位 | ✅ 找到 `f.FS`，但闭包内无法触达 | `127.0.0.1-1783315171251` |

#### 可能的未来出路（未验证）

1. **升级 Unity 到 2022.2+**：`Application.debugMode` API 可直接关水印
2. **微信 UnityPlugin 更新**：Plugin 自身修复非 Dev Build 兼容性
3. **自行实现数据包加载**：替代 Plugin 的 `processRawData`（工程量极大）

#### 当前状态

**保持 `DevelopBuild: 1`，接受右下角水印。** WXConvertCore.cs 无任何修改。

---

## 五、Unity 2022 升级指南（待定）

### 前提

升级到 Unity 2022.2+ 可通过 `Application.debugMode = false` 隐藏 Development Build 水印，是解决第四节问题的正道。

### 当前项目兼容性评估

| 配置项 | 当前值 | 2022 兼容 |
|---|---|---|
| 渲染管线 | Built-in | ✅ 不受 URP 黑屏问题影响 |
| 颜色空间 | Gamma | ✅ Linear 在 2022 WebGL 有问题 |
| WebGL 版本 | 2.0 | ✅ |
| WX SDK | ≈v0.1.32 | ✅ 官方标注兼容 2022 |
| 第三方包 | 2D Feature, TextMeshPro | ⚠️ 需同步升级 |

### 升级 Checklist

- [ ] **锁死版本**：安装 **Unity 2022.3.10f1**（不能更高，2022.3.31+ 真机启动报错）
- [ ] **WebGL Player Settings**：
  - `Auto Graphics API` → **关闭**，手动指定 WebGL2
  - `Color Space` → **Gamma**（不能 Linear）
  - `Allow downloads over HTTP` → **Always allowed**
  - `HDR` → **关闭**
- [ ] **包升级**：TextMeshPro、2D Animation 等包需匹配 2022 版本号
- [ ] **API 适配**：检查 C# deprecated API（`Application.targetFrameRate` 等行为变化）
- [ ] **构建参数**：Emscripten 版本升级，检查 `emscriptenArgs` 兼容性
- [ ] **Managed Stripping**：2022 默认更激进，确认反射/序列化不挂
- [ ] **WX SDK 重新导入**：切引擎后重新 Import，确保 `.meta` 正确
- [ ] **真机验证**：开发者工具正常 ≠ 真机正常，必须在真机测
- [ ] **添加 `Application.debugMode = false`** 到 AppBootstrap 关水印

### 备选：团结引擎（Tuanjie）

微信官方推荐，wasm 体积 8MB → 3.38MB，原生 WeixinMiniGame 构建目标。当前项目已在用 Tuanjie SDK，切换成本与 Unity 版本升级相当。

---

## 六、游戏内主动分享与 SDK 桥接（2026-10-27 修整）

### 现象

游戏内按钮点击调用 `WXShareHelper.ShareForRevive()` 无法拉起转发。

### 日志证据（Build/ 目录，2026-07-02）

| 日志 | 错误 |
|---|---|
| `127.0.0.1-1782919723377.log` | `missing function: WX_OneWayNoFunction_vt` |
| `127.0.0.1-1782924742584.log` | `missing function: WX_OneWayFunction` |
| `127.0.0.1-1782918167765.log` | 调用栈完整走到 JS 层，**分享仍不触发** |
| `127.0.0.1-1782917175864.log` | 试了两种 C# 调用方式（`WXShareToRevive` + `ShareForRevive`）均失败 |

### 更新诊断

历史日志首先指向缺失的 C# → JavaScript 桥接。`1782918167765` 日志也有
`missing function: WX_CallJSFunction`，不能据此认定已经成功调用原生分享。
此前关于手势上下文、插件消费触摸及“成功率 ≤ 10%”的判断没有可复核的证据，撤回。

2026-10-27 同时发现胶囊查询使用的 `WX_SyncFunction_t` 也是直接 `abort(-1)` 的占位函数；
游戏内复活、商城当时只调用模拟广告 Provider，没有调用保留的分享方法。

修整采用统一的 WebGL SDK 桥接、主动分享请求与返回游戏后的单次奖励流程，
并在小游戏导出前检查已修复接口是否退化成缺失符号占位。
微信分享接口不能提供可靠的“已送达”确认；返回游戏领取奖励仅表示本次分享尝试已返回，
不能作为好友已收到或用户未取消的证明。真实手机上的分享面板及返回事件仍需验证。

具体覆盖范围、自动验证和真机验收步骤见 `Doc/wechat-sdk-bridge-repair.md`。

---

## 七、ReplaceRules 机制

---

## 七、ReplaceRules 机制

`WXConvertCore.ConvertCode()` 对 Unity 输出的 `webgl.wasm.framework.unityweb.js` 执行正则替换：

- `ReplaceRules.rules` — 主规则集（处理 Emscripten → 微信 API 映射）
- `ReplaceRules.NativeRules` — 仅非 IL2CPP 模式下使用
- `ReplaceRules.DoenetRules` — .NET 8 wasm runtime 专用

匹配失败的规则写入 `Build/unmatched_rules.log`。Development Build 时规则大部分匹配成功；非 Development Build 时 38 条规则全部失败。

---

## 八、构建输出目录结构

```
Build/minigame/
├── minigame/
│   ├── game.js              # 入口，加载 UnityPlugin + 启动流程
│   ├── game.json            # 小游戏配置（插件版本、分包声明）
│   ├── project.config.json  # 开发者工具配置
│   ├── webgl.wasm.framework.unityweb.js  # 核心框架（单行 minified）
│   ├── wasmcode/            # wasm 代码分包
│   │   └── *.webgl.wasm.code.unityweb.wasm.br
│   ├── data-package/        # 首包资源分包
│   │   └── *.webgl.data.unityweb.bin.br
│   ├── workers/             # Worker 线程
│   └── open-data/           # 开放数据域
├── webgl/                   # Unity 原始 WebGL 输出
│   └── Build/
└── unmatched_rules.log      # 正则匹配失败日志
```

---

## 九、本地修改清单

以下文件相对 SDK 原始版本有本地改动，**SDK 升级时需手工合并**：

| 文件 | 改动 |
|---|---|
| `Editor/MiniGameConfig.asset` | DevelopBuild: 0, 项目定制参数 |
| `Runtime/Plugins/SDK-WX-Bridge-Fix.jslib` | 新增 `SetShareImageIds` C#↔JS 桥接 |
| `Runtime/wechat-default/unity-sdk/share.js` | 自定义转发图逻辑（后台素材 ID 随机选取） |

---

## 十、构建步骤

1. Unity Editor → 顶部菜单 **微信小游戏** → **转换小游戏**
2. 在弹出面板确认参数，点击导出
3. 构建过程：
   - Unity 先执行 WebGL Build → `Build/minigame/webgl/`
   - WXConvertCore 后处理 → `Build/minigame/minigame/`
   - 包括：ReplaceRules 正则替换、FS/GL 兜底注入、wasm brotli 压缩
4. 微信开发者工具打开 `Build/minigame/minigame/` 目录预览

---

## 十一、关键代码位置

| 文件 | 说明 |
|---|---|
| `Assets/WX-WASM-SDK-V2/Editor/WXConvertCore.cs` | 构建核心：Emscripten args、ReplaceRules、FS stub 注入 |
| `Assets/WX-WASM-SDK-V2/Editor/MiniGameConfig.asset` | 构建配置 |
| `Assets/WX-WASM-SDK-V2/Editor/WXPluginVersion.cs` | 插件版本号 |
| `Assets/WX-WASM-SDK-V2/Runtime/wechat-default/game.js` | 小游戏入口模板 |
| `Assets/WX-WASM-SDK-V2/Runtime/wechat-default/game.json` | 小游戏配置模板（UnityPlugin 版本） |
| `Assets/WX-WASM-SDK-V2/Runtime/wechat-default/plugin-config.js` | 启动事件枚举 |
| `ProjectSettings/ProjectSettings.asset` | WebGL/Emscripten 编译设置 |
| `Build/minigame/minigame/webgl.wasm.framework.unityweb.js` | 构建产物：核心框架 |
| `Build/unmatched_rules.log` | 正则匹配失败日志 |
