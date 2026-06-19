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

---

## 阶段 5~7 详细设计（已确认）

### 前置：技能伤害类型体系

免伤技能需要对技能分类。当前 `SkillId` 枚举有 12 个主动技能，按伤害形态分成三组：

| 类型 | 枚举值 | 涵盖技能 |
|------|--------|----------|
| `Physical` | 物理 | AutoProjectile, AutoProjectilePistol, AutoProjectileSword, OrbitingBlades, ThrowGrenade, BouncingGrenade |
| `Energy` | 能量 | LineBeam, FieldGenerator, LightningStrike, AutoProjectileTalisman |
| `Explosive` | 爆炸 | HomingMissile, HomingMissileBasic |

实现方式：新增 `SkillDamageType.cs`，为每个 `SkillId` 扩展方法 `GetDamageType()`，返回枚举值。不改 `SkillId` 枚举本身——用扩展方法，对现有代码零侵入。

```csharp
public enum SkillDamageType { Physical, Energy, Explosive }

public static class SkillDamageTypeExtensions
{
    public static SkillDamageType GetDamageType(this SkillId id) => id switch
    {
        SkillId.AutoProjectile or SkillId.AutoProjectilePistol
            or SkillId.AutoProjectileSword or SkillId.OrbitingBlades
            or SkillId.ThrowGrenade or SkillId.BouncingGrenade
            => SkillDamageType.Physical,

        SkillId.LineBeam or SkillId.FieldGenerator
            or SkillId.LightningStrike or SkillId.AutoProjectileTalisman
            => SkillDamageType.Energy,

        SkillId.HomingMissile or SkillId.HomingMissileBasic
            => SkillDamageType.Explosive,

        _ => SkillDamageType.Physical
    };
}
```

---

### CloneModule（分身·改：可配置技能继承）

```
elementNum = "8,0.4,2,15,homingKnife|dash" = cooldown, hpPercent, cloneCount, lifetime, inheritSkills
```

| 参数位置 | 含义 | 默认值 |
|----------|------|--------|
| p[0] | cooldown 冷却时间(秒) | 8 |
| p[1] | hpPercent 分身血量比例(0~1) | 0.4 |
| p[2] | cloneCount 分身数量 | 1 |
| p[3] | lifetime 存活时间(秒，0=永久) | 12 |
| p[4] | inheritSkills 继承技能(\|分隔，"*"=全部，""=无) | "" |

**流程**：
1. `Execute()` → Boss 短暂闪光蓄力 0.35s → `IsBusy = true`
2. 解析 `inheritSkills` 参数 → 构建 `HashSet<string>` 保留集（永远排除 "clone"）
3. `Instantiate(boss.gameObject)` × `cloneCount`（夹角均分 + 环形分散半径 2.5~4.5，防止堆叠）
4. 克隆体 `BossBrain.RemoveAllModulesExcept(keepTypes)` — 只保留指定技能（空集则 Destroy BossBrain）
5. 覆盖血量、半透明、挂 `CloneMarker`、限时 `Destroy`

**配置示例**：
```
影武者:    elementNum=["8,0.35,2,15,homingKnife|dash"]    → 分身会追踪刀+冲刺
完全复制:  elementNum=["12,0.5,1,20,*"]                   → 分身继承全部(除分身)
白板分身:  elementNum=["8,0.3,3,10,"]                     → 空=无技能，和原来一样
```

---

### SummonModule（召唤·改：走 LevelWave 表）

```
elementNum = "12,0" = cooldown, reserveWaveId
```

| 参数位置 | 含义 | 默认值 |
|----------|------|--------|
| p[0] | cooldown 冷却时间(秒) | 12 |
| p[1] | reserveWaveId 待召唤波次 ID | 0 |

**设计变更**：召唤怪不再由 SummonModule 直接 Instantiate，改为触发 `SpawnerWaves.TriggerReserveWave(reserveWaveId)`。

在 LevelWave 表中配一行 `wave=0`（或其它 ≤0 的 wave 值）的怪物数据，`SpawnerWaves.WaveRoutine()` 自动跳过 `wave<=0` 的行存入 `_reserveWaves`，由 SummonModule 按需触发。

**优势**：攻血速防全部走 Excel 配表，生成逻辑复用 SpawnerWaves 的环形分散，关卡策划改数值不碰代码。

**LevelWave 配置示例**：
```
wave=0, monsterId=5, totalMonster=6, attack=15, maxHp=80, speed=3
wave=1, monsterId=1, totalMonster=20, ...
wave=2, monsterId=3, isBoss=true, ...
```

**流程**：
1. `Execute()` → Boss 举臂动作（闪光 + `IsBusy = true`）→ 蓄力 ~0.45s
2. `FindObjectOfType<SpawnerWaves>().TriggerReserveWave(reserveWaveId)`
3. SpawnerWaves 异步刷怪（环形分散 + WallStuckResolver），不发布 WaveChanged 事件
4. `IsBusy = false`

**SpawnerWaves 改动**：
- `WaveRoutine()` 中分离 `wave<=0` 行到 `_reserveWaves` 字典
- 新增 `TriggerReserveWave(int wave)` 公开方法
- `wave<=0` 不参与自动波次循环和 `BattleWavesCompletedEvent`

---

### ReviveModule（复活·被动触发）

```
elementNum = "0.5,1.5,1" = reviveHpPercent, reviveDelay, maxRevives
```

| 参数位置 | 含义 | 默认值 |
|----------|------|--------|
| p[0] | reviveHpPercent 复活后血量比例(0~1) | 0.5 |
| p[1] | reviveDelay 复活延迟(秒) | 1.5 |
| p[2] | maxRevives 最大复活次数 | 1 |

**流程**：
1. `ReviveModule.Init()` → hook `EnemyBase.OnDied.AddListener(OnBossDied)`
2. Boss 死亡 → `EnemyBase.Die()` 先调 `OnDied.Invoke()` → `ReviveModule.OnBossDied()` 被回调
3. 如果 `_reviveCount < maxRevives` → 设 `_eb.preventPoolDeath = true`；`Die()` 看到此标记 → 调 `HideForRevive()`（关碰撞+渲染+物理）→ `return`（不发布 `EnemyDiedEvent`，不回收）
4. `reviveDelay` 秒后 → `ApplyTableStats` 回血 → `ShowFromRevive()` 恢复 → 挂临时 `ResistShield`（全类型免疫 0.5s）→ 闪光+缩放脉冲 → `BattleVictoryBossTracker.RegisterBossSpawned()` 重新 +1
5. 所有复活次数用尽 → `OnBossDied` 不再拦截 → 正常 `Die()` 流程

**EnemyBase 改动**：
- +`preventPoolDeath` 字段（运行时标记，NonSerialized）
- `Die()` 重构：`OnDied.Invoke()` 移到 `EventBus.Publish` 之前，让 ReviveModule 有机会拦截
- +`HideForRevive()` / `ShowFromRevive()` 方法

**BattleVictoryBossTracker 联动**：复活拦截时 `Die()` 不发布 `EnemyDiedEvent`，tracker 不扣计数；复活后显式 `RegisterBossSpawned()` +1，保证复活后击杀仍能正常通关。

---

### ResistModule（免伤/抵抗）

```
elementNum = "4,14,0.6,Physical|Energy,3" = duration, cooldown, resistRatio, blockedTypes, maxTriggers
```

| 参数位置 | 含义 | 默认值 |
|----------|------|--------|
| p[0] | duration 免伤持续时间(秒) | 4 |
| p[1] | cooldown 冷却时间(秒) | 14 |
| p[2] | resistRatio 减伤比例(0~1，1=完全免疫) | 0.6 |
| p[3] | blockedTypes 阻挡类型(`\|`分隔) | "Physical" |
| p[4] | maxTriggers 最大触发次数(0=不限) | 3 |

**流程**：
1. `Execute()` → Boss 身上添加 `ResistShield` 组件 → Boss 叠加淡蓝色护盾视觉 → 设 `duration` 秒自毁定时器
2. `ResistShield.OnEnable()` 订阅 `EnemyDamagedEvent`（owner: this）
3. 每次受伤 → 判断 `e.damageSourceSkill.GetDamageType()` 是否在 `blockedTypes` 中 →
   - **命中**：在 `EnemyBase.TakeDamage` 中实际减伤 `damage × (1 - resistRatio)`，发布 `DamageResistedEvent`，`_triggerCount--`
   - **不命中**：正常扣血
4. `_triggerCount == 0` 或 `duration` 到期 → 销毁 shield，Boss 恢复正常外观
5. 新盾覆盖旧盾（再次 `Execute()` 时先销毁旧 `ResistShield`）

**飘血表现**：
- 被抵抗的伤害 → 灰色 `#999999` + `"免伤"` 后缀（如 `-15免伤`）
- 与暴击(金色+"暴击!")、破防(橙红+"破防!") 形成三色区分
- `DamageFloatText` 新增 `PlayResisted()` 方法；`DamageFloatTextPresenter` 订阅 `DamageResistedEvent`

**Boss 视觉**：有盾时 `SpriteRenderer.color` 叠加半透明蓝色（`new Color(0.27f, 0.53f, 1f, 0.3f)`），盾消即去。

**EnemyBase.TakeDamage 改动**：在防御减伤之后、扣血之前，检测 `GetComponent<ResistShield>()`，调用 shield 的 `ApplyResist(ref damage, skillSource)`。

---

### 新增文件清单

```
Assets/Script/Game/Combat/Skills/SkillDamageType.cs       伤害类型枚举 + 扩展方法
Assets/Script/Game/Enemies/Boss/Skills/CloneModule.cs      分身技能模块
Assets/Script/Game/Enemies/Boss/Skills/SummonModule.cs     召唤技能模块
Assets/Script/Game/Enemies/Boss/Skills/ReviveModule.cs     复活技能模块
Assets/Script/Game/Enemies/Boss/Skills/ResistModule.cs     免伤技能模块
Assets/Script/Game/Enemies/Components/CloneMarker.cs       克隆体标记（空组件）
Assets/Script/Game/Enemies/Components/ResistShield.cs      护盾运行时 MonoBehaviour
```

### 修改文件清单

```
Assets/Script/Game/Enemies/Boss/BossBrain.cs               +4 个 case + Revive 钩子
Assets/Script/Game/Enemies/Components/EnemyBase.cs         +preventPoolDeath + ResistShield 检查
Assets/Script/Game/Common/Events/GameEvents.cs             +DamageResistedEvent
Assets/Script/Game/UI/DamageFloatText.cs                   +灰色免伤飘字
Assets/Script/Game/UI/DamageFloatTextPresenter.cs          +订阅 DamageResistedEvent
```

---

### Boss 技能组合示例

```
牛刀小试      → ["homingKnife"]
战场老兵      → ["homingKnife", "summon"]
影武者        → ["dash", "clone"]
不死将军      → ["bladeBurst", "revive"]
元素克星      → ["zone", "resist(Physical|Energy)"]
最终兵器      → ["homingKnife", "dash", "bladeBurst", "clone", "summon", "revive", "resist"]
```
冲刺+分身
召唤+追踪弹
召唤+刀气爆发
召唤+冲刺
召唤+刀气爆发+冲刺
分身+追踪弹
分身+刀气爆发
分身+刀气爆发+冲刺
复活+刀气爆发

---

## 实施顺序

| 阶段 | 内容 | 风险 |
|------|------|------|
| 5 | `SkillDamageType` + `ResistModule` + `ResistShield` + 飘血 | 中，涉及 `EnemyBase.TakeDamage` 流程改动 |
| 6 | `CloneModule` + `SummonModule` | 中，对象生命周期 |
| 7 | `ReviveModule` + `EnemyBase.preventPoolDeath` | 高，死亡/复活状态机 + Boss 击杀判定 |
| 8 | 清理所有 `BossBehavior_T*` 旧类 | 低 |
