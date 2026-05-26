# Boss 技能可配置方案分析

## 现状问题

`BossBehavior_T1` 是硬编码 MonoBehaviour，技能"追踪小刀"和 Boss 绑死。
后续每增加一个 Boss 变体就需要一个新 C# 类，技能无法复用。

## 目标

- 技能独立，一个 Boss 可配置多个技能
- 新增 Boss 不改 C# 代码，只改 Excel
- 同一个技能可以被多个 Boss 使用

---

## 方案：BossBrain + 技能组件池

### 架构

```
Excel Monster 表
  element[]  = ["homingKnife", "dash"]          ← 技能类型列表
  elementNum[]= ["3,120,5,20", "6,200,8,30"]    ← 每技能的参数（逗号分隔）

         ↓ 运行时解析

BossBrain (一个通用组件，挂到所有 Boss Prefab 上)
  ├─ 读取 Monster.element[] → 知道要装哪些技能
  ├─ 为每个技能创建对应的 SkillModule
  │   ├─ HomingKnifeModule   (可复用)
  │   ├─ DashModule           (可复用)
  │   ├─ BladeBurstModule     (可复用)
  │   ├─ CloneModule          (可复用)
  │   ├─ SummonModule         (可复用)
  │   └─ ReviveModule         (可复用)
  └─ 每个模块独立计时、独立执行
```

### BossBrain 核心逻辑（伪代码）

```
Awake():
  读 Monster 表 → 拿 element[] 和 elementNum[]
  foreach i in element.Length:
    type = element[i]
    params = ParseCSV(elementNum[i])  // "3,120,5,20" → [3,120,5,20]
    module = CreateModule(type, params)
    modules.Add(module)

Update():
  foreach module in modules:
    module.cooldown -= dt
    if module.cooldown <= 0 and module.CanTrigger():
      module.Execute()
      module.cooldown = module.interval
```

### 技能模块接口

```csharp
public abstract class BossSkillModule
{
    public float interval;   // 冷却时间
    public float cooldown;   // 当前冷却
    public float damage;
    public float speed;
    public float lifetime;

    public virtual void Init(float[] rawParams, BossBrain owner);
    public virtual bool CanTrigger();  // 检查触发条件（如血量%、距离）
    public virtual void Execute();
}
```

每个具体技能（HomingKnife / Dash / BladeBurst 等）只重写 Execute()，
Init() 从 elementNum[] 的 CSV 字符串解析参数填充字段。

---

## 可行性分析

### ✅ 可行的点

**1. Excel 字段复用**
`element[]` 和 `elementNum[]` 是 Monster 表已有字段，FlatBuffer 已支持 string 数组。不需要改表结构。

**2. BossBrain 组件通用性**
所有 Boss Prefab 挂同一个 `BossBrain` 脚本。Prefab 层面完全相同，差异只在 Excel 配表——这符合"Excel 为唯一数据源"的原则。

**3. 技能模块可组合**
牛刀小试 = ["homingKnife"]
牛刀小试·强 = ["homingKnife", "dash"]
剑气纵横 = ["bladeBurst"]
刀光剑影 = ["bladeBurst", "clone", "summon", "revive"]

四个 Boss 只需要 6 个 SkillModule 类，不新增 Boss 级别的 C# 文件。

**4. 单一 BossBrain 替代所有 BossBehavior_T***
`BossBehavior_T1` / `T2` / `T3` 全部可以删除，统一用 `BossBrain`。

---

## 坑点分析

### 坑 1：elementNum[] 格式脆弱

`"3,120,5,20"` 这种 CSV 字符串没有编辑器校验。写错一位（如 `"3,120,5"` 少一个参数）→ 运行时静默失败或数组越界。

**缓解**：加 Editor 校验工具，构建时扫描 Monster 表，检查格式合法性。或者用固定顺序 + 默认值兜底：
```
HomingKnife:  cooldown, turnRate, lifetime, damage
Dash:         cooldown, speed, distance, damage
```
位置固定，少参数用默认值。

### 坑 2：Excel 和代码的技能名强耦合

`element[] = ["homingKnife"]` → 代码里 `if (type == "homingKnife")`。
字符串拼写错误 = 技能静默缺失，没有任何编译期检查。

**缓解**：用 int 枚举代替字符串。`element[] = ["1"]` → 代码里 `SkillType.HomingKnife = 1`。
但 element[] 是 string[]，存 "1" 也不优雅。更好的做法是用 `triggerMode` 作为 bitmask：
```
triggerMode = 0b000001 → 只有 homingKnife
triggerMode = 0b000011 → homingKnife + dash
triggerMode = 0b001111 → 四个技能全有
```
但 bitmask 只能表达"有没有"，不能表达参数和冷却。

**最终建议**：element[] 存技能类型枚举值（"homingKnife"），代码里用 const string 匹配。Editor 工具校验拼写。

### 坑 3：BossBrain 和 EnemyAI 的协作

`EnemyAI` 每帧追玩家（FixedUpdate → MoveTowards）。
`DashModule` 执行时需要接管移动（冲锋期间不受 EnemyAI 控制）。
`CloneModule` 需要克隆 Boss，新实例也需要 BossBrain。

**关键**：BossBrain 和 EnemyAI 之间需要互斥控制。
```
Dash 开始 → 通知 EnemyAI 暂停追人
Dash 结束 → 恢复 EnemyAI
```

**方案**：BossBrain 有一个 `IsBusy` 标志位。EnemyAI 的 Update 检查这个标志，busy 时跳过移动。

### 坑 4：技能间交互

多个技能同时 Ready 时怎么处理？
- 是排队依次执行还是同时并发？
- 分身 + 刀气爆发同时触发会不会卡顿？

**方案**：默认串行——同一帧只执行一个技能。冷却重叠时按优先级排队。配置中加一个 `priority` 字段。

### 坑 5：分身技能的复杂性

`CloneModule` 需要：
1. 复制 Boss GameObject（包含 BossBrain + EnemyAI + TextMesh）
2. 修改血量（30%）
3. 设置半透明
4. 启动 15 秒自毁定时器

复制一个带多个组件的 GameObject 不是简单的 Instantiate——TextMesh 的状态、当前血量、冷却计时器都需要处理。

**方案**：CloneModule 重新走 SpawnerWaves 的生成流程，生成一个相同 monsterId 的新实例，然后覆盖血量。不直接 Instantiate 当前 Boss。

### 坑 6：死亡复活的生命周期

`ReviveModule` 在 Boss 死亡时触发。但 `EnemyBase.Die()` 里已经有回收/销毁逻辑（对象池 Release 或 Destroy）。

**问题**：Die() 已经被调用了，EnemyBase 的 hp 已经是 0，GameObject 可能已经被回收了。

**方案**：Boss 的 Die() 需要被拦截。BossBrain 在 Awake 时 hook `OnDied`，如果配置了 revive 技能，就在 Die() 回收前截断，执行复活逻辑（回血 + 短暂无敌）。需要给 EnemyBase 加一个 `PreventDeath` 钩子。
或者更简单的：Boss 不进入对象池，Die() 时只隐藏不销毁。复活 = 恢复显示 + 重置血量。

### 坑 7：elementNum[] 参数数量因技能而异

不同技能参数数量不同：
- homingKnife: 4 个参数 (cooldown, turnRate, lifetime, damage)
- dash: 4 个参数 (cooldown, speed, distance, damage)
- bladeBurst: 4 个参数 (cooldown, count, range, damage)
- clone: 3 个参数 (hpPercent, count, lifetime)
- summon: 3 个参数 (cooldown, count, monsterId)
- revive: 2 个参数 (hpPercent, delay)

**方案**：每个 SkillModule 自己解析自己的参数。Init(float[] rawParams) 收到原始数组，按自己的顺序取，越界用默认值。
或者 elementNum[] 用 key=value 格式：`"cooldown=3,turnRate=120,lifetime=5,damage=20"`，更易读但解析更复杂。

---

## 建议分步实施

| 阶段 | 内容 | 风险 |
|------|------|------|
| 1 | 创建 BossBrain + ISkillModule 接口 + HomingKnifeModule | 低，现有功能平移 |
| 2 | BossBrain 替换 BossBehavior_T1，测试 Excel 配置 | 低 |
| 3 | 加 DashModule，测试双技能 Boss | 中，EnemyAI 互斥 |
| 4 | 加 BladeBurstModule | 低 |
| 5 | 加 CloneModule + SummonModule + ReviveModule | 高，生命周期复杂 |
| 6 | 删除所有 BossBehavior_T* 类 | 低，清理 |

---

## 结论

**可行，核心风险可控。** 最大的坑是：
1. Excel elementNum[] 格式脆弱 → Editor 校验工具
2. 分身/复活涉及对象生命周期 → 需要 EnemyBase 的 Die() 重构
3. 技能模块和 EnemyAI 的移动互斥 → BossBrain.IsBusy 标志位

前两个 Boss（牛刀小试、剑气纵横）只有简单的周期性技能，实现成本低。分身和复活是后续阶段的事，不阻塞当前。
