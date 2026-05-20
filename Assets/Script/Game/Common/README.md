# Common 模块团队规范（Game/Common）

本目录用于存放**跨模块复用**的基础设施与工具代码。目标是让工程随着功能增长仍保持清晰、可维护、可扩展。

> 约定：**业务逻辑不要放在 Common**。Common 只放“被多个模块依赖、且不依赖业务细节”的代码。

---

## 1. 目录与模块边界

工程脚本根目录建议保持如下结构（按功能域分层，域内再分 Components/Data/Systems）：

- `Shell/`：局外壳（登录/大厅流程、选关与词汇预览等 **业务弹窗脚本**，见 `Shell/UI/`；与局内 `UI/` 区分）
- `App/`：壳层启动与场景编排（如 `AppBootstrap`、登录/Loading 场景控制器等，**不放** `Shell/UI` 已覆盖的弹窗脚本）
- `Player/`：玩家相关
- `Enemies/`：怪物相关
- `Combat/`：战斗/子弹/伤害计算
- `Spawning/`：刷怪/波次
- `UI/`：局内界面层（HUD、Layer、**弹窗框架**见 `UI/Framework/`）
- `Input/`：输入（摇杆、触摸、输入采集）
- `Camera/`：相机跟随/边界
- `World/`：Tilemap、地图边界、世界规则（如有）
- `Common/`：公共基础设施（本目录）

Common 内部建议（按需建立子文件夹）：

- `Common/Events/`：事件系统（Event Bus、事件定义）
- `Common/Resources/`：资源加载与缓存（Resources/Addressables 包装）
- `Common/Config/`：配置读取（CSV/JSON/ScriptableObject 的读取与解析）
- `Common/Utils/`：纯工具方法（Math、String、集合扩展等）
- `Common/Diagnostics/`：调试开关、日志工具、性能采样（可选）
- `Common/Pooling/`：对象池（如 `GameObjectPool`、借出登记与 `ClearAllPools` 约定，见 **5.2**）
- `Common/Targeting/`：索敌/目标注册表（如 `CombatTargetRegistry`，见 **5.3**）

---

## 2. 命名规范（简化版）

- **类名**：PascalCase（如 `EnemyBase`, `CameraFollow2D`）
- **方法名**：PascalCase（如 `TakeDamage`, `TriggerWaves`）
- **字段**：
  - `public`：camelCase（Unity Inspector 常用，如 `moveSpeed`）
  - `private`：`_camelCase`（如 `_waveRoutine`）
- **文件名**：与类名一致（一个文件一个主类）
- **Tooltip**：统一中文，说明“用途 + 典型取值/注意点”

---

## 3. 依赖方向（非常重要）

依赖必须遵守“从上到下”：

1. `Common`（最底层）  
2. `Data`（纯数据）  
3. `Components`（挂在 GameObject 上的组件）  
4. `Systems`（跨对象的系统/管理器）  
5. `UI`（展示层）

### 允许
- `Enemies/*` 依赖 `Common/*`
- `Spawning/*` 依赖 `Enemies/Data`、`Enemies/Components`
- `UI/*` 监听事件或读取状态（但不直接写核心战斗逻辑）
- `UI/Framework/*`（`UIManager`、面板基类）仅依赖 uGUI / UnityEngine，**不反向引用**具体业务模块；**局外壳弹窗**脚本放在 `Shell/UI/`，局内玩法弹窗放在各域下（如 `Roguelike/UI/`），均继承 `UIPanelBase` 即可

### 禁止（反模式）
- `Common` 引用 `UI`/`Enemies`/`Player`（会导致循环依赖）
- 任意模块到处 `FindObjectOfType` 拿对象做强耦合（会让后期维护非常痛苦）

> 例外：原型阶段允许少量 `FindObjectOfType`，但必须在需求稳定后迁移到事件或依赖注入方式。

---

## 4. 事件系统（推荐做法）

为什么要事件：
- 减少模块间硬引用（例如 `EnemyBase` 死亡不必直接找 UI）
- 让 UI、音效、刷怪、统计等“旁路系统”订阅事件即可工作

### 推荐事件粒度
- `EnemyDied(enemyId, rewardKillCount, position)`
- `PlayerDamaged(amount, hpLeft)`
- `WaveTriggered(waveIndex)`

### 推荐位置
- 事件总线放 `Common/Events/`
- 事件定义（struct/class）也放 `Common/Events/`

---

## 5. 数据驱动与资源引用

当前阶段（你选的 D）：在 Inspector 手配 `EnemyCatalog` 是合理的。

后续接表格（CSV/JSON）建议：
- **读取与解析**：放 `Common/Config/`
- **配置对象模型**：放 `Enemies/Data/`（例如 `EnemyDefinition`）
- **资源引用策略**：
  - 原型：直接引用 `prefab/sprite/bulletPrefab`
  - 中期：用 `Addressables` 或资源路径（需要统一加载器）

### 5.1 `LevelWave` 表头注释（配表用）

以下为 Excel **第二行说明**或备注列文案；列名以导出程序为准（与 `ProtoTable.LevelWave` 字段一致）。`waveTimeContinue` / `timeStart` 在表里多为**整型秒**，`intervalSpawn` 为**秒（可小数）**。

**全局约定**

- **关卡键**：运行时 **`CurrentLevel` = `levelId`** 筛本表；**`ChapterLevel` 仅章节/地图**，不参与波次解析。
- **波次排序**：同 `levelId` 下按 **`wave`** 升序。
- **时间模型（模型 II）**：每波先 **`timeStart`** 秒（仅延迟**首次**出怪），再进入 **`waveTimeContinue`** 秒的**完整刷怪窗**；开场等待**不占用**战斗窗。
- **1.A 时间窗优先**：`waveTimeContinue` **到点即强制下一波**；本窗内未刷满的 **`totalMonster`** **不再补**。

**字段 × 注释**

| 字段名 | 表头注释 |
|--------|----------|
| `ID` | 行主键；表内唯一，配表/工具用 |
| `levelId` | 关卡 ID；须与运行时 `CurrentLevel` 一致 |
| `wave` | 波次序号；同 levelId 内递增，决定播放顺序 |
| `waveTimeContinue` | 刷怪时间窗（秒）：在 `timeStart` **结束之后**才开始计时；**到点切下一波**，未满额怪不补（1.A） |
| `monsterId` | 怪物配置 ID；对接 `Monster` / 预制体或词缀表 |
| `attack` | 本波实例攻击力覆盖；0 可走怪物默认 |
| `maxHp` | 本波实例生命覆盖；0 可走怪物默认 |
| `exp` | 本波击杀经验（或规则内含义） |
| `prop` | 掉落/道具等扩展字段（按策划规则） |
| `timeStart` | 首只怪前等待（秒）；只堵开场，**不占用** `waveTimeContinue` |
| `intervalSpawn` | 刷怪间隔（秒）；在刷怪窗内循环直到时间到或达到 `totalMonster` |
| `totalMonster` | 本波计划刷怪只数上限；可被 `waveTimeContinue` 截断 |
| `lineSpawn` | 出生路线：`0` 环玩家；`1` 上、`2` 下、`3` 左、`4` 右（边侧带，以实现为准） |
| `iscirculate` | 环绕/循环类行为标记（与 `lineSpawn`、玩法实现配合） |
| `isBoss` | 是否 Boss 波（剧情/UI/掉落等分支） |
| `quantityBoss` | Boss 数量或配额（按策划规则） |

**整行备注（单列粘贴）**：`ID=行主键 | levelId=关卡ID对齐CurrentLevel | wave=波次顺序 | waveTimeContinue=timeStart后的刷怪总秒数到点强制下波不满不补 | monsterId=怪ID | attack/maxHp=实例覆盖0可默认 | exp/prop=扩展 | timeStart=首怪前等待秒不吃战斗窗 | intervalSpawn=刷怪间隔 | totalMonster=计划只数上限 | lineSpawn:0环玩家1上2下3左4右 | iscirculate/isBoss/quantityBoss=玩法标记`

### 5.2 对象池 `GameObjectPool`（借出登记）

`Common/Pooling/GameObjectPool.cs` 在 **`Get`** 时把实例记入 **`LeasedObjects`**（`HashSet<GameObject>`），在 **`Release`** 开头移除；**`ClearAllPools`** 时仅对该集合与池内 inactive / 池根做销毁，**不再** `FindObjectsOfType` 全场景扫描。

**约定**：局内借出对象须走 **`Release`** 回池或依赖 **`ClearAllPools`**（如结算、离局场景）；不要 **`Get` 后去掉 `PooledObject`** 或长期占用不归还，否则清池可能漏网或逻辑不一致。

### 5.3 索敌注册表 `CombatTargetRegistry`（禁止技能内 `FindGameObjectsWithTag`）

`Common/Targeting/CombatTargetRegistry.cs` 按 **Tag** 维护当前活跃、可被索敌/统计的 `Transform` 列表，供技能、战斗结算等模块查询，**替代** `GameObject.FindGameObjectsWithTag` 的全场景扫描（避免 GC 与重复遍历）。

**注册约定**

- 带 **`EnemyBase`** 的怪物在 **`OnEnable`** 时 `Register`，**`OnDisable`** 时 `Unregister`（池化借出/归还、死亡 Disable 会自动进出表）。
- 新怪物 Prefab 须挂 `EnemyBase` 且 Tag 正确（如 `monster`），否则不会进入注册表。
- 离局清表与 `GameObjectPool.ClearAllPools` 对齐：离开局内场景（`BattleSceneNameForPoolClear`，默认 `Game`）时 **`ClearAll`**。

**查询 API（常用）**

| 方法 | 用途 |
|------|------|
| `FindNearest(tag, from, maxRange)` | 范围内最近敌人（自动剔除已销毁/未激活） |
| `CountInRange(tag, from, maxRange)` | 范围内活跃敌数量 |
| `CountActive(tag)` | 该 Tag 当前注册总数（如胜利判定清怪） |

**调用方示例**

- 技能：`SkillBase.FindNearestEnemy` / `CountActiveEnemiesInRange`（内部走注册表；`SkillAutoProjectile`、`SkillThrowGrenade`、`SkillLineBeam2D` 等沿用即可）
- 战斗：`BattleOutcomeCoordinator.CountMonstersAlive`
- Legacy：`PlayerController` 自动射击索敌

**禁止 / 注意**

- **禁止**在技能、战斗逻辑里对敌人使用 **`FindGameObjectsWithTag`** / **`FindGameObjectWithTag`** 做索敌或计数（找**玩家**的 Tag 查询另议，可后续做 `PlayerRegistry` 或事件注入）。
- 不要手动 `Register` 非战斗目标或 Untagged 对象；注册表只服务于「按 Tag 批量查询活跃 Transform」这一件事。
- 查询时会惰性剔除列表中的 null / 未激活项；**Unregister 仍应在 `OnDisable` 正常调用**，不要依赖惰性清理代替生命周期。

### 5.4 技能 Def 工厂（`PlayerSkills` 不再 switch）

各 `*SkillDefinition`（ScriptableObject）负责：

| 方法 | 时机 |
|------|------|
| `CreateRuntimeSkillInternal(bindings)` | 用 Def 资源 + `SkillRuntimeBindings`（Player 仅 Prefab 覆盖 / 射线表现钩子）构造 `ISkill` |
| `ApplyStatsToSkill(skill, level)` | 创建后 / 升级时写入 per-level 数值 |

`PlayerSkills.CreateSkill` 流程：

```
GetDef(id) → def.CreateRuntimeSkill(_bindings)   // 内含 ApplyStats Lv.1；无 Def 则告警并失败
```

**新增技能 checklist**：`SkillId` → `*SkillDefinition` 两方法 → `SkillCatalog` 挂 SO → （P1 前）Player 上可选 Prefab 覆盖 / 射线表现字段。

**P0 约定**：技能数值与 AutoProjectile 的 `bulletPrefab` 均在 SkillDef；Player 不再存 interval/damage 等回退字段。

---

## 6. 常见落位示例

- `DynamicJoystick`：`Input/UI/`（输入模块的 UI 控件）
- `TouchLayer`：`UI/Layers/`（全屏触摸层属于 UI Layer）
- `UIManager` / `UIPanelBase` / `UiConfirmDialog`：`UI/Framework/`（全局弹窗栈，**不放 Common**）
- `BattleChineseFontRuntime` / `TMPChineseFontAutoApply`：`App/`（TMP 中文字体加载与 UI 兜底，见 **7.6**）
- 大厅/选关/词汇预览等壳业务 Panel：`Shell/UI/`（与 `Roguelike/UI/` 对称：前者局外，后者局内）
- `SpawnerWaves`：`Spawning/Systems/`
- `CombatVfxSpawner` / `PooledCombatVfx`：`Combat/Visuals/`（一次性池化战斗特效统一走 **`CombatVfxSpawner.TryPlayPooled`**；Prefab 挂 **`PooledCombatVfx`**；SpawnLimiter key = `CombatVfx`）
- `SkillDefinitionBase` / `SkillRuntimeBindings` / `SkillRuntimeFallback`：`Combat/Skills/`（**Def 自注册**：各 `*SkillDefinition` 实现 `CreateRuntimeSkillInternal` + `ApplyStatsToSkill`；`PlayerSkills` 只调 `def.CreateRuntimeSkill(bindings)`，见下）
- `EnemyBase/EnemyAI/EnemyRanged`：`Enemies/Components/`（怪物 **`OnEnable/OnDisable`** 向 `CombatTargetRegistry` 注册，见 **5.3**）
- `EnemyCatalog/EnemyDefinition`：`Enemies/Data/`

---

## 7. UI 弹窗框架（规范）

技术栈为 **uGUI（Canvas + RectTransform）**。统一入口为场景中的 **`UIManager`**（`UI/Framework/UIManager.cs`），与 **Common 解耦**：实现放在 `UI/`，避免 `Common` 引用 UI 造成循环依赖。

### 7.1 结构：单主栈 + 顶层确认框（弱 B）

- **主栈**：模态弹窗 **后进先出**，同一时刻通常只显示栈顶（下层 `GameObject` 会隐藏，但仍在栈中）。
- **确认框**：与主栈独立的 **Overlay** 层，用于小确认框；可叠在主栈之上，**不**把主栈 Panel 先 Pop 掉。
- **同类型唯一实例**：每种 `UIPanelBase` 子类 **最多一个运行时实例**；再次 `Open<T>` 时若该面板已在栈中，会先 **关掉上方各层** 将其抬到栈顶；若已在栈顶则 **仅再次 `OnOpen` 刷新**。

### 7.2 框架职责 vs 业务职责

| 框架（`UIManager`） | 业务（具体 Panel 脚本） |
|---------------------|-------------------------|
| 主栈 / Overlay 父节点、可选 **共用遮罩**（`stackBackdrop`） | 继承 **`UIPanelBase`**，实现 **`OnOpen(object payload)`** / **`OnClose()`**；脚本落位 **`Shell/UI/`**（壳）或 **`Roguelike/UI/`** 等玩法域 |
| **Canvas 排序**（`stackSortingBase` / `overlaySortingBase`，子物体需 `Canvas` 且 Override Sorting） | 数据绑定、布局、按钮逻辑 |
| **`Time.timeScale` 暂停栈**（`UiOpenOptions.PauseTime`，引用计数） | 在 `Update`/动画中按 **`LastOptions.UseUnscaledTime`** 选用 `unscaledDeltaTime` |
| **Escape**：先关确认框（`onResult(false)`），再按栈顶 **`CloseOnBack`** 关闭 | 微信/Android **返回键**建议在输入层转发为 `CloseConfirm()` / `CloseTop()` |
| **`ShowConfirm`** / **`CloseConfirm`** | 具体玩法 UI 不强制迁入框架；逐步迁移即可 |

### 7.3 打开方式与参数

- **主面板**：`UIManager.Instance.Open<T>()`（默认 `UiOpenOptions.ModalDefault`：暂停 + 非缩放时间 + 返回关闭），或 `Open<T>(object payload, UiOpenOptions options)`。
- **Payload**：由业务在 `OnOpen` 内自行强转；无需框架层泛型约束。
- **确认框**：`ShowConfirm(title, message, Action<bool> onResult, UiOpenOptions)`；**不要**使用 `Open<UiConfirmDialog>()`（框架会拦）。

### 7.4 Inspector 注册约定

- **`panelPrefabs`**：各模态 Prefab 根节点挂载 **具体** `UIPanelBase` 子类（勿注册抽象基类本身）。
- **`confirmDialogPrefab`**：单独配置 **`UiConfirmDialog`**，**不要**再塞进 `panelPrefabs`，避免重复注册。
- 场景根下建议两个 **`RectTransform`**：`stackRoot`、`overlayRoot`（均在同一 Overlay Canvas 下）；Overlay 的 **sortingOrder / sibling** 应保证确认框在主栈之上。

### 7.5 资源实例化（与发布）

- 当前实现为 **Inspector 拖 Prefab 注册**，适合原型与首包可控的中型项目。
- 若需 **微信小游戏压首包 / 远程热更**，可后续接入 **Addressables** 等异步加载，在框架侧扩展「按类型解析 Prefab」即可；**不建议**把大量弹窗长期堆在 `Resources/`。

### 7.6 TextMeshPro 中文显示（`BattleChineseFontRuntime`）

**核心结论**：能否显示中文，取决于 **`TMP_Text.font` 是否为含汉字的 SDF 字体**（工程默认 `Resources/Fonts/msyh SDF`），**不是**有没有挂 `TMPChineseFontAutoApply`。该组件只是在 `Start` 时做一次字体替换的**兜底**，不能代替框架或预制体上的正确配置。

**相关代码（`App/` + `UI/Framework/`）**

| 类型 | 路径 | 作用 |
|------|------|------|
| 运行时加载与替换 | `App/BattleChineseFontRuntime.cs` | `EnsureLoaded()` 加载 msyh SDF；`ApplyToTMP` / `ApplyToHierarchy` 批量替换子树字体 |
| 单节点兜底 | `App/TMPChineseFontAutoApply.cs` | `Start` 时对同物体 `TMP_Text` 调用 `ApplyToTMP` |
| 主栈弹窗 | `UI/Framework/UIManager.Open<T>()` | `OnOpen` 之后对 **整棵 Panel 子树** 执行 `ApplyToHierarchy` |
| 确认框 | `UI/Framework/UIManager.ShowConfirm()` | 首次实例化与每次 `Show` 时同样对确认框子树 `ApplyToHierarchy`（**勿**用 `Open<UiConfirmDialog>()`） |

**为何有的预制体不挂 `TMPChineseFontAutoApply` 也能显示中文？**

1. **经 `UIManager.Open<T>()` 打开**：例如 `LevelSelectPanel`、`GameResultPanel`；打开时框架已批量换字体。预制体里即使用 TMP 默认 **Liberation Sans SDF**（guid `8f586378…`），运行时仍会被改成 msyh。
2. **预制体 Inspector 已指定 msyh SDF**：不依赖运行时替换。
3. **代码里显式 `ApplyToTMP`**：例如 `GameLayer` 动态创建 HUD、`BattleLoadingSceneController` 文案等。
4. **挂了 `TMPChineseFontAutoApply`**：适用于未走 `UIManager.Open`、或 `Open` 之后才动态生成的 `TMP_Text`（见下）。

**为何 `PopConfirmPanel` 等容易「不挂就不显示」？**

- 确认框走 **`ShowConfirm`**，挂在 **Overlay**，**不进主栈**；若历史上未对确认框执行 `ApplyToHierarchy`，会一直沿用预制体上的 Liberation Sans，中文显示为 □ / 空白 / 乱码。
- 挂 `TMPChineseFontAutoApply` 后 `Start` 会换字体，所以看起来「只有挂了才行」——根因是**缺一次字体应用**，不是组件本身有魔法。

**推荐做法（按优先级）**

1. **弹窗 / 重要 UI 预制体**：在 Inspector 把 **Font Asset** 直接设为 `msyh SDF`（`Assets/Resources/Fonts/msyh SDF`），减少对运行时替换的依赖。
2. **经 `UIManager` 打开的 Panel**：依赖 `Open` / `ShowConfirm` 内的 `ApplyToHierarchy` 即可，**不必**每个 `TextMeshProUGUI` 都挂 `TMPChineseFontAutoApply`。
3. **`TMPChineseFontAutoApply` 仅作兜底**：不经 `UIManager` 实例化的 UI、运行时 `new GameObject` + `TextMeshProUGUI`、或在 `Open`/`Show` **之后**才创建且父级当时不在已应用子树上的 TMP。
4. **动态列表 Cell**：若在 `OnOpen` 里已生成且为 Panel 子物体，一般已被 `ApplyToHierarchy` 覆盖；若在打开后才异步生成，需在生成后对子树再调一次 `ApplyToHierarchy`，或给 Cell 挂 `TMPChineseFontAutoApply`。

**局内文字怪**：`EnemyWordLabel` 走 `BattleChineseFontRuntime.TryApplyTo`，与 UI 的 `TextMeshProUGUI` 路径分开，勿混用。

### 7.7 错误码提示（`ErrorCodeTable` + `GameErrorPresenter`）

- 配表：`Share/table/xls/c错误码c.xls` → `ErrorCodeTable.bytes`；字段 `ErrorCode`（键）、`CodeText`（文案，可 `{0}`）、`CodeDisplay`（0 不显示 / 1 Toast / 2 对话框）。
- 代码常量：`UI/Framework/GameErrorCodes.cs`；统一入口 **`GameErrorPresenter.Show(errorCode, args…)`**。
- **Toast**：`UIManager.toastPanelPrefab`（挂 `UiToastPanel`），需单独做预制体并绑定 Home 场景 UIManager。
- **对话框**：复用 `confirmDialogPrefab`（`ShowAlert` / `CodeDisplay=2`）。
- 填表示例见 `Share/table/xls/错误码填表示例.md`。

---

## 8. 代码评审清单（提交前自检）

- 是否把新脚本放到了正确的模块目录？
- 是否新增了跨模块硬引用（特别是 UI ↔ 战斗）？
- 是否所有 Inspector 字段都有清晰中文 Tooltip？
- 是否避免了不必要的 `Update()`（可以用协程/事件就别每帧轮询）？
- 是否避免在运行时频繁 `FindObjectOfType`（能缓存就缓存）？
- **索敌**：技能/战斗是否通过 **`CombatTargetRegistry`** 查询敌人，而非 **`FindGameObjectsWithTag`**（见 **5.3**）？
- **战斗 VFX**：一次性粒子/特效是否经 **`CombatVfxSpawner.TryPlayPooled`**，Prefab 是否含 **`PooledCombatVfx`**？
- **弹窗**：新模态是否继承 `UIPanelBase` 并通过 `UIManager` 打开？局外壳是否落在 **`Shell/UI/`**？是否误把业务逻辑写进 `UI/Framework/`？
- **TMP 中文**：新 UI 预制体是否使用 **msyh SDF**，或确认会经 `UIManager.Open` / `ShowConfirm` 触发 `ApplyToHierarchy`？动态创建的 TMP 是否补了字体应用？
- **错误码**：新增玩家可见失败是否已在 `c错误码c.xls` 加行，且 `GameErrorCodes` 有对应常量？
