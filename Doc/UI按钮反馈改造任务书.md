# UI 按钮反馈改造任务书

> 2026-11-01 初稿；2026-11-02 可行性审查修订；同日第一批代码交付（组件 + T1/T2/T3 绑定，待 Unity Import 验证）。
> 状态：⬜ 待办 / ✅ 已核查或完成（以说明为准） / ⚠️ 待修正或验证。

## 0. 背景与根因

- Unity `Button.interactable=false` 会阻止 `Button.onClick` 执行，挂在该回调中的音效和失败提示随之无法触发；并非 EventSystem 不再派发所有 Pointer 事件。
- 对象启用、射线可命中且未被遮罩拦截时，同对象上的独立 Pointer 事件组件仍可接收置灰按钮交互，因此可以用通用组件补反馈。
- 已有升级、升阶等失败提示在置灰时无法通过用户点击到达；部分方法仍可能被其他代码调用，不应一概删除为死代码。
- **置灰不等于资源不足**：还可能表示交易中、成功动画播放中、请求等待、无下一关或合理空操作。反馈必须区分原因。
- 已有基础设施：`UiToastPanel`、`GameErrorPresenter`、`UiClickSound`、`UIButtonSfx`；`CardView` 有手写按压及确认逻辑，不重复挂缩放组件。

## 1. 实施决策

| 决策点 | 结论 |
|---|---|
| 技术路线 | 通用反馈组件 + 少量业务状态绑定；不再要求业务代码零改动 |
| 职责划分 | 组件负责输入反馈与动画；业务负责锁定原因，保留交易、防重入及确认逻辑 |
| 锁定反馈 | 可解释的业务锁定：抖动 + 错误码提示；忙碌/等待及合理空操作：不弹资源不足提示 |
| 按压范围 | 主界面、弹窗及任务清单中明确列出的按钮；组件级排除规则见 §6 |
| 提示通道 | `GameErrorCodes` + `c错误码c.xls` + `GameErrorPresenter.Show`，允许格式化参数 |
| 多原因按钮 | 动态状态/错误码提供接口为必做；带数值的精细文案为可选 |
| 卡牌交互 | T10 单独实施与验收，不纳入第一批通用组件铺设 |

## 2. 代码与组件规格（.meta 由 Unity 生成）

### 2.1 UiPressScale.cs（新增，Framework）

- `RequireComponent(Button)`，禁止重复挂载；处理 Pointer Down / Up / Exit，并覆盖取消和拖动后的复位。
- 暴露视觉目标 `RectTransform`，优先缩放视觉子节点，保持命中区域和布局节点稳定；不得与已有动画争用同一属性。
- 按下按原始缩放乘以系数，默认可用 0.92，实际手感可调；抬起、移出、取消或开始拖动时回弹。使用 unscaled 时间，暂停时仍有效。
- 记录并恢复原始缩放，不假定为 `Vector3.one`；禁用、关闭、对象池回收和重新打开后不得残留缩放。
- 仅处理左键/触摸对应的有效指针周期，记录 pointerId，避免多指互相复位。拖动 ScrollRect 不得留下按压态。
- 纳入范围的业务置灰按钮允许按压；父级交互禁用、模态遮挡等整体输入屏蔽不得被绕过。

### 2.2 UiLockedHint.cs（新增，Framework）

- `RequireComponent(Button)`，禁止重复挂载；独立处理 Pointer Down / Click，取消、退出或禁用时清理指针状态。
- 固定原因使用序列化 `errorCode`；空码只抖动、不弹提示。固定配置仅适用于锁定原因单一的控件。
- **动态状态接口为必做**：业务提供当前状态（可用 / 可解释锁定 / 忙碌或静默禁用）、错误码及可选格式化参数。接口可采用委托或等价绑定方式，生命周期与面板一致，复用时不残留上次状态。
- 业务接口优先，固定码作为单一原因控件的回退；忙碌或静默禁用不触发锁定 Toast 和失败抖动，按压按 §2.1 执行。
- **不能只在 `OnPointerClick` 中检查当时的 `interactable`**：正常 `Button.onClick` 可能已把按钮锁住。按下时记录锁定资格，点击时复核仍为有效业务锁定；按下时可用的正常点击，不得因回调内置灰而补发失败反馈。不得依赖组件回调顺序。
- 仅有效左键/触摸点击触发；取消、拖动、非对应 pointerId 不提示。组件不调用业务 `onClick`，不解除原有锁定。
- 锁定点击：水平抖动 2 次，初始参数约 8 UI 单位、0.15 秒 + `GameErrorPresenter.Show`。幅度可配，随 Canvas 缩放验收；与按压缩放使用不同属性或独立目标，避免互相覆盖。
- 抖动围绕原始位置执行，使用 unscaled 时间；重复点击复用/重启动画，不累加位移。禁用、关闭、回收立即复位；连续点击不得堆积 Toast。
- 锁定点击如需音效，由本组件负责一次；可用点击由现有业务或音效组件负责一次，禁止重复播放，尊重音效静音设置。

### 2.3 错误码校验与缓存

- 不在 `OnValidate` 中直接调用当前运行时 `ErrorCodeCatalog.TryGet`。它依赖 `TableManager`，编辑状态下表可能未就绪，不能据此判定错误码不存在。
- 增加独立编辑器校验入口，读取导出数据，检查已配置错误码、文案、`CodeDisplay`；数据未就绪应报告“无法校验”，不能误报“错误码不存在”。
- 修正 `ErrorCodeCatalog` 缓存建立时机：表未就绪时不发布空缓存，后续可重试；表重载后失效重建。
- `GameErrorCodes.cs` 新增六个码的常量，成员命名沿用现有风格，字符串值见 §3；已有 `LevelNoNext`、`LevelLocked`、`LevelNoContext` 继续复用。

动态按钮（教学引导继续/跳过、路线图弹窗、升级横幅跳过）在创建和补音效的同一处挂载适用组件；先检查已有处理，避免重复添加或重复音效。

## 3. 错误码数据与导出（P0 前置）

源表：`Share/table/xls/c错误码c.xls`；运行数据：`Assets/Resources/Data/table_fb/ErrorCodeTable.bytes`。

**已发现阻塞项**：当前导出数据中的金币码是 `RR_GOLD_NOT_ENOUGH`，少了开头的 `E`。必须检查并修正源表、重新导出，再验证运行时查询；不能按“已填表”直接放行。

| ErrorCode（目标） | CodeText（目标） | CodeDisplay | 当前核查状态 |
|---|---|---|---|
| ERR_GOLD_NOT_ENOUGH | 金币不足，无法升级 | 1 | ⚠️ 导出为 RR_GOLD_NOT_ENOUGH，待修正、重导 |
| ERR_FRAGMENT_NOT_ENOUGH | 角色碎片不足 | 1 | ✅ 导出可见该码；⬜ 查询及展示验证 |
| ERR_STAGE_LEVEL_NOT_ENOUGH | 等级未达到升阶要求 | 1 | ✅ 导出可见该码；⬜ 查询及展示验证 |
| ERR_SHOP_SOLD_OUT | 该商品已售罄 | 1 | ✅ 导出可见该码；⬜ 查询及展示验证 |
| ERR_GIFT_CODE_ALREADY_CLAIMED | 今日已兑换，明日再来 | 1 | ✅ 导出可见该码；⬜ 查询及展示验证 |
| ERR_LEVEL_NEXT_LOCKED | 通关前置关卡后解锁 | 1 | ⬜ 原文案为“通关当前关卡后解锁下一关”，待调整、重导 |

- 地图未解锁提示使用前置关卡措辞；结算页没有下一关使用 `LevelNoNext`，不可统一套用未解锁码。
- 复用 `LevelLocked` 时按现有模板传入关卡编号等参数，校验模板与实际展示内容。
- 导出后逐项检查码名、文案、展示模式及 `GameErrorPresenter.Show` 实际效果；缺码只会 LogWarning，不能算验收通过。

## 4. 任务清单

### P0 基础设施与数据

- ⬜ T0 修正金币错误码，调整前置关卡提示，重新导出并验证六个码。
- ⚠️ T0 实现 `UiPressScale.cs`，含视觉目标、指针取消、生命周期复位。代码已交付（2026-11-02），待 Import 编译 + 挂载验证。
- ⚠️ T0 实现 `UiLockedHint.cs`，含动态状态接口、按下状态记录、点击复核及去重。代码已交付（2026-11-02）；第一版未带锁定音效（AudioId 无合适"拒绝"资源，规范本为可选项）。
- ⚠️ T0 增加六个错误码常量、独立编辑器校验（`Assets/Editor/UiLockedHintErrorCodeValidator.cs`，菜单 Tools/UI/校验锁定提示错误码）；`ErrorCodeCatalog` 缓存修复已交付——空表/表未就绪不发布缓存、下次 TryGet 重试。**未在 TableManager 挂 InvalidateCache**：运行时无表重载路径（UnInit 零调用、Init 幂等），且 Common 不得反向引用 UI（README §3）；InvalidateCache 保留给未来重载场景。

### P0 角色养成

- ⚠️ T1 `CharacterSelectionPanel.upgradeButton`：动态绑定原因（`EvaluateUpgradeLock`）；金币不足 → `ERR_GOLD_NOT_ENOUGH`；交易中、冷却及成长动画期间 → 忙碌，不报金币不足。代码已交付，待挂载验证。
- ⚠️ T2 `CharacterSelectionPanel.promoteButton`：动态绑定（`EvaluatePromoteLock`，优先级对齐 TryPromoteSelected：先等级后碎片）；交易中、冷却及成长动画期间 → 忙碌。代码已交付，待挂载验证。
- 保留 `TryUpgradeSelected` / `TryPromoteSelected` 的业务检查、扣费和防重入；不通过反馈组件重复调用交易。
- 保留成功路径 `CharacterGrowthFeedback`；组件目标不得与成长动画争用属性。

### P1 其余锁定点击反馈

- ⚠️ T3 `GameResultPanel._btnNext`：动态绑定（`EvaluateNextLock`，复用 OnNextClicked 同款三分支）。代码已交付，待挂载验证。
- ⬜ T4 `SettingsPanel.claimButton`：今日已兑换 → `ERR_GIFT_CODE_ALREADY_CLAIMED`；若增加请求中状态，单独标为忙碌。
- ⬜ T5 `ShopCell.btnBuy`（普通 + bonus 两套 Prefab）：售罄 → `ERR_SHOP_SOLD_OUT`；保留 SpriteState，不叠加通用缩放。
- ⬜ T6 `HomeMapNode.button` / `RoadmapNodeView.clickButton` / `LevelSelectLevelRowCell.clickButton`：未解锁提示前置关卡要求，原有业务提示与组件提示去重。HomeMapNode / LevelSelectLevelRowCell 排除缩放，保留锁定提示；详见 §6。
- ✅ T7 接线静态核查：Home 场景中 `HomeChapterMapView.startButton` 与 `HomeHubController.startGameButton` 指向同一按钮（fileID 705030317），由后者注册开始游戏回调；不是已确认的漏接线 bug。⬜ Unity 运行回归开始游戏，不额外重复接线。
- ⬜ T8 `GameRevivePanel` 请求中置灰：按忙碌处理，不弹资源不足或业务锁定提示；按压按适用范围补充。
- ⬜ T9 `BackpackItemView.button`：count≤0 当前被过滤；不改变过滤逻辑，空格子不挂锁定提示。

### P2 成功路径反馈

- ⬜ T11 `TutorialOverlay` 继续/跳过引导：动态创建处补音效、按压，检查引导遮罩下的事件可达性。
- ⬜ T12 `GamePausePanel` 音乐/音效 Toggle、`BossWheelPanel.skipAnimationToggle`：补适用音效，暂停面板 label 随开关变化。Toggle 不强挂要求 Button 的组件，初始化赋值不误播点击音效。
- ⬜ T13 补适用音效：`GameResultPanel.unlockGoButton`、`HomeChapterMapView.locateButton`、HomeRoadmapView“再次挑战”/关闭遮罩、`CharacterGrowthFeedback`“点击跳过”横幅；已有音效路径先去重。
- ⬜ T14 成就领取失败、商店购买 default 分支补对应失败提示，使用错误码通道；不把未知失败统一解释为资源不足。

### 独立批次：卡牌输入（不随第一批组件铺设上线）

- ⬜ T10 评估并调整 `CardSelectionPanel` pending / inputDelay（0.3s）期间的视觉反馈：可以按压，但不确认、不消耗刷新次数、不重复请求。
- 当前 `CardView` 按压依赖 `IsInteractable()` 和 `CanAcceptCardInput`，须在原有实现中区分视觉许可与业务许可；不挂 `UiPressScale`，不简单把按钮全部设为可用。
- 保留拖动取消、单次确认、请求等待及选中后防重入。确认动画期间不允许新的按压覆盖确认动画，独立回归后再交付。

### Unity 挂载与绑定清单（待实施，Import 后执行）

| Prefab / 控件 | UiPressScale | UiLockedHint / 状态来源 |
|---|---|---|
| 角色 upgradeButton / promoteButton | 挂，指定兼容的视觉目标 | 挂，业务动态绑定 |
| 结算 _btnNext | 挂 | 挂，业务动态绑定 |
| 设置 claimButton | 挂 | 挂，今日已兑换码；忙碌另分 |
| 商店两套 Cell btnBuy | 不挂，保留 SpriteState | 挂，售罄码 |
| HomeMapNode / LevelSelectLevelRowCell | 不挂，保留状态卡面 | 挂，前置关卡未解锁码 |
| RoadmapNodeView.clickButton | 挂，避免干扰现有状态表现 | 挂，前置关卡未解锁码 |
| 其余本次纳入的主界面/弹窗按钮 | 按 §6 排除后挂 | 仅有明确锁定原因时挂 |
| T11 / T13 动态按钮 | 创建处按适用范围挂 | 无锁定需求不挂 |
| T10 卡牌 | 不挂，沿用原有交互 | 不挂，业务保持原有门禁 |

## 5. 验收标准

1. 纳入范围的按钮按下有反馈，抬起、移出、取消及拖动后复位；排除项、整体屏蔽的 UI、空格子不强求反馈。
2. 金币不足、等级不足、碎片不足分别显示正确原因；等级与碎片同时不足时遵循现有业务优先级。
3. **升级/升阶成功后连续点击**：不误报金币或碎片不足，不重复扣费或交易，不打断成长动画。正常点击回调内置灰不额外触发失败反馈。
4. **拖动地图/列表**：不误弹锁定 Toast，不遗留缩放或位移；多指不互相复位。连续锁定点击不叠加位移、不堆积提示。
5. **最终关通关**提示没有下一关；未解锁下一关和地图节点使用各自正确原因，不提示再次通关已完成的当前关卡。
6. **暂停、关闭重开、对象池复用**后动画与原始位置/缩放正常；父级禁用、模态遮罩及引导输入屏蔽不被反馈组件绕过。
7. 主操作按钮音效路径明确且每次最多一次，遵循静音设置；教学按钮有反馈，Toggle 初始化不触发用户点击音效。
8. SpriteState、Tab 选中放大和原有卡面状态保留；CardView 无双重缩放。组件排除规则不得导致漏掉明确要求的锁定提示。
9. 六个新增及复用错误码可从导出表查询，文案、参数、`CodeDisplay` 正确；编辑器校验不污染运行时缓存，Home / Game 场景实际展示通过。
10. T10 独立验收：inputDelay / pending 期间的视觉反馈不导致确认、刷新扣次或重复请求；拖动取消和确认动画保持正确。

## 6. 组件级排除清单

| 控件 / 状态 | 通用缩放 | 锁定提示 | 保留行为 |
|---|---|---|---|
| ShopCellPresentation 对应购买按钮 | 排除 | 售罄时启用 | SpriteState 按压图、售罄表现 |
| CharacterSelectionElement | 排除 | 本次不新增 | SpriteState 与状态卡面 |
| HomeMapNode / LevelSelectLevelRowCell | 排除 | 未解锁时启用 | 原有状态卡面；允许锁定抖动 |
| CardView | 排除 | 排除 | 原有按压、拖动、确认；T10 单独调整 |
| AchievementCell 卡面 | 排除 | 本次不新增 | 原有卡面；T14 失败提示由业务触发 |
| 背包 Tab/卡片选中态 | 排除 | 本次不新增 | 原有选中表现 |
| HomeTabBar | 排除 | 当前页签排除 | 选中放大、当前页签合理 no-op |
| ItemCell.BindEmpty 空格子 | 排除 | 排除 | 合理置灰，无资源不足提示 |

排除缩放不等于排除音效或锁定提示，以本表和任务清单的具体要求为准。未列出的复杂动画按钮挂载前检查属性冲突。

## 7. 必做绑定与可选精细文案

- **必做**：多原因按钮由业务提供实时状态与错误码；组件不自行读取金币、碎片、等级或参与交易。忙碌与可解释锁定分开。
- **必做**：绑定随面板生命周期建立和清理；固定码只用于单一原因控件；原有业务失败处理与组件提示不重复触发。
- **本次允许**：使用“金币不足，无法升级”“角色碎片不足”等静态文案。
- **可选后续**：配表模板与格式化参数配合，显示“还差 X 金币/碎片”；不在组件内复制养成计算逻辑。
- 本次交付包含必要的业务绑定，不以“纯组件路线”为由牺牲提示准确性。

## 8. 已核查事实与范围外建议

1. `SettingsPanel` / `AboutPanel` 已有 `OnClose` 移除对应监听，撤回“只 Add 不 Remove”的原扫描结论，不据此新增监听修复任务。
2. `HomeChapterMapView.startButton` 通过 HomeHub 的按钮绑定路径接线；需同时考虑场景覆盖引用与运行时监听，不能只凭地图 Prefab 的持久化监听判定漏接线。
3. `AntiSpamClick` 全局铺设不在本次范围；本次仍须保留交易、请求及卡牌确认的防重入保护。
4. 静态审查确认的场景引用和导出码名不代替 Unity 运行验收；实现后按 §5 验证，再标记任务完成。
