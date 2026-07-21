# Unity vs Cocos Creator 编辑器实操对比

> 面试官问「面板有没有什么不一样」「Creator 有没有 Scene」——不是考理论，是确认你真的打开过 Creator。
> 这份文档模拟「把两个编辑器并排打开，逐个面板找对应关系」。

---

## 一、首屏第一眼：项目结构

打开 Unity 和打开 Cocos Creator，第一眼看到的东西完全不同：

| | Unity | Cocos Creator |
|---|---|---|
| 项目管理 | `.sln` / `.csproj`，外挂 IDE（VS/Rider） | **内置代码编辑器**（或 VS Code 一键打开） |
| 入口 | Unity Hub → 选项目 → 等加载 | **Dashboard（项目列表）→ 点项目 → 秒进**（轻量得多） |
| 项目文件夹 | `Assets/` + `ProjectSettings/` + `Packages/` | `assets/`（跟你写的脚本/场景放一起）+ `settings/` |
| 场景文件后缀 | `.unity`（二进制+YAML，Git 很痛苦） | **2.x：`.fire`**（JSON，纯文本） → **3.x：`.scene`**（也是 JSON） |

面试时如果说「Creator 的 Scene 文件是 .fire」，要马上补一句「这是 2.x，3.x 改成了 .scene」。只说不补，让人觉得你就打开过一次旧版本。

---

## 二、主编辑器面板：一一对应

```
Unity 面板             Cocos Creator 面板        差异
───────────────────────────────────────────────────
Hierarchy              Node Tree（层级管理器）    几乎一样。都是树形结构，拖拽改变父子关系

Scene View             Scene View（场景编辑器）   几乎一样。Gizmo 操作、2D/3D 切换都有

Inspector              Properties（属性检查器）   一样的功能。不同的是——
                                                   Unity 上组件列表竖直排列
                                                   Creator 也是竖直排列，但多了装饰器语法

Project               Assets（资源管理器）       一样的文件树。
                                                  区别：Creator 里直接双击 .prefab 进入编辑
                                                  Unity 也是双击进入 Prefab Mode

Game                   Preview（预览窗口）        功能一样，但 Creator 的预览跟浏览器调试打通

Console               Console（控制台）           几乎一样

Animation             Animation Editor           Creator 的动画编辑器也是时间轴+关键帧
                      但它叫 Animation Editor

—                     Builtins（内置资源）        Creator 独有的面板：引擎自带的纹理/材质/字体
```

**面试时可以说的差异**：

> Unity 和 Creator 的主面板布局很接近——都是层级 + 场景 + 属性检查器 + 资源管理器的四件套。最大的面板差异有三个：一是 Creator 没有 Unity 那种 ProjectSettings 独立面板，配置全散在菜单栏「项目 → 项目设置」里；二是 Creator 多了个 Builtins 面板——引擎内置的纹理和材质在这；三是 Creator 的动画编辑器叫 Animation Editor，Unity 叫 Animation 窗口，功能相近但交互不太一样。

---

## 三、场景 / Prefab 系统

**这是面试官最可能追问的点**——因为「Cocos 没有 Scene」这个说法是错的，它有。

### 3.1 Scene（场景）

| | Unity | Cocos Creator 2.x | Cocos Creator 3.x |
|---|---|---|---|
| 文件后缀 | `.unity` | **`.fire`** | **`.scene`** |
| 文件格式 | YAML（文本 + 二进制混合） | **JSON（纯文本）** | JSON（纯文本） |
| 是否可读 | 勉强（YAML 很长） | ✅ 记事本能打开 | ✅ 记事本能打开 |
| Git 友好度 | 差（二进制部分没法 diff） | ✅ 纯 JSON，diff 可读 | ✅ 纯 JSON，diff 可读 |

**Unity Scene 跟 Cocos Scene 的结构差异**：

```
Unity .unity 文件结构：
  → GameObject 列表 + 每个 GameObject 的组件序列化数据
  → 引用了哪些 Asset（通过 GUID）
  → Lightmap/光照设置
  → 文件可能几百 KB 甚至几 MB

Cocos .fire / .scene 文件结构：
  → Node 树（JSON 嵌套） + 每个 Node 的组件数据
  → 引用资源 UUID
  → 纯 JSON，人为操作比 Unity Scene 轻量
```

### 3.2 Prefab（预制体）

| | Unity | Cocos Creator |
|---|---|---|
| 文件后缀 | `.prefab` | `.prefab` |
| 创建方式 | Hierarchy 里右键 → Create Prefab | 层级管理器拖到资源管理器 |
| 嵌套 Prefab | ✅ 支持（2018.3+） | ✅ 支持 |
| 打开编辑 | 双击 → Prefab Mode（独立视图） | 双击 → 进入预制体编辑模式 |
| Prefab Variant | ✅ 支持 | ❌ 没有 Variant 概念 |

### 3.3 面试怎么答

> Cocos Creator 当然有 Scene——2.x 叫 .fire 文件，3.x 改成了 .scene，都是 JSON 格式。跟 Unity 的核心区别是：Unity 的 .unity 文件是 YAML+二进制混合，Git diff 不友好；Creator 的 Scene 是纯 JSON，可以直接看 diff 解决冲突。Prefab 两边都有，后缀都是 .prefab，嵌套 Prefab 都支持。Creator 没有 Unity 的 Prefab Variant 概念，得用嵌套 Prefab + Override 属性来模拟。

---

## 四、组件系统

### 4.1 挂载方式

```
Unity：选中 GameObject → Add Component → 搜脚本名 → 挂上
Creator：选中 Node → 属性检查器底部 → 添加组件 → UI / 自定义脚本 → 挂上

两者操作逻辑一样，Creator 把内置组件按 UI / 渲染 / 物理分了类。
```

### 4.2 脚本如何暴露到编辑器

```csharp
// Unity：public 字段自动暴露 + [SerializeField] 强制暴露
public int speed = 10;
[SerializeField] private int hp = 100;
```

```typescript
// Cocos Creator：用 @property 装饰器，不是 pubilc/private
@property({ type: cc.Integer })
speed: number = 10;

@property({ type: cc.Integer })
private hp: number = 100;
// ↑ 加了 @property 才会出现在编辑器，不是默认暴露
```

**关键差异**：

- Unity 里 `public` 字段**默认**出现在 Inspector 里（不想暴露要加 `[HideInInspector]`）
- Creator 里**默认不暴露**，必须加 `@property` 才出现在属性检查器
- Creator 的 `@property` 更强大——可以限定类型、范围、slide 条、tooltip——这些 Unity 要用多个 Attribute 叠加

### 4.3 组件生命周期

| | Unity | Cocos Creator |
|---|---|---|
| 初始化（最早） | `Awake()` | `onLoad()` |
| 激活时 | `OnEnable()` | `onEnable()` |
| 初始化（依赖就绪后） | `Start()` | `start()` |
| 每帧 | `Update()` | `update(dt)` — **有 float 参数** |
| 每帧末尾 | `LateUpdate()` | `lateUpdate(dt)` |
| 停用时 | `OnDisable()` | `onDisable()` |
| 销毁时 | `OnDestroy()` | `onDestroy()` |

**关键差异**：

Creator 的 `update(dt)` 带 `dt` 参数——不需要自己在 Update 里调 `Time.deltaTime`。另外 Creator 没有 `FixedUpdate`——因为没有 Unity 那样的独立物理步长，物理按需跑在 `update` 之内。

### 4.4 `getComponent` 的差异

```csharp
// Unity —— 泛型
GetComponent<Rigidbody2D>();
```

```typescript
// Cocos Creator —— 字符串或组件类
this.getComponent(cc.RigidBody);     // 传类
this.getComponent("EnemyAI");         // 传字符串（注意——字符串拼错不报错）
```

**Creator 的字符串 getComponent 是坑**——`"EnemyAI"` 拼错了一个字母，编译不报错，运行时返回 null。所以 Creator 项目一般推荐用类引用方式（`getComponent(EnemyAI)`），需要 import 进来。

---

## 五、资源管理

| | Unity | Cocos Creator |
|---|---|---|
| 资源文件夹 | `Assets/` | `assets/` |
| 资源引用方式 | GUID（在 `.meta` 文件里） | UUID（也在 `.meta` 文件里） |
| `.meta` 提交 Git？ | ✅ 必须 | ✅ 必须 |
| 动态加载 | `Resources.Load` / `AssetBundle` / `Addressables` | `cc.resources.load` / `cc.assetManager.loadBundle` |
| 图集 | Sprite Atlas | Auto Atlas（自动图集配置） |
| 字体 | TMP SDF Asset / Font Asset | Bitmap Font / LabelAtlas / 系统字体 |

---

## 六、构建与发布

| | Unity | Cocos Creator |
|---|---|---|
| 目标平台切换 | File → Build Settings → Switch Platform | 项目 → 构建发布 → 选平台 |
| 原生出包 | IL2CPP / Mono 编译 | JSB（JS Binding，自带原生壳） |
| WebGL 发布 | 手动配 Player Settings | 勾选 Web Mobile → 一键 HTML5 |
| 热更新方案 | HybridCLR + YooAsset（第三方） | 内置 AssetsManager + manifest 比对 |
| 微信小游戏 | WX-WASM-SDK 插件（复杂） | 内置支持——平台选"微信小游戏"即可 |

**Cocos 做微信小游戏的优势**：平台原生支持，构建发布里直接选"微信小游戏"→ 出来的就是微信开发者工具能打开的工程。Unity 要接 WX-WASM-SDK → 配 Emscripten → 调分包 → 步骤多且容易踩坑。

---

## 七、面试时怎么把对比说高级

**面试官问「Creator 和 Unity 编辑器有什么区别」——别只答面板名，答设计哲学**：

> 两者主面板布局很接近——都是四件套。但设计哲学上差异明显。Unity 的编辑器是通用游戏引擎编辑器——从 2D 到 3D、从手游到主机，窗口多、配置深。Creator 的目标非常聚焦——就是为手机小游戏和 H5 打造的，所以编辑器更轻——没有那么深的 Project Settings 嵌套、构建发布一键到微信。另外 Creator 的场景文件是纯 JSON——`.fire`（2.x）或 `.scene`（3.x），Git diff 可以直接看冲突，不像 Unity 的 `.unity` 文件是 YAML+二进制混合。第三个关键区别是组件暴露方式——Unity 的 public 字段默认出现在 Inspector，Creator 必须显式加 `@property` 装饰器。这个差异背后是两种语言的设计哲学——C# 默认开放，TS 需要声明。

---

## 八、快速自查：你能回答上来的清单

面试前对着这个清单说一遍，能流畅说清就不会出现今天的尴尬：

- [ ] Creator 2.x 的场景文件叫 `.fire`，3.x 改成了 `.scene`——都是 JSON
- [ ] 主面板跟 Unity 一样是四件套：层级管理器、场景编辑器、属性检查器、资源管理器
- [ ] Creator 多了个 Builtins 面板，少了 ProjectSettings 独立窗口
- [ ] 组件用 `@property` 装饰器暴露，不是 `public` / `[SerializeField]`
- [ ] `update(dt)` 自带 `dt` 参数，没有 `FixedUpdate`
- [ ] Creator 的 `getComponent` 支持传字符串（有坑——拼错不报错）
- [ ] 构建微信小游戏：Creator 原生一键，Unity 要接 WX SDK + 走 Emscripten 编译链
