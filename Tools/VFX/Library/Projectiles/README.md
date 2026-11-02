# 投射物表现配置

保存飞行主体、短拖尾与命中引用的 Editor 制作参数；运行时不读取这些 JSON。一次性粒子导入器只处理 `../Recipes/`，不能导入 `mode=projectile-style` 的文件。

当前配置：[刀光剑影 V1](sword_presentation_v1.json)，普通青锋 / 满级金青双刃。使用菜单「工具/VFX/Build Sword Presentation V1」重建，「工具/VFX/检查刀光剑影配置」检查。构建器更新三个原投射物 Prefab 的表现并保留原 GUID，绑定独立的命中反馈；不修改技能定义和伤害入口。

命中也登记为 [sword_hit_v1](../Recipes/sword_hit_v1.json)，可在工作台独立预览和导出变体。主配置与命中配方共享图集、材质、命中时长与回池参数；修改后须同步重建并重新验收，不能只改 JSON 保留旧报告。

`integrated-editor-verified` 仅对应报告中的 Editor 实战结果。制作记录、参数、预算和证据见 [刀光剑影 V1](../../sword-presentation-v1.md)。
