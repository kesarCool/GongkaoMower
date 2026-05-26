# Boss 怪物设计文档

## 一、现有架构梳理

### 当前怪物数据流

```
配表 Monster.bytes (FlatBuffer)
  ├─ monsterId      ← 全局唯一
  ├─ type           ← 1=文字怪（当前只有这一种）
  ├─ CategoryTag    ← 词库分类标签
  ├─ name           ← 怪物名
  ├─ pic            ← 图片路径（当前未使用，预留）
  ├─ path           ← prefab 路径
  ├─ describe       ← 描述文本
  ├─ element[]      ← 元素/技能数组（当前未使用）
  ├─ elementNum[]   ← 元素数值数组（当前未使用）
  ├─ ImmuneEffect[] ← 免疫效果数组（当前未使用）
  ├─ triggerMode    ← 触发模式（当前未使用）
  └─ randomKillPro  ← 击杀奖励概率

         ↓

SpawnerWaves 刷怪
  ├─ LevelWave 表指定 monsterId
  └─ SpawnOne() → EnemyCatalog.TryGet(id) → Instantiate(prefab) → InitFromDefinition(def)

         ↓

运行时组件链
  EnemyBase          ← id, name, speed, hp, damage, reward, OnDamaged, OnDied, Die()
    ├─ EnemyAI       ← 追玩家 + 碰撞伤害
    ├─ EnemyRanged   ← 定时发射子弹（继承 EnemyBase）
    ├─ EnemyStats    ← hp/maxHp 数据容器
    ├─ EnemyWordLabel← TextMesh 显示词条文字
    └─ PooledObject  ← 对象池回收
```

### 关键发现：表结构已预留 Boss 字段

`LevelWave` 表已有：
- `isBoss` (bool) — 标记 Boss 波次
- `quantityBoss` (int) — Boss 数量
- `timeStart` (int) — 开场等待（秒）
- `waveTimeContinue` (int) — 波次持续时间

`Monster` 表已有预留字段：
- `triggerMode` — 可用于指定行为模式 ID
- `element[]` — 可用于配置技能列表
- `ImmuneEffect[]` — 免疫类型

**Boss 不需要改表结构。现有字段够用。**

---

## 二、Boss 全局设计

### 2.1 Boss 等级体系

| 等级 | 名称 | 解锁关卡 | 特征 |
|------|------|----------|------|
| T1 | 牛刀小试 | 第1关 | 1 个技能：追踪小刀 |
| T1+ | 牛刀小试·强 | 第2关 | 2 个技能：追踪小刀 + 冲刺 |
| T2 | 剑气纵横 | 第3关 | 1 个技能：周身刀气爆发 |
| T3 | 刀光剑影 | 第4关 | 3 个技能：刀气 + 分身 + 召唤 |

### 2.2 Boss 与小怪的区别

| 维度 | 小怪 | Boss |
|------|------|------|
| 血量 | 10-50 | 200-800 |
| 速度 | 1.5-3.5 | 1.0-2.0（更大更慢） |
| 体形 | 单行文字 | 大字 + 装饰图 |
| 技能 | 0-1（远程射子弹） | 1-3 个 |
| 显示 | TextMesh 7 字 | TextMesh 大字 + 角/翼等装饰 Sprite |
| 击杀奖励 | 1 击杀数 | 10 击杀数 + 掉落 |
| 波次 | 普通波（2-4 波） | 最后一波（isBoss=true） |

---

## 三、Boss 怪物编号与命名

### 3.1 Monster 表新增行

```
monsterId  name        type  CategoryTag  triggerMode  describe
10001      (现有小怪)    1     (词汇分类)    0            普通文字怪
20001      牛刀小试      2     -            1            放出追踪小刀
20002      牛刀小试·强   2     -            2            追踪小刀+冲刺
20003      剑气纵横      3     -            3            周身刀气爆发
20004      刀光剑影      3     -            4            刀气+分身+召唤
```

`type` 字段语义扩展：
- `type=1` → 普通文字怪（使用 LexiconTable 词条）
- `type=2` → T1 Boss（name 字段直接写死，不走词库）
- `type=3` → T2/T3 Boss

`triggerMode` → 映射到 Boss 行为脚本：
- `0` → 无特殊行为（普通怪用 EnemyAI）
- `1` → BossBehavior_T1 （追踪小刀）
- `2` → BossBehavior_T1Plus （追踪小刀 + 冲刺）
- `3` → BossBehavior_T2 （刀气爆发）
- `4` → BossBehavior_T3 （刀气 + 分身 + 召唤）

### 3.2 LevelWave 表 Boss 波次配置示例

```
levelId  wave  monsterId  totalMonster  attack  maxHp  isBoss  quantityBoss  timeStart  intervalSpawn
1001     3     20001      1             20      500    true    1             3          0.5
1002     4     20002      1             30      600    true    1             3          0.5
```

第 1 关（levelId=1001）的最后一波（wave=3）出 1 个牛刀小试，攻击 20，血量 500。

---

## 四、Boss Prefab 结构设计

### 4.1 牛刀小试 Prefab（boss_20001.prefab）

```
boss_20001 (根节点)
  ├─ EnemyBase           ← id/name/hp/speed/damage
  ├─ BossBehavior_T1     ← {attackInterval, knifeSpeed, knifePrefab}
  ├─ EnemyAI             ← 追玩家（比小怪慢）
  ├─ EnemyStats          ← hp/maxHp
  ├─ Rigidbody2D
  ├─ CircleCollider2D    ← 碰撞体（比小怪大一圈）
  ├─ PooledObject
  │
  ├─ Visual (空节点，挂显示组件)
  │   ├─ EnemyWordLabel  ← TextMesh 大字
  │   ├─ LeftHorn        ← SpriteRenderer + 左侧"牛角"图片
  │   └─ RightHorn       ← SpriteRenderer + 右侧"牛角"图片
  │
  └─ AbilityRoot (空节点，技能发射点)
      └─ KnifeSpawnPoint ← 小刀生成位置（向前偏移）
```

**"牛角"视觉**：两个小 SpriteRenderer，挂在 TextMesh 两侧。位置手动调，让它们看起来像从文字上方伸出来的角。不需要动画，静态贴图就行。

**Prefab 路径**：`Assets/Res/Prefabs/Boss/boss_20001.prefab`

### 4.2 后续 Boss Prefab 结构差异

| Boss | 额外组件 | 额外子节点 |
|------|----------|-----------|
| 牛刀小试·强 | BossBehavior_T1Plus | 同 T1 |
| 剑气纵横 | BossBehavior_T2 | 刀气发射点 × 8（环形一圈） |
| 刀光剑影 | BossBehavior_T3 | 刀气发射点 × 8 + CloneSpawnPoints + SummonSpawnPoint |

所有 Boss 共用同一个 Visual 结构模板：TextMesh 大字 + 两侧装饰图。不同 Boss 换不同的装饰图（角、翅膀、光环等）。

---

## 五、Boss 技能详细设计

### 5.1 追踪小刀（T1 牛刀小试）

```
触发方式： 每 N 秒自动释放（attackInterval）
目标：     玩家当前位置
表现：     从 Boss 前方发射 1 把刀形子弹
          子弹追踪玩家（速度较慢，会拐弯）
          碰到玩家造成伤害，碰到墙壁消失
          持续追踪 5 秒后自毁

参数：
  attackInterval: 3s（每 3 秒放一次）
  knifeSpeed: 4f（比普通子弹慢，但会追踪）
  knifeLifetime: 5s（5 秒后消失）
  knifeDamage: 20
  knifePrefab: 刀形子弹预制体

与现有 EnemyRanged 的区别：
  EnemyRanged 发射直线子弹（Set velocity，不拐弯）
  追踪小刀需要每帧调整方向 → 需要新的 Bullet 脚本或追踪逻辑
```

**小刀 Prefab**：一个带 Trail 的菱形 Sprite，挂 `HomingBullet` 脚本。

```
HomingBullet:
  speed: 4f
  lifetime: 5s
  damage: 20
  targetTag: "Player"
  turnRate: 120f（每秒最多转 120 度）
  
  Update():
    if (target exists):
      dir = (target - position).normalized
      // 平滑转向，不是瞬间锁定
      currentDir = RotateTowards(currentDir, dir, turnRate * dt)
      velocity = currentDir * speed
    lifetime -= dt; if <= 0 → destroy
```

### 5.2 冲刺（T1+ 牛刀小试·强 追加技能）

```
触发方式： 每 6 秒，当玩家在一定距离外时触发
表现：     Boss 短暂蓄力（0.3 秒原地不动 + 红色闪烁）
          然后以极快速度向玩家当前位置直线冲刺
          冲刺距离固定（约 8 个单位）
          碰到墙壁或冲刺结束停止
          冲刺路径上的玩家受到 30 点伤害

参数：
  dashInterval: 6s
  dashSpeed: 20f（极快）
  dashDistance: 8f
  chargeTime: 0.3s（蓄力时间，期间不能移动）
  dashDamage: 30
```

**与现有 EnemyAI 的关系**：冲刺时 EnemyAI 的 FixedUpdate 追玩家逻辑需要暂停。`BossBehavior_T1Plus` 控制冲刺期间的移动。

```
BossBehavior_T1Plus 状态机:
  [Idle] → 追玩家(EnemyAI) + 每隔 attackInterval 发追踪小刀
  [DashReady] → 如果 cooldown 到 + 玩家在范围内
  [Charging] → 0.3s 原地不动，播放闪烁
  [Dashing] → 直线冲刺，忽略碰撞伤害（冲刺自带伤害判定）
  → 冲刺结束 → 回到 [Idle]
```

### 5.3 刀气爆发（T2 剑气纵横）

```
触发方式： 每 8 秒，当玩家在一定距离内时触发
表现：     Boss 蓄力 0.5 秒
          向周身 8 个方向（每 45°）同时发射刀气子弹
          刀气直线飞行，不追踪
          碰到玩家造成伤害，碰到墙壁消失
          飞行距离 6 个单位

参数：
  bladeBurstInterval: 8s
  bladeCount: 8（环形均匀分布）
  bladeSpeed: 7f
  bladeRange: 6f
  bladeDamage: 25
  chargeTime: 0.5s
```

**环形发射点**：Boss 子节点放 8 个 `Transform`，均匀 45° 分布。

```
function BladeBurst():
  for i in 0..7:
    angle = i * 45°
    dir = (cos(angle), sin(angle))
    spawn blade at Boss position
    blade.velocity = dir * bladeSpeed
```

### 5.4 分身（T3 刀光剑影 技能 1）

```
触发方式： Boss 血量降到 50% 时触发一次
表现：     Boss 原地消失（0.2 秒）
          在地图随机 2 个位置生成 2 个分身
          分身有 Boss 30% 的血量，外观相同但半透明
          分身只会追玩家（无技能）
          同时存在不超过 2 个
          分身在 15 秒后自动消失

参数：
  triggerHpPercent: 0.5（血量 50% 触发）
  cloneCount: 2
  cloneHpPercent: 0.3（分身血量比例）
  cloneLifetime: 15s
  cloneAlpha: 0.5（半透明）
```

### 5.5 召唤小怪（T3 刀光剑影 技能 2）

```
触发方式： 每 12 秒触发
表现：     Boss 播放召唤动作
          在地图随机位置生成 3 个普通文字怪
          召唤的小怪由 LevelWave 表的普通 monsterId 指定

参数：
  summonInterval: 12s
  summonCount: 3
  summonMonsterId: 10001（普通文字怪）
```

### 5.6 死亡复活（T3 刀光剑影 被动）

```
触发方式： Boss 死亡时自动触发（仅一次）
表现：     Boss 倒下后 1 秒，原地复活
          回复 30% 血量
          复活时短暂无敌（1 秒）
          复活后立即释放一次刀气爆发（警告玩家）

参数：
  reviveHpPercent: 0.3
  reviveDelay: 1s
  invincibleDuration: 1s
  reviveBurst: true（复活时触发刀气）
```

---

## 六、Boss UI 设计

### 6.1 Boss 血条

Boss 出场时屏幕顶部显示 Boss 血条：

```
┌──────────────────────────────┐
│ 🐂 牛刀小试                  │
│ ████████████████░░░░░░░ 75%  │
└──────────────────────────────┘
```

位置：顶部居中，宽度约屏幕 80%。
显示条件：`LevelWave.isBoss == true` 且 Boss 存活。
隐藏条件：Boss 死亡。

### 6.2 Boss 出场提示

Boss 波次开始前 1 秒，屏幕中央弹大字：

```
⚠ 牛 刀 小 试 ⚠
```

1.5 秒后淡出，然后 Boss 生成。

---

## 七、数据配置对照表

### 7.1 Monster 表新增行

```
ID  monsterId  type  name           triggerMode  describe
10  20001      2     牛刀小试        1            放出追踪小刀
11  20002      2     牛刀小试·强     2            追踪小刀 + 冲刺
12  20003      3     剑气纵横        3            周身刀气爆发
13  20004      3     刀光剑影        4            刀气 + 分身 + 召唤
```

### 7.2 LevelWave 表 Boss 波次配置（示例）

```
第 1 关 (levelId=1001):
  wave 1: monsterId=10001, totalMonster=20  (普通小怪)
  wave 2: monsterId=10001, totalMonster=30  (普通小怪)
  wave 3: monsterId=20001, totalMonster=1,  isBoss=true, attack=20, maxHp=500

第 2 关 (levelId=1002):
  wave 1: monsterId=10001, totalMonster=25
  wave 2: monsterId=10001, totalMonster=35
  wave 3: monsterId=10001, totalMonster=40
  wave 4: monsterId=20002, totalMonster=1,  isBoss=true, attack=30, maxHp=600

第 3 关 (levelId=1003):
  wave 1: monsterId=10001, totalMonster=30
  wave 2: monsterId=10001, totalMonster=40
  wave 3: monsterId=10001, totalMonster=50
  wave 4: monsterId=20003, totalMonster=1,  isBoss=true, attack=40, maxHp=800
```

### 7.3 EnemyCatalog 新增条目（Inspector 配置）

```
id=20001  name=牛刀小试     prefab=boss_20001  moveSpeed=1.5  maxHp=500  damage=20
id=20002  name=牛刀小试·强  prefab=boss_20002  moveSpeed=1.5  maxHp=600  damage=30
id=20003  name=剑气纵横     prefab=boss_20003  moveSpeed=1.2  maxHp=800  damage=40
id=20004  name=刀光剑影     prefab=boss_20004  moveSpeed=1.0  maxHp=1200 damage=50
```

---

## 八、需要新增的代码文件

| 文件 | 用途 |
|------|------|
| `BossBehaviorBase.cs` | Boss 行为组件基类（抽象） |
| `BossBehavior_T1.cs` | 追踪小刀（继承 BossBehaviorBase） |
| `BossBehavior_T1Plus.cs` | 追踪小刀 + 冲刺 |
| `BossBehavior_T2.cs` | 刀气爆发 |
| `BossBehavior_T3.cs` | 刀气 + 分身 + 召唤 + 复活 |
| `HomingBullet.cs` | 追踪子弹（挂到小刀 prefab 上） |
| `BossUI.cs` | Boss 血条显示 |
| `BossSpawnAnnounce.cs` | Boss 出场字幕 |

不需要新增资源文件：装饰图（牛角等）用现有 `Res/image/` 下的贴图或生成新的 32x32 纯色图。小刀子弹用三角/菱形 Sprite。

---

## 九、与现有系统的衔接点

### 9.1 SpawnerWaves 改动

`SpawnOne()` 调用 `MonsterWordSpawnBinding.TryApply()` — Boss 不需要词条绑定。判断 `type != 1` 时跳过：

```csharp
// MonsterWordSpawnBinding.TryApply 中
if (monster.type != MonsterTypeIds.Word) return;  // Boss type=2/3 跳过词库
```

### 9.2 Boss 的 TextMesh 显示

仍然用 `EnemyWordLabel.SetWord()`，但 Boss 的 `name` 直接写在 Monster 表里（不查词库）。SpawnerWaves 的 `MonsterWordSpawnBinding.TryApply()` 跳过 Boss 类型后，Boss 的 `SetRuntimeDisplayName()` 直接取 `Monster.name`。

### 9.3 Boss 生成位置

Boss 从地图中央偏上的位置生成（独立于普通怪的环形分布）。`SpawnerWaves` 里 `lineSpawn=0` 走环形生成，需要让 Boss 用固定位置或 `lineSpawn=某个新值`。

方案：`lineSpawn=5` → Boss 专用，生成在地图中央偏上。

### 9.4 Boss 击杀事件

`EnemyBase.Die()` 已发布 `EnemyDiedEvent`。Boss 死亡后需要额外处理（如通关判定）。在 `Die()` 中判断 `isBoss` 标记：

```csharp
EventBus.Publish(new BossDiedEvent { enemy = this, bossId = enemyId });
```

现有的 `BattleOutcomeCoordinator` 已监听怪物死亡事件计算通关条件，加 Boss 事件即可。

---

## 十、Boss 美术资源清单

| 资源 | 类型 | 规格 | 说明 |
|------|------|------|------|
| 牛角装饰 | Sprite | 32×32 | Boss 头部两侧角 |
| T2 装饰（剑翼） | Sprite | 32×32 | 剑气纵横 Boss 两侧翼 |
| T3 装饰（光环） | Sprite | 64×64 | 刀光剑影 Boss 周身光环 |
| 小刀子弹 | Sprite | 16×16 | 菱形/三角形 |
| 刀气子弹 | Sprite | 16×32 | 竖条形 |
| Boss 出场字幕背景 | 无 | - | 纯 Text 大字即可 |

所有美术可用临时纯色方块代替，先跑通逻辑再替换。
