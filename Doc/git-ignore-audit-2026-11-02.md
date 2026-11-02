# Git 忽略建议清单（2026-11-02）

本次只做盘点和规则草案，未修改 `.gitignore`，未暂存、提交或删除现有文件。统计为本清单创建前的快照，文件数包含 `.meta`。

## 当前状态

- 已跟踪文件：226 个修改、188 个删除，共 414 个；暂存区为空。
- 未跟踪文件：21,133 个。
- 已跟踪且命中当前忽略规则的文件：0 个。
- 当前规则已经覆盖 Unity 的 Library、Temp、obj、Build、Builds、Logs、UserSettings，以及 IDE 工程文件。

| 未跟踪目录 | 文件数 | 判断 |
| --- | ---: | --- |
| outputs/ | 8,537 | 8,527 个 node_modules 文件，另 10 个岗位筛选表格、脚本和预览文件，与游戏工程分开存放更合适 |
| ArtOutput/ | 7,051 | 混合了截图、录屏、设计源稿、生成器输入、历史基线与报告，必须细分 |
| 美术资源/ | 3,580 | 包含源美术、交付、第三方资源、备份和推广材料，不宜整体忽略 |
| Assets/ | 1,379 | 新增工程资产和脚本，默认保留提交，检查资产与 .meta 配对 |
| output/ | 314 | 角色预览帧、迁移备份和验证输出，建议作为本地产物排除 |
| Tools/ | 198 | 工具代码和配方，默认保留提交 |

## 可以直接补充的规则

以下项目属于依赖或本机配置、日志。当前直接命中 8,531 个未跟踪文件：8,527 个依赖文件、2 个日志、2 个本机配置。

```gitignore
# Downloaded dependencies / Python caches
node_modules/
__pycache__/
*.py[cod]
.venv/

# Machine-specific agent settings; keep shared instructions and commands
/.claude/settings.local.json
/.codex/config.toml

# Root-level runtime / crash logs
/*.log
```

保留 `.claude/CLAUDE.md` 和 `.claude/commands/`。不要用 `/.claude/` 整体屏蔽共享说明和命令。本次未读取本机配置的内容。

## 建议排除的过程产物

这些规则会使对应文件不进入版本库，但不会删除磁盘上的文件。备份若承担恢复用途，仍需另行保存；Git 克隆无法恢复被忽略的文件。

```gitignore
# Local previews and migration backups (314 files currently)
/output/

# Unrelated spreadsheet task and its dependencies (8,537 files currently)
/outputs/

# Recorded validation frames, videos and recording metadata
/ArtOutput/UI/**/frames/
/ArtOutput/UI/**/*.mp4
/ArtOutput/UI/**/*.webm
/ArtOutput/UI/**/*.mov
/ArtOutput/UI/**/capture.concat
/ArtOutput/UI/**/frame-times.json

# Archived copies of retired map assets (85 files currently)
/ArtOutput/MapCleanup/ArchivedLegacyMaps-*/
```

`ArtOutput/UI` 内当前有 3,571 个帧文件、83 个视频、105 个上述录制元数据文件。这组规则不屏蔽整个 UI 目录，保留设计说明、布局配置、SVG 源稿与精选截图。部分 proof 工具需要录制帧和 frame-times.json；忽略后其他机器运行这类工具前需要重新录制。

`outputs/` 当前包含岗位筛选工作簿与 build_jobs.mjs。建议移出游戏工程或本地忽略；这些表格和脚本仍有独立使用价值，不应当作垃圾删除。node_modules 已在上一组覆盖，数量不能重复相加。

## 需要逐项保留或另行归档

| 路径 | 建议与依据 |
| --- | --- |
| ArtOutput/VFX/ | 不整体忽略。BossBladeBurstV1Builder 等读取 Sources/；ExplosionLibraryV2Builder 也读取 Sources/。部分审计工具读取 Baseline/、before-integrity.json，需保留所需输入 |
| ArtOutput/World/ | 不整体忽略。prepare-autumn-academy.cjs、prepare-autumn-terrain.cjs 会读取 source/；迁移报告也被项目文档引用 |
| ArtOutput/UI/ | 不整体忽略。存在 42 个 SVG，以及 design-data.json、layout-spec.json、design-notes.md 等设计成果 |
| 美术资源/地图设计/ | 保留生成器输入。CartoonBattleMapGenerator、ChapterTwoMapGenerator 直接读取其中资源 |
| 美术资源/备份/、游戏截图/、实机截图/ | 可作为本地归档排除；先区分唯一源稿与已有正式资产的副本 |
| 美术资源/UI交付/、UI设计/、推广/、品牌视觉参考/ | 属于交付或创作成果，建议精选入库或建立独立美术库，不能按缓存处理 |
| 美术资源/kenney_ui-pack/ | 第三方原始资源包；可以只保留项目使用部分及许可信息，但需先核对加工工具与来源需求 |
| Assets/ArtPreview/ | 不因名字包含 Preview 就忽略。它处于 Unity 资产目录中，需先检查场景、Prefab 和工具引用 |
| sites/word-mower-ui/ | 已确认是独立 Git 仓库。若仅用于本地网站预览，可在父仓库忽略此精确路径；若属于交付，明确采用独立仓库或 submodule，避免随 git add . 意外作为嵌套仓库加入 |
| TextToolDatas/slim/backup.txt | 约 26 MiB，建议核实是否唯一原始数据后再排除；不能仅凭 backup 名称判断 |
| 软著材料/ | 脚本、原始 Markdown 可保留；生成 PDF/DOCX/HTML 可以选择归档或仅保留发布版 |
| docs/resume_preview.html、Tools/rebuild_resume.py、docs/ 中个人面试材料 | 与游戏提交分开处理；现有仓库也已跟踪部分面试文档，不应顺手全局忽略 docs/ |

## 应保留进入工程更新的内容

- Assets 下正式脚本、场景、Prefab、材质、纹理、ScriptableObject，以及相应 .meta。
- Packages/manifest.json、Packages/packages-lock.json、ProjectSettings 下共享项目配置，包括新增的 BurstAotSettings 配置。
- Share/table/xls 原始配置表，以及工程当前使用的 table_fb/*.bytes 和 ProtoTable 代码。生成文件如果承担运行时输入，仍需保留；目前没有验证出完整的自动重建流程。
- Tools 下项目工具和 JSON 配方、Doc/docs 下项目设计与维护文档。
- WX-WASM-SDK-V2 的桥接修复与配置改动需要单独审查；它们不是可一律忽略的缓存。
- 188 个已跟踪删除项需要作为删除或迁移纳入检查，不能靠新增忽略规则隐藏。部分旧地图在归档目录中有副本，提交时仍需核对新地图、GUID 和资源引用。

## 当前规则中值得收窄的地方

根目录 `/*.png`、`/*.zip`、`/*.unitypackage` 和全局 `*_report.txt` 比较宽，可能隐藏未来正式交付文件。建议后续改为明确的输出目录或已知文件名；本次未调整，也未将已经被这些规则忽略的文件纳入上述未跟踪统计。

## 后续提交顺序建议

1. 落地依赖、日志、本机配置与确认后的过程产物规则。
2. 将源美术与生成器所需输入保留下来，复核旧资源删除和新资源引用。
3. 按忽略规则、玩法代码、UI/特效/地图资产、SDK 与构建配置、文档分批提交；强耦合的代码与资产保持同一提交。

本次未运行 Unity 或验证游戏功能，结论来自 Git 清单、目录内容以及工具输入路径检查。
