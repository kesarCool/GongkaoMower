# 弹仔五向整身瞄准 V1

> 当前状态：已按用户要求恢复原版单贴图模式。弹仔身体与头像改回 player01，aimFrames 已清空；角色目录不再引用五向实验图集。下文保留实验制作与技术验证记录，非当前启用配置。[恢复检查](../../ArtOutput/VFX/PlayerGunnerAimV1/Audit/single-sprite-restore-check.json)。

> 观感验收：未通过。用户反馈与单贴图区别不大、切换不够顺畅，并关注增加帧数带来的资源成本。50 项技术检查仅代表功能接入通过；用户已要求转到“了解 GPT 多模态与游戏素材生成”分析 PerfectPixel 与 GPT Image 的接入可行性，暂不继续增加本版贴图。

2026-10-31。首个单角色样本：无字红额带、短管霰弹枪、五张整身持枪姿态，通过镜像表达八向。只接入默认弹仔（character_01），没有批量修改其他角色。当前是方向关键帧验证，尚未制作迈步循环。

## 画面与范围

- [五张整身姿态](../../ArtOutput/VFX/PlayerGunnerAimV1/five-poses.png)：右、右上、正上、右下、正下；头部保持正面和直立。左右镜像后，额带没有反字问题。
- [Unity 八向检查图](../../ArtOutput/VFX/PlayerGunnerAimV1/unity-eight-directions.png)与[Unity 原速预览](../../ArtOutput/VFX/PlayerGunnerAimV1/unity-aim-validation.mp4)：真实 Game 场景和 Player Prefab，受控目标；视频放大镜头便于检查，不是真机性能测试。
- [原图评估](player-frame-reference-review-v1.md)保留来源与取舍；使用内置 imagegen 生成新整身图，[完整提示词](../../ArtOutput/VFX/PlayerGunnerAimV1/prompts.md)及两次源图保存在工程外层 ArtOutput，不作为运行时纹理导入。
- 这轮只换视觉。保留原手枪技能的伤害、弹数、间隔、弹体资源和玩家中心发射起点；短管霰弹枪是持枪造型，不额外改成新的伤害规则。

## 运行接入

[CharacterAimFrames](../../Assets/Script/Game/Characters/CharacterAimFrames.cs)保存五张整身姿态。[CharacterDefinition](../../Assets/Script/Game/Characters/CharacterDefinition.cs)增加可选 aimFrames；[弹仔配置](../../Assets/ScriptableObject/Character/Role/Character_PistolGuy.asset)引用新图集，头像和默认身体图同步使用新右向姿态。其他四个角色的资产不写入此字段。

[PlayerController](../../Assets/Script/Game/Player/Components/PlayerController.cs)把已有最近目标方向传给 [CharacterAppearance](../../Assets/Script/Game/Player/Components/CharacterAppearance.cs)；无目标时使用真实位移，静止保留最后方向，传送重置不制造一次假转身。方向边界有 6° 缓冲，减少来回跳图。身体保留原有 SpriteRenderer、材质、受击与呼吸组件，不拆分四肢或增加第二把枪。

[SkillAutoProjectile](../../Assets/Script/Game/Combat/Skills/Implementations/SkillAutoProjectile.cs)在普通投射物成功生成后额外发布 PlayerWeaponFiredVisualEvent，携带发射者、技能、实际瞄准方向及成功数量。整身姿态只响应本角色主武器，发射当帧优先切到对应方向；其他角色、其他技能和零发射不会争抢姿态。原 SkillCastEvent 及爆炸事件语义不变。

## 资源

| 项目 | 本轮结果 |
| --- | --- |
| 运行时新增 PNG | 一张 512×256，28,929 字节（约 28.3 KiB） |
| 存储姿态 | 5 张，每张画布 128×128；8 向不复制镜像纹理 |
| 人物高度 / 脚底 | 人体约 106 px，统一脚底基线；中心 Pivot，72 PPU |
| 导入 | Point、Clamp、无 mipmap、关闭 Read/Write、Multiple Sprite |
| 当前 Editor 导入 | DXT5；Profiler 记录 262,760 字节，约 256.6 KiB；包含当前 Editor 对象开销，不能当作微信真机结果 |
| 理论纹理块 | 若最终使用 ETC2 RGBA8，无 mipmap 的纹理块约 128 KiB；尚未实测微信包体 / 设备内存 |

## 重建与验收

1. `node Tools/VFX/pack-player-gunner-aim-v1.cjs`：从保留的透明源图裁格、统一身高 / 脚底、生成小图集和选帧板。
2. Unity 菜单 `工具/角色美术/构建弹仔五向整身姿态`：设置导入、配置五帧资产及单角色引用。
3. Unity 菜单 `工具/角色美术/验证弹仔八向瞄准`：使用隔离内存存档加载真实 Game 场景。结束用 `工具/Home UI/Finish Preview` 恢复原已保存场景。
4. `node Tools/VFX/render-player-gunner-aim-proof.cjs`：按采集时间戳合成原速视频和八向局部检查图。
5. `node Tools/VFX/audit-player-gunner-aim-v1.cjs`：核对源图、配置、其他角色、技能数值、移动 / 碰撞逻辑和本轮 Unity 报告。

报告：[资源配置](../../ArtOutput/VFX/PlayerGunnerAimV1/Audit/config-check.json)、[真实场景运行检查](../../ArtOutput/UI/PlayerGunnerAimV1/After/validation-report.json)、[源码 / 资源完整性](../../ArtOutput/VFX/PlayerGunnerAimV1/Audit/integrity-check.json)。运行检查暂时关闭已有呼吸组件以核对受击复位和 Transform 不变量，录像恢复原呼吸；血条的两个 SpriteRenderer 单独保留。首轮报告中的计数 / 呼吸检查假设错误另留 first-run-report.json，最终结果以 After 报告为准。

后续先验收人物整体与短枪，再按每个基础方向 4～6 帧补移动循环。本次不把五张站姿记作已完成跑步动画。
