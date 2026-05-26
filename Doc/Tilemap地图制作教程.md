# Unity Tilemap 地图制作——纯小白教程

> 前置：已运行 `Tools/生成地图瓦片纹理`，瓦片在 `Assets/Res/Map/GeneratedTiles/`

---

## 第一步：打开 Tile Palette 面板

Unity 菜单 → `Window` → `2D` → `Tile Palette`

会弹出一个停靠面板，拖到 Scene 窗口旁边方便操作。

---

## 第二步：创建第一个 Tile Palette（第一章）

1. 在 Tile Palette 面板中，点击左上角 `Create New Palette` 按钮
2. 弹窗设置：
   - **Name**: `Chapter1_Tiles`
   - **Grid**: `Rectangle`（正方形格子，不要选 Hex）
   - **Cell Size**: `Automatic`
3. 选择保存路径：`Assets/Res/Map/Palettes/Style01/`（和现有文件放一起）
4. 点击 `Create`

此时 Tile Palette 面板是一片空白，左下角有个铅笔图标。

---

## 第三步：把瓦片纹理拖进 Palette

1. 打开 Project 窗口，导航到 `Assets/Res/Map/GeneratedTiles/`
2. **全选** 第一章的 4 个 tile：`ch1_grass`, `ch1_dirt`, `ch1_water`, `ch1_wall`
3. **拖拽** 到 Tile Palette 面板的空白区域
4. 弹窗问"你要把这些纹理存在哪个文件夹？"
   - 选择 `Assets/Res/Map/Palettes/Style01/` 或者新建 `Assets/Res/Map/Tiles/Ch1/`
   - 这步会为每个纹理生成一个 `.asset` 的 Tile 资源文件
5. 点击 `Save`

现在 Tile Palette 里应该能看到 4 个彩色小方块了。

---

## 第四步：在场景中刷地图

### 4.1 确保场景有 Tilemap 组件

打开 `Game.unity` 场景。检查 Hierarchy：

```
▼ Grid                          ← 如果没有，右键 Hierarchy → 2D Object → Tilemap → Rectangular
  ├─ Tilemap (改名为 Ground)     ← 地面层
  ├─ Tilemap (改名为 Obstacle)   ← 障碍/水域层
  └─ Tilemap (改名为 Wall)       ← 围墙边界层
```

**如果你的场景已经有 Grid 和 Tilemap，跳过这步。**

### 4.2 开始刷

1. 在 Tile Palette 面板，点击 `ch1_grass`（选中它）
2. 在 Toolbar 确认"画笔"工具激活（铅笔图标）
3. 在 Scene 窗口中，鼠标移到 Grid 上方
4. **单击** 一个格子 → 草皮上去了
5. **按住 Shift + 拖动** → 连续填充矩形区域
6. **按住 Shift + 点击一个已刷区域** → 擦除（橡皮擦）

### 4.3 分层刷

**关键概念——Tilemap 层级**：

每个 Tilemap 是一个"图层"。你在 Hierarchy 里选哪个 Tilemap，瓦片就刷到哪一层。

| Tilemap 层 | 刷什么 |
|------------|--------|
| Ground | 草皮（铺满全图） |
| Obstacle | 水域（不能走） |
| Wall | 围墙（边界） |

**操作顺序**：
1. Hierarchy 中点击 `Ground` → Palette 选 `ch1_grass` → 铺满全图
2. Hierarchy 中点击 `Ground` → Palette 选 `ch1_dirt` → 在草地中间画一条路
3. Hierarchy 中点击 `Obstacle` → Palette 选 `ch1_water` → 画几块水域
4. Hierarchy 中点击 `Wall` → Palette 选 `ch1_wall` → 围绕地图边缘画一圈墙

---

## 第五步：调整层级顺序

1. Hierarchy 中选中 `Grid`
2. 拖拽子 Tilemap 调整上下顺序：
   ```
   Grid
     ├─ Wall      ← 最上面
     ├─ Obstacle
     └─ Ground    ← 最下面
   ```
3. 顺序影响视觉遮挡（上面的盖住下面的，但通常瓦片不重叠，影响不大）
4. **真正的层级控制**在 Tilemap Renderer 组件：
   - 选中每个 Tilemap
   - Inspector 中 `Tilemap Renderer` → `Order in Layer`
   - Ground = 0, Obstacle = 1, Wall = 2

---

## 第六步：设置碰撞（让墙和水挡住人）

### 6.1 给 Tilemap 加碰撞

1. Hierarchy 中选中 `Wall` Tilemap
2. Inspector → `Add Component` → 搜索 `Tilemap Collider 2D`
3. 勾选 `Used By Composite`（如果有多段墙拼一起）
4. 同样给 `Obstacle` 加 `Tilemap Collider 2D`

### 6.2 给 Grid 加 Composite Collider（优化性能）

1. Hierarchy 中选中 `Grid`
2. `Add Component` → 搜索 `Composite Collider 2D`
3. 会自动添加 `Rigidbody 2D`
4. `Rigidbody 2D` 的 `Body Type` 设为 `Static`

### 6.3 添加 SpawnerWaves 的地图边界引用

1. 选中场景中挂载 `SpawnerWaves` 脚本的 GameObject
2. Inspector 中把 `Map Bounds Tilemap` 字段拖入 Ground Tilemap
3. 这确保怪物刷在地图内

---

## 第七步：多个关卡共用地图 or 各自独立地图

### 方案A：一个场景刷所有关卡的地图（推荐新手）

在同一个 `Game.unity` 中操作：

1. 选中要刷的 Tilemap，刷完一张小地图的全部格子
2. Hierarchy 中右键 Grid → `Copy`
3. 运行游戏时，根据 `ChapterLevel` 表的 `mapPath` 字段加载对应的 Prefab

**但你当前的做法是只有一个场景**，所有关卡共用同一个 Tilemap。如果想不同关卡不同地图：
- 每关单独做一个 Tilemap Prefab
- 或者代码里运行时切换 tile 数据

### 方案B：最简单——13 张地图都在一个大画布上

把每个关卡的区域隔开，比如：
- 关卡 1：x=0~30, y=0~20
- 关卡 2：x=40~70, y=0~20
- 关卡 3：x=80~110, y=0~20
- ...

运行时根据 `levelId` 动态设置摄像机位置和生成范围。

---

## 第八步：给不同章节换皮肤（重要）

你刚才生成了三套瓦片。每套创建一个 Tile Palette：

1. `Create New Palette` → 命名 `Chapter2_Tiles`
2. 拖入 `ch2_ground, ch2_stone, ch2_abyss, ch2_wall`
3. 重复创建 `Chapter3_Tiles`

画画时：
- 在 Tile Palette 面板顶部下拉框切换当前使用的 Palette
- 切换后画笔就变成另一套颜色

**或者在同一个 Palette 里放所有 tile**，每章选自己那几个颜色就行。新手推荐先放一起，省得切来切去。

---

## 常见踩坑合集

### 坑1：Tile 刷不上去
**原因**：你在 Scene 窗口点，但 Hierarchy 里没选中正确的 Tilemap。
**解决**：先在 Hierarchy 点一下你要画的那个 Tilemap 层。

### 坑2：Tile 刷了看不见
**原因**：Tilemap Renderer 的 `Order in Layer` 太小，被别的东西遮住了。
**解决**：调大 Order in Layer，检查 Sorting Layer 设置。

### 坑3：瓦片之间有缝隙
**原因**：Tile 纹理没有正确设置 Pixels Per Unit。
**解决**：选中 `ch1_grass` 图片 → Inspector → Pixels Per Unit 设为 32（和你的瓦片尺寸一致），Filter Mode 设为 Point（no filter）。运行一次 `Tools/生成地图瓦片纹理` 已自动设好了。

### 坑4：碰撞体不匹配
**原因**：Tilemap Collider 2D 默认每个 tile 生成一个碰撞体，太多会卡。
**解决**：加 `Composite Collider 2D`，把多个格子的碰撞体合并成一个大轮廓。记得 Tilemap Collider 2D 上勾选 `Used By Composite`。

### 坑5：删错了想回退
**解决**：Ctrl+Z 在 Tilemap 上同样有效。但在 `Generate Map Tiles` 工具中提前想好颜色，重刷比微调快。

### 坑6：复制粘贴 Tilemap 数据
1. Hierarchy 中选中 Tilemap
2. Inspector → 右上角三个点 → `Copy Component`
3. 选中另一个 Tilemap → 右上角三个点 → `Paste Component Values`
4. 这只复制设置，不复制瓦片数据

**要复制瓦片数据**：选中源 Tilemap，Ctrl+C，Ctrl+V → 会复制一个带所有瓦片的 Tilemap GameObject。

### 坑7：SpawnerWaves 的地图边界
在 `Game.unity` 中找一个叫 `GameLayer` 或挂 `SpawnerWaves` 脚本的 GameObject，Inspector 里有个 `Map Bounds Tilemap` 字段，必须拖入你的 Ground Tilemap，否则怪会刷到地图外面。

### 坑8：编辑时不小心移动了 Grid
Grid 的位置就是地图的世界原点。**不要移动 Grid**。如果偏移了，Inspector → Transform → 右键 → Reset。

### 坑9：生成瓦片后 UI 上数字或文字变方块
你的 TMP 字体和 Tile 纹理互不相关。这是两件事。如果 UI 文字变成方块，是你还没做字体裁剪那一步。

---

## 快速检查清单

刷完地图后在 Scene 窗口确认：

- [ ] Ground 层铺满了（没有黑窟窿 = 没有格子的位置）
- [ ] 地图外围有一圈 Wall 围栏
- [ ] Obstacle（水/岩浆/深渊）不会把路全堵死
- [ ] SpawnerWaves 的 `Map Bounds Tilemap` 已赋值
- [ ] 各层 `Order in Layer` 正确（Ground < Obstacle < Wall）
- [ ] 有 `Tilemap Collider 2D` + `Composite Collider 2D`
- [ ] 运行游戏 → 怪物刷在地图内、玩家不能走进水里

---

# 进阶篇

## 一、设置固定地图大小（告别目测）

Unity Tilemap 本身没有"地图尺寸"输入框，但你可以用三种方式约束：

### 方式A：围墙框定法（最简单）

用 Wall 瓦片画一个矩形框，框内就是地图。框多大你说了算。

**精确操作方法**：
1. Scene 窗口顶部工具栏，把 `Grid` 的 Snap 设置打开：
   - 点击 `Grid Snapping` 图标（或者 `Edit` → `Grid and Snap Settings`）
   - 确认 Grid 的 cell size 是 1×1
2. 画边框时，**看着 Scene 窗口右下角的坐标读数**（Grid 坐标会显示当前鼠标指向的格子位置）
3. 比如你想做 20×15 的地图：Wall 画在 x=-1, x=20, y=-1, y=15 四条线

### 方式B：Camera 锁定法

如果地图比屏幕大，角色走到边缘时摄像机不该看到地图外面的黑边。

给摄像机加一个边界脚本：

```csharp
// CameraClamp.cs — 挂到 Main Camera 上
using UnityEngine;

public class CameraClamp : MonoBehaviour
{
    public Vector2 mapMin = new Vector2(0, 0);   // 地图左下角
    public Vector2 mapMax = new Vector2(20, 15); // 地图右上角

    void LateUpdate()
    {
        var cam = GetComponent<Camera>();
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        var pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, mapMin.x + halfW, mapMax.x - halfW);
        pos.y = Mathf.Clamp(pos.y, mapMin.y + halfH, mapMax.y - halfH);
        transform.position = pos;
    }
}
```

Inspector 里填上你的地图范围就行。

### 方式C：用代码生成 Tilemap（不手刷）

如果 13 关都想要不同布局，手刷 13 次会疯。用一个 Editor 工具从文本文件生成地图：

```
# map_1001.txt（用字符表示地形）
WWWWWWWWWWWWWWWWWWWW
WGGGGGGGGGGGGGGGGGGW
WGGGGDDDDDDGGGGGGGW
WGGGGDGGGGDGGGGGGGW
WGGGGDGGGGDGGGGOOOW
WGGGGDDDDDDGGGGGOOW
WWWWWWWWWWWWWWWWWWWW

W = Wall, G = Grass, D = Dirt, O = Water
```

写一个脚本遍历字符数组 → SetTile。这个可以后续做，先用手刷把前 3 关搞定。

---

## 二、让地图变好看（入门 → 够用）

纯色瓦片功能上没问题，但看起来像测试关卡。几个低成本改进：

### 2.1 同一地形放 3-4 个变体

不是整片地草一个色，而是做 3 个稍有偏差的草地贴图：

```
ch1_grass_a  ← 基础
ch1_grass_b  ← 略微偏黄/偏亮
ch1_grass_c  ← 略有纹理
```

刷的时候随机点，或者用 Weighted Random 刷。手工操作就是三个颜色换着点，10 秒一层。

**升级生成脚本**：修改 `GenerateMapTiles.cs`，为 grass 生成多个变体：

```csharp
// 以草地为例，生成 3 个变体
GenerateSolid("ch1_grass_a", new Color(0.29f, 0.58f, 0.30f));
GenerateSolid("ch1_grass_b", new Color(0.31f, 0.60f, 0.32f));
GenerateSolid("ch1_grass_c", new Color(0.28f, 0.56f, 0.29f));
AddNoise(Path.Combine(OutputDir, "ch1_grass_a.png"), 0.03f);
AddNoise(Path.Combine(OutputDir, "ch1_grass_b.png"), 0.04f);
AddNoise(Path.Combine(OutputDir, "ch1_grass_c.png"), 0.05f);
```

### 2.2 加装饰瓦片（不参与碰撞）

在地面层刷完基础地形后，再新建一个 Decoration 层：

```
Grid
  ├─ Wall (碰撞)
  ├─ Obstacle (碰撞)
  ├─ Decoration (不碰撞！纯视觉)   ← 新增
  └─ Ground (碰撞可选)
```

Decoration 层可以画：
- 石头、花草（做几个小 icon，不用碰撞）
- 地板的裂缝/纹理线条
- 路标、箭头

**Decoration 层不加 Tilemap Collider 2D**，只有视觉效果。

### 2.3 水/岩浆加简单动画

如果想让水动起来，Unity 自带 Animated Tile 功能：

1. 生成 4 张水的变体（色相微微不同）
2. 选中这 4 张 → Assets → Create → 2D → Tiles → Animated Tile
3. 在 Inspector 里设置每帧速度

代码生成动画帧变体：

```csharp
// 为水生成 4 帧动画
for (int i = 0; i < 4; i++)
{
    float offset = (i - 1.5f) * 0.02f;
    GenerateSolid($"ch1_water_f{i}", 
        new Color(0.38f + offset, 0.70f + offset, 0.95f + offset));
}
```

### 2.4 Rule Tile 自动边缘过渡

这是 Tilemap 最强大的功能。Rule Tile 会根据相邻格子自动选择不同的贴图。

**例子**：草地挨着水，自动画一条泥泞的过渡边。

但 Rule Tile 的学习成本较高，**建议先做 2.1 和 2.2，上线后有余力再学**。

### 2.5 光照叠加（零成本）

不用任何额外资源，调一下 Tilemap 颜色就能区分区域：
- 安全区：正常颜色
- 危险区：在另一个 Tilemap 上画半透明红色叠加
- 出口/目标点：画一个亮色标记

直接在 Inspector 里调 Tilemap Renderer 的 Color 属性（改 alpha 可以半透明叠加）。

---

## 三、地图设计原则（文字割草专用）

### 3.1 尺寸建议

| 关卡阶段 | 建议尺寸 | 理由 |
|----------|----------|------|
| 前三关（新手） | 20×15 | 小空间，学会打字 |
| 中期 | 25×20 | 有走位空间 |
| 后期 | 30×25 | 大战场 |

> 微信小游戏屏幕一般是 750×1334 逻辑像素。摄像机 orthographicSize=10 时能看到约 20×18 格。地图比屏幕大一圈刚好。

### 3.2 不要做的

- ❌ 一整张纯色大平原（无聊）
- ❌ 狭窄通道 + 大量怪（堵死，没走位空间 = 纯拼手速）
- ❌ 死胡同（进去就出不来）
- ❌ 水/障碍占地图 40% 以上（走位空间不够）

### 3.3 要做的

- ✅ 中间开阔、四周有遮挡（地形提供掩护）
- ✅ 地图上有 2-4 个"兴趣点"（柱子、石堆、草丛）可以用来绕怪
- ✅ 水/障碍形成走廊，引导敌人流向
- ✅ 出生点（玩家起始位置）附近安全区域够大

---

## 四、进阶检查清单

- [ ] 地图有明确边界（墙围死）
- [ ] 主摄像机有边界限制
- [ ] 每种地形有 2-3 个变体贴图
- [ ] Decoration 层有装饰物且无碰撞
- [ ] 出生点周围至少有 3×3 空地
- [ ] 水域/障碍不超过 30% 面积
- [ ] 地图有至少 2 条可行走的路径
