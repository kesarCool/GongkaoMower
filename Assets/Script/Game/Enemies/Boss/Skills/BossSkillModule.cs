using UnityEngine;

/// <summary>
/// Boss 技能模块基类（非 MonoBehaviour）。
/// BossBrain 在每个技能初始化时 new 一个实例，参数从 Excel elementNum[] 来。
/// </summary>
public abstract class BossSkillModule
{
    public float interval;       // 冷却时间（秒）
    public float cooldown;       // 当前剩余冷却
    public bool requiresTarget;  // Execute 前是否需要目标存在

    protected Transform boss;
    protected BossBrain brain;

    /// <summary>elementNum[i] 的逗号分隔参数 → 技能专属解析。</summary>
    public virtual void Init(string rawParams, BossBrain owner)
    {
        brain = owner;
        boss = owner.transform;
        // 子类重写，按固定顺序解析 rawParams
    }

    /// <summary>能否触发（冷却到 + 可选条件）。</summary>
    public virtual bool CanTrigger()
    {
        return cooldown <= 0f;
    }

    /// <summary>执行技能，子类实现。</summary>
    public abstract void Execute();

    /// <summary>BossBrain.Update 中调用，倒计时冷却。</summary>
    public virtual void Tick(float dt)
    {
        if (cooldown > 0f)
            cooldown -= dt;
    }

    /// <summary>Execute 后重置冷却。</summary>
    protected void ResetCooldown()
    {
        cooldown = interval;
    }

    /// <summary>把逗号分隔字符串解析成浮点数组。</summary>
    protected static float[] ParseFloats(string raw, int expectedCount)
    {
        float[] defaults = new float[expectedCount];
        if (string.IsNullOrWhiteSpace(raw))
            return defaults;

        string[] parts = raw.Split(',');
        for (int i = 0; i < Mathf.Min(parts.Length, expectedCount); i++)
        {
            if (float.TryParse(parts[i].Trim(), out float v))
                defaults[i] = v;
        }
        return defaults;
    }
}
