using UnityEngine;

/// <summary>
/// Home 页签视图基类：定义 Tab 切换生命周期。
/// 新模块（商店等）直接继承此基类，由 <see cref="HomeTabBar"/> 管理。
/// <see cref="UIPanelBase"/> 子类（如 CharacterSelectionPanel）不继承此基类，
/// 由 HomeTabBar 用适配器方式直接调用 OnOpen / OnClose。
/// </summary>
[DisallowMultipleComponent]
public abstract class HomeTabViewBase : MonoBehaviour
{
    /// <summary>是否已完成首次初始化（懒加载判断用）。</summary>
    public bool IsInitialized { get; protected set; }

    /// <summary>切换到本页签时调用（首次进入会先调 OnTabInit）。</summary>
    public virtual void OnTabEnter()
    {
        gameObject.SetActive(true);
    }

    /// <summary>切走时调用（默认隐藏；子类可 override 保留 GameObject 状态）。</summary>
    public virtual void OnTabLeave()
    {
        gameObject.SetActive(false);
    }

    /// <summary>外部数据变更（金币/碎片等）时刷新，仅对当前活跃页签有效。</summary>
    public virtual void OnTabRefresh() { }

    /// <summary>首次 Instantiate 后的初始化（替代 Awake/Start 或 OnOpen 外的逻辑）。</summary>
    public virtual void OnTabInit()
    {
        IsInitialized = true;
    }
}
