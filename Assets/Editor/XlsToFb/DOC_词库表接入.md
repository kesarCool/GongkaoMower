# 打表工具接入说明（NPOI + FlatBuffers，`.xls`）

> 管线：**Excel (.xls)** → 内存 **`Table` 解析** → **`.fbs` 描述** → **`flatc.exe` 生成 C#** → **`FlatBufferBuilder` 写出二进制** → 运行时 **`TableManager` + `Resources`**。  
> 与 `Assets/Script/Game/Common/Config/Lexicon` 下 **CSV→ScriptableObject** 为并行方案时，**以本管线为配表主数据源**；词库可只维护 **一份 `.xls`**。

---

## 0. 工作原理（端到端）

1. **入口**
   - 批量：`[TM工具集]/xls转cs` → `Xls2FBWindow.ConvertXls` → 对目录下每个符合条件的 `.xls` 调用 `Convert`。
   - 窗口：`转表工具/xls转txt` 打开 `Xls2FBWindow`（列表、勾选、单表转换等）。

2. **枚举 xls（当前逻辑）**
   - 默认扫描：**`../client/Share/table/xls/`**（相对 **Unity 工程根目录 / 当前工作目录**）。
   - 仅匹配文件名以 **`c.xls`** 或 **`cs.xls`** 结尾的表（`FindFile` 内 `Where`）。
   - `FileSystemWatcher` 监视同一路径，用于标记「文件已变更」（部分逻辑已注释）。

3. **单次 `Convert`**
   - 用 **MD5**（`文件名.xls.MD5`）判断是否跳过；`ignoremd5=true` 时强制重转。
   - **NPOI `HSSFWorkbook`** 读 `.xls`，取 **`GetSheetAt(0)`**。
   - 若 `UseSplitTable()` 为 true：`MergeTableArray` 按 `XlsxDataUnit.splitXls` 把多张物理表合并成 `List<ISheet>`；否则只有首 Sheet。当前 `splitXls` 为 **空数组**，合并列表即为 **单 Sheet**。
   - **`xls.Table.ParserFrom`**：按表头行约定解析列（required/repeated、类型、字段名、cryptic 等），得到 **`Table` 内存模型**。
   - **`fb.GenerateDesc(table)`**：写 **`Assets/Editor/XlsToFb/fbs/{表名}.fbs`**，在 **`Assets/Editor/XlsToFb`** 下启动进程 **`flatc.exe --csharp --gen-onefile -o ../../../../Script/Table/ProtoTable ...`**，生成 **`Assets/Script/Table/ProtoTable/{表名}.cs`**（命名空间 **`ProtoTable`**，与 `Union.cs` / `FlatBufferArray.cs` 一致）。
   - **`fb.DumpData(table)`**：用 **`FlatBufferBuilder`** 按行写入向量与表，得到 **`byte[]`**，写入 **`Assets/Res/Data/table_fb/{表名}.bytes`** 并 `ImportAsset`。
   - 转换结束后还会 **`new XlsxDataUnit(filename)`** 并在 `UseSplitTable()` 时调用 **`XlsxDataUnit.MergeTables`**（与拆表配置相关；`splitXls` 为空时影响很小）。

4. **运行时**
   - **`TableManager`**（`MonoSingleton`）：`Resources.Load<TextAsset>("Data/table_fb/" + 类型名)`，用 **`FlatBuffers.Table` + `ByteBuffer`** 解根向量，反射调用每行类型的 **`__assign`**，按 **`ID`** 填入字典。
   - 因此 **二进制资源** 必须出现在 **`Assets/Resources/Data/table_fb/{表名}.bytes`**（无后缀传参），与当前写出目录 **`Assets/Resources/Data/table_fb/`** 若未做拷贝或改路径，会导致 **加载不到**——见下文「需注意」。

5. **另存批处理（少用）**
   - `Xls2FB.GenerateFbs()`（`Xls2FB.cs`）曾用于扫 **`../Share/xls/`**；与窗口用的 **`../client/Share/table/xls/`** 不是同一路径，避免混用。

---

## 1. 当前工程状态与需注意

| 项目 | 状态 |
|------|------|
| 配表格式 | **`.xls`**（代码里对 **`.xlsx` 直接跳过**）；`XlsxDataManager` 中仍有 XSSF 引用，主流程为 HSSF |
| NPOI | DLL 在 **`Assets/Plugins/NPOI`**，**PluginImporter 已勾选 Editor**，Editor 脚本可引用 |
| FlatBuffers 运行时 | **`Assets/Editor/FlatBuffer`**：`ByteBuffer.cs`、`Table.cs`（class）、`IFlatbufferObject.cs`、`FlatBufferConstants.cs` |
| FlatBuffers 构建侧 | **`Assets/Editor/XlsToFb`**：`FlatBufferBuilder.cs`、`Offset.cs`、`Struct.cs`，与 `fb.DumpData` 配套 |
| `TableManager` 加载 | 已使用 **`Resources.Load<TextAsset>`**（**不再依赖 HotResMgr**） |
| **`TableManager` 程序集** | 源文件在 **`Assets/Editor/XlsToFb/TableManager.cs`** → 归属 **Editor** 程序集，**不会打进 Player**。若正式运行时需要读 FlatBuffer 表，请将 **`TableManager`（及仅运行时需要的解析代码）迁到 `Assets/Script` 等非 Editor 目录**，或抽取共享加载逻辑。 |
| **Resources vs `Res/`** | 生成路径为 **`Assets/Res/Data/table_fb/*.bytes`**，加载路径为 **`Data/table_fb/`（Resources）** → 需统一：**改 `fb.cs` 输出目录** 或 **构建/手工同步到 `Assets/Resources/Data/table_fb/`** |
| 输入目录 | **`../client/Share/table/xls/`**（`ConvertXls` / `FindFile` / `FileSystemWatcher`）与 **`Xls2FB` 中的 `../Share/xls/`** 并存，建议团队收敛到 **单一配置**（常量或 EditorPrefs） |
| 运行时程序集 | 生成的 **`ProtoTable/*.cs`** 目标为 **`Assets/Script/Table/ProtoTable/`**，须落在 **Player 可编译程序集**（勿仅放在 `Editor` 下） |

---

## 2. 词库表命名（与生成代码强绑定）

- Excel **Sheet 名** / 首表解析名 → **`Table.tablename`** → **FlatBuffers 根表名** → **生成的 C# 类名** → **`.bytes` 文件名**（不含扩展名）。
- 建议使用：**`LexiconTable`**（与历史 `BaseWordTable` 二选一并全局统一）。
- **首 Sheet**：主流程 **`GetSheetAt(0)`**；合并拆表时由 `ParserFrom(sheetList, ...)` 多 Sheet 逻辑决定。

---

## 3. Excel 行列约定（与 `Table.ParserFrom` 一致）

列从左到右；行从 **Excel 第 1 行**起：

| Excel 行 | 含义 |
|----------|------|
| 第 1 行 | **required / repeated** |
| 第 2 行 | **类型**（解析取 `:` 前主类型） |
| 第 3 行 | **字段名**（解析取 `:` 前） |
| 第 4 行 | **说明 / cryptic**：若某列为 **`cryptic`** 则整表 cryptic，说明顺延 |
| 第 5～6 行 | cryptic 时数据再从下一行开始；**未开 cryptic 时数据一般从第 5 行开始** |

**数据结束**：某行 **第 1 列为空** → 该行及之后不导入。  
**首列**：第一列通常为 **`ID`**（类型会被规范为 **`sint32`**），与 `TableManager` 字典键一致。

---

## 4. 词库列定义（建议）

> 与策划 CSV 列语义对齐，便于迁移；类型需符合 `Table`/`fb.DumpData` 已支持类型。

| 第 1 行 | 第 2 行 type | 第 3 行 name | 说明 |
|---------|--------------|--------------|------|
| required | sint32 | ID | 主键，唯一 |
| required | string | DisplayText | 文字怪显示正文 |
| required | enum | ContentLine | 见下「枚举写法」 |
| required | string | CategoryTag | 分类标签 |
| required | sint32 | RarityOrTier | 0/1/2 |
| required | sint32 | MinWave | 从第几波；可 1 |
| required | sint32 | MaxWave | 最后一波；**不限**建议填 `2147483647` 或团队约定哨兵 |
| required | realfloat | Weight | 权重（或改用 sint32，按现有表习惯） |
| required | sint32 | AllowElite | 0/1 |
| required | string | ThemePackId | 可填空字符串 |
| required | string | Locale | 如 zh-CN |
| required | string | Notes | 备注 |

**`ContentLine`（enum）列**：第 2 行类型为 **`enum`**，第 3 行名为 **`ContentLine`**；**第 4（或 5）行「说明」单元格内用多行文本定义枚举**，每行格式：

```text
枚举成员名:整型值:中文说明
```

示例（写在 **对应列** 的说明格里，多行）：

```text
Funny:0:搞笑
LightKnowledge:1:轻知识
```

（具体换行在 Excel 单元格内 Alt+Enter。）

---

## 5. 导表产出路径（以 `fb.cs` 为准）

| 产物 | 路径 |
|------|------|
| `.fbs` | `Assets/Editor/XlsToFb/fbs/{表名}.fbs` |
| `flatc` 工作目录 | `Assets/Editor/XlsToFb`（进程内 `SetCurrentDirectory`） |
| 生成 C# | `Assets/Script/Table/ProtoTable/{表名}.cs` |
| `.bytes`（当前代码） | `Assets/Resources/Data/table_fb/{表名}.bytes` |
| **运行时 Resources** | `TableManager` 使用 **`Data/table_fb/{表名}`** → 应对应 **`Assets/Resources/Data/table_fb/{表名}.bytes`** |

**依赖**：`Assets/Editor/XlsToFb/flatc.exe` 须存在且可执行；`cmd.cs` 调 `cmd.exe` 承载 `flatc`。

---

## 6. `TableManager` 注册

1. 生成 **`ProtoTable.{你的表名}`** 后，在 **`mTypeList`** 增加 `typeof(你的表名)`。
2. 启动时调用 **`TableManager.Instance.Init()`**。
3. 读取：**`GetTableItem<T>(id)`** / **`GetTable<T>()`**。
4. 筛选、随机权重等在业务层处理；表内无随机逻辑。

---

## 7. 操作清单（策划/程序）

1. **定目录**：把 **`../client/Share/table/xls/`**（或你们统一后的路径）配置好，并保证仅处理需要的 **`c.xls`/`cs.xls`** 命名规则，或放宽/修改 `FindFile` 过滤。
2. **新建/维护 xls**，首行规约正确，`ID` 首条非空。
3. 执行 **`[TM工具集]/xls转cs`**，确认 **无 flatc 错误**，生成 **`.cs` + `.bytes`**。
4. **将 `.bytes` 放到 `Resources` 约定路径**（或修改 `fb.DumpData` 直接写入 `Assets/Resources/Data/table_fb/`）。
5. 业务代码通过 **`TableManager`** 取数。

---

## 8. 可精简 / 遗留文件（仅建议，改前请全局搜索引用）

| 对象 | 说明 |
|------|------|
| **`Assets/Editor/XlsToFb/pb.cs`** | 全文注释的旧 **Protobuf** 管线，**不参与编译逻辑**，可删除减小噪音（或移出工程归档）。 |
| **`Assets/Editor/XlsToFb/msvcp140d.dll`** | 疑似 MSVC **调试** 运行时；若 **`flatc.exe`** 独立可运行，可尝试移除并本地验证。 |
| **`Assets/Editor/XlsToFb/Library/UnityAssemblies/*.rsp`** | 历史 `smcs`/`gmcs` 警告屏蔽，若 Unity 版本不再需要可删。 |
| **`XlsxDataManager.cs` 内 `XlsxDataManager` 类** | 工程内 **无任何 `Instance()` 调用**，属死代码；精简需确认无人动态反射调用。 |
| **`UseSplitTable()`** | 恒为 **`true`**，但 **`splitXls` 为空** 时合并/忽略逻辑接近空操作；可评估改为 **`false`** 并删掉对 **`XlsxDataUnit`** 的依赖（**工程量较大**，需改 `Convert` / `FindFile` / `MergeTableArray`）。 |
| **`CExtendButton` + `ButtonBaseInspector`** | 仅 `CustomEditor` 扩展；若不打自定义 UI 可合并或迁出 `Xls2FBWindow.cs`。 |
| **`Assets/Editor/FlatBuffer/compile.bat`** | 手工调用 flatc 用，与 Unity 内流程重复时可文档化一笔即可。 |
| **`FlatBuffers.Struct`（`Struct.cs`）** | 为小部分 flatc 生成形态预留；若生成代码未使用 **struct** 表，可保留（体积小）。 |

**不建议删除（核心依赖链）**：`fb.cs`、`cmd.cs`、`Table.cs`（`xls` 命名空间表解析）、`Xls2FBWindow.cs`、`FlatBufferBuilder.cs`、`Offset.cs`、`MiniJSON.cs`（窗口映射表 JSON）、`Assets/Editor/FlatBuffer/*`、`Assets/Editor/TableScript/FlatBufferUtility/{Union,FlatBufferArray}.cs`、`NPOI` 各 DLL。

---

## 9. 与旧「CSV 词库」关系

- 若以本管线为唯一数据源：可移除仅服务 CSV 的 `LexiconDatabase`、`LexiconCsvImporter` 等（避免双维护）。
- 过渡期：CSV 草稿 → 定稿进 `.xls` → 菜单导表。

---

## 10. 校验清单

- [ ] Sheet / 表名与 `mTypeList` 中 **C# 类型名**一致  
- [ ] 首列 `ID`，首条数据有效  
- [ ] 枚举说明格式正确  
- [ ] **`.bytes` 在 `Resources` 路径与 `TableManager.kTablePath` 一致**  
- [ ] 生成 C# 在 **运行时程序集**，能引用 **`ProtoTable`** 与 **`FlatBuffers`** 类型  

---

*文档版本：与当前仓库 `Xls2FBWindow.cs`、`Xls2FB.cs`、`fb.cs`、`TableManager.cs`、`Table.cs`、`ByteBuffer.cs` 行为对齐；若修改输出/输入路径请以代码为准并更新本节。*
