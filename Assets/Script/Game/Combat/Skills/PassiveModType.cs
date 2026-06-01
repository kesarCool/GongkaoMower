/// <summary>
/// 被动技能的加成计算方式。
/// </summary>
public enum PassiveModType
{
    /// <summary>加算：新值 = 原值 + bonus</summary>
    Additive,
    /// <summary>乘算：新值 = 原值 × (1 + bonus)</summary>
    Multiplicative,
    /// <summary>绝对值覆盖：新值 = bonus</summary>
    Absolute,
}
