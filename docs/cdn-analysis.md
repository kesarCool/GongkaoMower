# 微信小游戏 CDN 方案分析

## 现状数据（WebGL Build 2026-05-16 优化后）

> 已移除 SourceHanSansCN-Bold.otf、bg_01.jpg 压缩至 188KB

| 项目 | 大小（未压缩） | 预估 gzip 后 | 备注 |
|------|--------------|-------------|------|
| `WebGL.wasm`（IL2CPP 代码） | 24 MB | ~5 MB | |
| `WebGL.data`（资源包） | 26 MB | ~8 MB | |
| `WebGL.framework.js`（引擎 JS） | 427 KB | ~80 KB | |
| `WebGL.loader.js` | 13 KB | — | |
| **构建总大小（Unity 输出）** | **~50 MB** | **~13 MB** | |

> ⚠️ 注意：Unity WebGL 构建产物为未压缩（WX SDK 会设置 `compressionFormat = Disabled`，因为微信有自己的分包压缩管线）。上方 gzip 栏为等效预估，实际微信小游戏产物由 WX SDK 转换工具（wasm-opt + Binaryen）处理，最终体积略有差异。

### 资源体积分解

| 资源 | 大小 | 备注 |
|------|------|------|
| ~~SourceHanSansCN-Bold.otf~~ | ~~8.3 MB~~ | ✅ **已移除** |
| bg_01.jpg（背景大图） | 188 KB | ✅ **已压缩**（原 764KB） |
| TMP msyh SDF 字体 | ~1-2 MB | Resources 内，运行时加载 |
| TileMap.prefab（地图） | 2.0 MB | 含 Tilemap 数据和引用 |
| Map/Textures（地图纹理集） | ~0.9 MB | Style01 系列建筑/地形图 |
| 其他精灵/图片 | ~0.7 MB | pic_sl(116K), pic_sb(84K), bg_taichuang1(40K) 等 |
| 场景/预制体/其他（Res） | ~0.9 MB | CardSelectionPanel, GameResultPanel 等 |
| WX SDK wechat-default | ~0.5 MB | 开放数据域模板等（微信导出时合并） |
| FlatBuffers 表数据 | ~0.5 MB | Data/table_fb/*.bytes（4 张表，已优化加载） |
| 其他（代码+框架+shader） | ~10-12 MB | IL2CPP 编译产物体积为主 |

### 优化效果对比

| 指标 | 优化前 | 优化后 | 节省 |
|------|--------|--------|------|
| 最大单文件（字体） | 8.3 MB | 0 | **-8.3 MB** |
| bg_01.jpg | 764 KB | 188 KB | **-576 KB** |
| Unity 输出总计（未压缩） | ~50 MB | ~50 MB | — |
| 预估微信产物（压缩后） | ~16 MB | **~13 MB** | **~3 MB** |


---

## 微信小游戏包体限制

| 限制项 | 数值 |
|--------|------|
| **首包上限**（无分包） | **4 MB** |
| **单个分包上限** | **4 MB** |
| **总分包 + 首包上限** | **~20 MB** |
| **CDN 远程加载** | 无上限（受微信存储策略限制） |

---

## 方案一：不使用 CDN（本地包全量加载）

### 核心思路
所有资源打进包体，通过分包把体积控制在 20MB 以内。

### 必须执行的优化

#### 1. 字体瘦身 ✅ 已完成

| 措施 | 预估效果 |
|------|----------|
| ~~移除 SourceHanSansCN-Bold.otf（8.3MB），改用 TMP SDF 裁剪字体~~ | ✅ 省 8 MB |
| TMP msyh SDF 只保留常用 3500 字（静态 SDF atlas） | 约 1~2 MB（当前已可接受，暂不处理） |
| 或使用微信系统字体 `WX.LoadFont()` 替代打包字体 | 后续可选优化 |

#### 2. 纹理压缩

| 措施 | 状态 / 预估效果 |
|------|----------|
| bg_01.jpg 压缩（764KB → 188KB） | ✅ **已完成** |
| 所有 PNG 改为 **ASTC 6×6** 格式（微信 Android/iOS 全支持） | 待做，省 50-70%（~0.9MB → ~0.3MB） |
| Map Textures 转 ASTC | 待做，省 ~0.5 MB |
| 去除 Alpha 通道（不需要透明的纹理用 RGB 格式） | 待做，省 10-20% |

#### 3. 分包配置

```
首包（<4MB）：Boot/Loading 场景 + 核心框架 + FlatBuffers schema
  ├── TableManager + ProtoTable types
  ├── BattleLoadingSceneController
  ├── 基础 UI 框架（UIManager 等）
  └── WX SDK 核心

battle 分包（<4MB）：Game 场景 + 局内资源
  ├── Game 场景 + GameLayer
  ├── SpawnerWaves + Enemy 预制体
  ├── Map Textures（ASTC 压缩后 ~0.5MB）
  └── TMP 中文字体 SDF

resource 分包（<4MB）：大图 + 背景
  ├── bg_01.jpg（压缩后）
  └── 结算 UI 图片等
```

#### 4. 代码裁剪加强

```csharp
// WebGLBuild.cs 中调整为：
PlayerSettings.stripEngineCode = true;
PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, ManagedStrippingLevel.High);
// High 比 Medium 多裁剪约 1~2MB 代码，但需确保 link.xml 覆盖完整
```

#### 5. WX SDK 设置

在 `WXEditorWindow`（微信小游戏 / 转换小游戏）中：
- `DevelopBuild`：正式版关闭
- `CleanBuild`：开启
- 音频 API 按需开启

### 无 CDN 方案估算（2026-05-16 更新）

| 项目 | 当前（已优化） | 进一步优化后 |
|------|---------------|-------------|
| wasm.gz（IL2CPP 代码） | ~5 MB | ~4.5 MB（High strip） |
| data.gz（资源包） | ~8 MB | ~6 MB（纹理 ASTC + 裁剪） |
| framework.js.gz | ~80 KB | ~80 KB |
| **总计（压缩后）** | **~13 MB** | **~10.5 MB** |

#### 分包规划

```
首包（< 4MB）：
├── IL2CPP WASM 代码                 ~4-5 MB（微信压缩后 ~2-3MB）
├── framework.js                      80 KB
├── 启动场景（BattleLoading）          ~0.1 MB
├── 核心框架（MonoSingleton, TableManager） ~0.1 MB
├── FlatBuffers 表数据                 43 KB
└── 最小 UI                             ~0.2 MB
─────────────────────────────────────────
首包总计（微信压缩后）                 ~3-4 MB ✅

battle 分包（< 4MB）：
├── Game 场景 + GameLayer             ~1.5 MB
├── Enemy 预制体                      ~0.5 MB
├── Map Textures（ASTC 后 ~0.3MB）     ~0.3 MB
├── TileMap.prefab                     ~2 MB（需压缩/拆分）
├── TMP msyh SDF 字体                  ~1.5 MB
└── bg_01.jpg                          ~0.2 MB
─────────────────────────────────────────
battle 分包总计                         ~4-6 MB（需拆为两个分包 ⚠️）

resource 分包（< 4MB）：
├── UI 图片（pic_sl, pic_sb 等）       ~0.4 MB
├── CardSelectionPanel.prefab          ~0.1 MB
├── GameResultPanel.prefab             ~0.1 MB
└── 其他杂项                            ~0.2 MB
─────────────────────────────────────────
resource 分包总计                        ~0.8 MB
```

> ⚠️ battle 分包实际可能需要拆成 battle1（Game + Enemy）+ battle2（TileMap + 字体），取决于微信压缩后的实际体积。

### 优点
- 无需 CDN 基础设施，零额外成本
- 用户打开即玩，无等待下载
- 适合初期快速发布

### 缺点
- 优化工作量大
- 需要精细管理每个包的体积
- 后续加资源容易超限
- 20MB 总分包上限是硬约束

---

## 方案二：使用 CDN（远程资源加载）

### 核心思路
首包只放启动必需的代码 + 最小资源（< 4MB），其他全部走 CDN 远程下载。

### 架构

```
首包（<4MB，一次性下载）：
├── IL2CPP WASM 代码
├── 启动场景（Boot/Loading）
├── 核心框架（MonoSingleton, TableManager, FlatBuffers）
├── WX SDK
└── 最小 UI（加载进度条）

CDN 远程资源（按需下载，Wi-Fi 下 P2P 加速）：
├── 所有纹理/精灵（ASTC 压缩）
├── 所有字体文件
├── 所有音频
├── 大预制体（TileMap 等）
└── 场景 Prefab 和 ScriptableObject
```

### 实现方式

#### 方式 A：Unity Addressables + WX CDN
1. 使用 Unity Addressables 系统标记远程资源
2. Build Addressables → 上传到 CDN
3. 微信侧通过 `WX.Request` 或 UnityWebRequest 加载
4. WX SDK 有 Addressables 适配支持

#### 方式 B：手动管理 + WX 文件系统
1. 资源打成 AssetBundle 放在 CDN
2. 游戏启动时通过 `WX.DownloadFile` 下载
3. 存入 `WX.env.USER_DATA_PATH`
4. 通过 `AssetBundle.LoadFromFile` 加载

### 微信 SDK 中的 CDN 配置

在 `WXEditorWindow` 中：
- **CDN**：填写 CDN 资源的前缀 URL（如 `https://cdn.example.com/minigame/`）
- **StreamCDN**：StreamingAssets 的 CDN 路径
- **assetLoadType**：选择「CDN」加载模式

### CDN 方案估算（更新）

| 项目 | 包内 | CDN |
|------|------|-----|
| wasm.gz（压缩后） | ~5 MB | — |
| framework.js | 80 KB | — |
| 首包资源（启动场景 + 框架 + 表数据） | 0.5 MB | — |
| **首包总计** | **~3-4 MB** ✅ | |
| 纹理资源（ASTC 压缩后） | — | ~0.5 MB |
| 字体（TMP SDF） | — | ~1.5 MB |
| 场景/Prefab（TileMap、Game 等） | — | ~3.5 MB |
| bg_01.jpg + UI 图片 | — | ~0.4 MB |
| WX SDK wechat-default | — | ~0.5 MB |

### CDN 服务选择

| 服务 | 成本 | 特点 |
|------|------|------|
| **微信云开发** | 按量付费，有免费额度 | 免域名白名单配置 |
| **腾讯云 COS** | 按量付费 | 与微信同生态，延迟低 |
| **阿里云 OSS** | 按量付费 | 国内主流 |
| **CloudFlare R2** | 免费 10GB/月 | 全球加速，国内稍慢 |

### 优点
- 首包体积可控，无限扩展内容
- 资源更新无需审核（微信审核 + CDN 替换即可）
- 用户体验好（Wi-Fi 下边玩边下）

### 缺点
- 需要 CDN 费用（初期免费额度通常够用）
- 需要实现资源加载/缓存/更新逻辑
- 首次进入需要等待下载（可做边玩边下）
- 玩家流量消耗（4G 下需提示）

---

## 建议

### 当前阶段推荐：**方案一（无 CDN）**

理由：
1. 项目处于开发测试阶段，暂无 AppID
2. 24MB → 13MB 的优化路径清晰可达
3. 无额外成本，无运维负担
4. 可快速打通「WebGL 构建 → 微信开发者工具 → 真机预览」流程

### 后续过渡到 CDN
当以下情况发生时引入 CDN：
- 资源体积超过 20MB 分包上限
- 需要频繁更新资源
- 用户反馈首次下载太慢

### 立即可以做的（不依赖 AppID）

1. 移除 SourceHanSansCN-Bold.otf，确认 TMP msyh SDF 已覆盖所有显示字符
2. PNG 纹理转为 ASTC 格式（Unity Editor 内操作）
3. bg_01.jpg 压缩质量调整
4. WebGLBuild 的 ManagedStrippingLevel 验证 High 级别是否兼容
