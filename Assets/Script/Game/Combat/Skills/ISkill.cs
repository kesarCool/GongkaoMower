/// <summary>
/// 技能接口：纯 C# 对象，由 PlayerSkills 驱动 Tick（不依赖 MonoBehaviour 生命周期）
/// </summary>
public interface ISkill
{
    SkillId Id { get; }
    int Level { get; }

    void OnEquip(SkillContext ctx);
    void OnUnequip();
    void OnLevelUp();

    /// <summary>每帧更新（deltaTime 为 scaled time）</summary>
    void Tick(float deltaTime);
}
