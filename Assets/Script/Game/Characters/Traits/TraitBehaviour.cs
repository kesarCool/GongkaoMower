using UnityEngine;

/// <summary>
/// Rare 阶位解锁的英雄特质基类。挂在 Player GameObject 上，
/// 由 CharacterConfigApplier 根据 HeroUpgradeData.rareTrait 创建对应子类。
/// </summary>
[DisallowMultipleComponent]
public abstract class TraitBehaviour : MonoBehaviour
{
    /// <summary>由 CharacterConfigApplier 设置参数后调用。</summary>
    public virtual void Initialize(float[] parameters) { }

    /// <summary>获取 PlayMode 下激活的特质实例。</summary>
    public static T Find<T>() where T : TraitBehaviour
    {
        return FindObjectOfType<T>();
    }
}
