# Home 场景规划（大厅入口 · 页签 · 弹窗统一）

面向：**壳流程中的 Home**，作为多模块入口；与工程内 **`UIManager` + `UIPanelBase`**（`Assets/Script/Game/UI/Framework/`）对齐。

---

## 一、定位

| 角色 | 说明 |
|------|------|
| **Home** | 登录后进主壳：常驻 **Canvas**、**页签/底栏**、各功能入口；**不**直接塞满具体业务 UI 细节。 |
| **弹窗** | 选关、选角、设置、商店等均以 **`UIPanelBase` 子类 + Prefab** 形式存在，由 **`UIManager`** 统一入栈、遮罩、返回键。 |
| **与局内关系** | Home 只做「选关确认 → `BattleLoading` → `Game`」；局内 TMP 大字库不在此场景引用。 |

---

## 二、场景内层级结构（建议）

```
Home (Scene)
├── EventSystem
├── MainCanvas (Screen Space Overlay, 排序主 UI)
│   ├── SafeArea（可选：适配刘海）
│   ├── HomeHub（挂 HomeHubController：页签、快捷入口逻辑）
│   │   ├── TabBar（页签：关卡 / 角色 / 商店 …）
│   │   ├── PageHost（各「页」内容容器：可切换显隐，非必须全是弹窗）
│   │   └── QuickActions（例如「选关」大按钮）
│   └── UiRoot（挂 UIManager）
│       ├── StackRoot（RectTransform：模态弹窗父节点）
│       ├── OverlayRoot（RectTransform：确认框等）
│       └── StackBackdrop（可选：全屏暗遮罩）
└── （可选）DontDestroyOnLoad 已由 Boot 的 AppRoot 处理；TableManager 可后续挂 AppRoot，不在此重复 DDOL
```

**原则**：**一个场景一个 `UIManager`**，挂在 **MainCanvas** 子节点上，避免与局内 `UIManager` 冲突（进 `Game` 时若局内也有 UIManager，应卸载 Home 或关闭 Home 的 UIManager——当前流程为 `LoadScene(Single)`，Home 被卸载，仅 `AppRoot` 常驻）。

---

## 三、统一管理的两层

### 3.1 `HomeHubController`（大厅编排）

- 职责：**页签切换**、**打开哪个弹窗**、**进 BattleLoading 前的校验**（如是否已选关）。  
- **不**在 Hub 里写具体关卡列表渲染逻辑；选关列表放在 **`LevelSelectPanel`**。  
- 打开选关前：`TableManager.Instance.Init()`（幂等），保证 `GetTable<LevelWave>` 等可用。

### 3.2 `UIManager`（弹窗栈）

- 已在工程中实现：**Prefab 注册表**、`Open<T>()`、`CloseTop()`、`ShowConfirm()`。  
- Home 场景 Inspector 配置：  
  - `stackRoot` / `overlayRoot`  
  - `panelPrefabs` 中注册 **`LevelSelectPanel` Prefab**（根物体挂 `LevelSelectPanel` 脚本）  
  - 可选 `confirmDialogPrefab`（二次确认「是否进入该关」）

---

## 四、选关弹窗（`LevelSelectPanel`）分步实现

| 阶段 | 内容 |
|------|------|
| **M1 壳** | Prefab + `LevelSelectPanel : UIPanelBase`，占位标题/关闭按钮；`HomeHubController` 按钮 `UIManager.Instance.Open<LevelSelectPanel>()`。 |
| **M2 读表** | `OnOpen` 内 `TableManager.GetTable<LevelWave>()`（或按 `levelId` 筛选）生成关卡列表；长列表使用 **`LoopScrollRect`**（UPM：`me.qiankanglai.loopscrollrect`），见 `docs/虚拟滚动列表选型.md`。 |
| **M3 选择** | 点击某关 → 写入静态 **`SelectedLevelContext`**（levelId / chapterId）或 ScriptableObject 会话；`CloseTop()`。 |
| **M4 进局** | 选关弹窗内配置「进局」按钮 → `BattleFlowLauncher.TryStartBattleLoading()` → `BattleLoading` 场景（`BattleLoadingSceneController`：分包占位 + 进 `Game`）。详见 `docs/壳流程操作指南.md` 第六节。 |

**注意**：若选关 UI 使用 **Legacy Text + 系统字**，Prefab 内不要引用 `msyh SDF`。

---

## 五、页签（Tab）与弹窗的分工

| 形态 | 适用 |
|------|------|
| **页签 + PageHost** | 高频、内容多、希望「在同一画布内切换」：如背包、角色属性长页。 |
| **UIManager 弹窗** | 模态、叠层、需返回键关闭：选关、选角、设置、公告。 |

推荐：**选关 / 选角用弹窗**；商店可先做弹窗，后期再改为 Tab 页。

---

## 六、模块清单（可扩展）

以下入口均可由 `HomeHubController` 统一转发到 `UIManager.Open<T>()` 或切 Tab：

- 选关 → `LevelSelectPanel`（当前优先）  
- 选角 → `CharacterSelectPanel`（后续）  
- 设置 → `SettingsPanel`  
- 公告 / 邮件 → `NoticePanel`  
- 商店 → `ShopPanel` 或 Tab 页  

每新增一个弹窗：**新建 `UIPanelBase` 子类 + Prefab → 在 Home 的 `UIManager.panelPrefabs` 注册**。

---

## 七、与 `TableManager`、分包的关系

- **表在首包**：进入 Home 的 `Start` 或**首次打开选关前**调用 `Init()` 即可（已在 `HomeHubController` 打开选关前调用）。  
- **表在分包**：改为在分包就绪回调里 `Init()`，**禁止**在 `Init` 完成前 `Open<LevelSelectPanel>`（Hub 内加 `if (!bytesReady) ShowConfirm(...)`）。

---

## 八、验收清单（Home）

- [ ] 场景内 **EventSystem** + **一个主 Canvas**。  
- [ ] **UIManager** 已配置 `stackRoot`、`panelPrefabs` 含 `LevelSelectPanel`。  
- [ ] **HomeHubController** 能打开/关闭选关弹窗；返回键 / Escape 行为符合预期（`UiOpenOptions`）。  
- [ ] 打开选关前已 `TableManager.Init()`；列表绑定后无空引用。  
- [ ] 无 `msyh` / TMP 大字库出现在 Home Prefab 引用链中。

---

## 九、相关代码与文档

| 资源 | 路径 |
|------|------|
| 大厅编排（示例） | `Assets/Script/Game/App/UI/HomeHubController.cs` |
| 选关弹窗（骨架） | `Assets/Script/Game/App/UI/LevelSelectPanel.cs` |
| 弹窗框架 | `Assets/Script/Game/UI/Framework/UIManager.cs`、`UIPanelBase.cs` |
| 壳流程总述 | `docs/壳流程操作指南.md` |

---

*版本：与当前 UIManager 设计一致；选关列表绑定随配表字段迭代。*
