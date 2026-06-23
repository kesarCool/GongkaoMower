using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Home 底部页签栏：管理 viewContainer 内子面板的创建与切换。
/// 支持三种挂载类型：
///   1. SceneView — 已在场景中的 GameObject（如 HomeRoadmapView），不需要 Prefab
///   2. UIPanelPrefab — UIPanelBase 子类 Prefab（如 CharacterSelectionPanel），走适配器生命周期
///   3. TabViewPrefab — HomeTabViewBase 子类 Prefab（如将来 ShopView），走标准接口
/// </summary>
[DisallowMultipleComponent]
public class HomeTabBar : MonoBehaviour
{
    [Header("容器")]
    [Tooltip("TopBar 和 BottomBar 之间的 RectTransform，所有页签视图挂载在此。")]
    [SerializeField] private RectTransform viewContainer;

    [Header("页签配置")]
    [SerializeField] private List<TabEntry> tabs = new List<TabEntry>();

    [Header("默认页签")]
    [Tooltip("启动时默认激活的页签 ID（如 \"battle\"）。")]
    [SerializeField] private string defaultTabId = "battle";

    private string _activeTabId;
    private readonly Dictionary<string, TabRuntime> _runtime = new Dictionary<string, TabRuntime>();

    /// <summary>当前活跃页签 ID。</summary>
    public string ActiveTabId => _activeTabId;

    /// <summary>外部数据刷新——转发给当前活跃视图。</summary>
    public void RefreshActive()
    {
        if (_activeTabId == null) return;
        if (!_runtime.TryGetValue(_activeTabId, out var rt)) return;

        if (rt.homeTabView != null)
            rt.homeTabView.OnTabRefresh();
        else if (rt.uiPanel != null)
            rt.uiPanel.OnOpen(null); // 重新触发 OnOpen 做刷新
    }

    /// <summary>按 ID 切换到指定页签。</summary>
    public void SwitchTo(string tabId)
    {
        Debug.Log($"[HomeTabBar] SwitchTo(\"{tabId}\"), 当前=\"{_activeTabId}\"");

        if (_activeTabId == tabId)
        {
            Debug.Log($"[HomeTabBar] \"{tabId}\" 已是当前页签，跳过");
            return;
        }

        // TabEntry 是 struct，Find 返回 default 时 tabId 为 null
        int idx = tabs.FindIndex(t => t.tabId == tabId);
        if (idx < 0)
        {
            Debug.LogError($"[HomeTabBar] 未找到页签 ID: \"{tabId}\"，已配置: [{string.Join(", ", tabs.ConvertAll(t => $"\"{t.tabId}\""))}]");
            return;
        }
        var entry = tabs[idx];

        // 隐藏旧页签
        if (_activeTabId != null && _runtime.TryGetValue(_activeTabId, out var oldRt))
        {
            if (oldRt.homeTabView != null)
                oldRt.homeTabView.OnTabLeave();
            else if (oldRt.uiPanel != null)
                oldRt.uiPanel.gameObject.SetActive(false);
            else if (oldRt.sceneView != null)
                oldRt.sceneView.SetActive(false);
        }

        // 懒加载新页签
        if (!_runtime.TryGetValue(tabId, out var newRt))
        {
            newRt = CreateTab(entry);
            _runtime[tabId] = newRt;
        }

        // 激活新页签
        if (newRt.homeTabView != null)
        {
            newRt.homeTabView.OnTabEnter();
        }
        else if (newRt.uiPanel != null)
        {
            newRt.uiPanel.gameObject.SetActive(true);
        }
        else if (newRt.sceneView != null)
        {
            newRt.sceneView.SetActive(true);
            // SetActive(true) 触发 HomeRoadmapView.OnEnable → RefreshAll()，无需额外调用
        }

        _activeTabId = tabId;

        // 按钮高亮
        RefreshButtonStates();
    }

    private TabRuntime CreateTab(TabEntry entry)
    {
        string pn = entry.prefab ? entry.prefab.name : "null";
        string svn = entry.sceneView ? entry.sceneView.name : "null";
        Debug.Log($"[HomeTabBar] CreateTab: tabId=\"{entry.tabId}\", mode={entry.mode}, prefab={pn}, sceneView={svn}");
        if (viewContainer == null) { Debug.LogError("[HomeTabBar] viewContainer 未绑定！"); return new TabRuntime(); }
        var rt = new TabRuntime();

        if (entry.mode == TabMountMode.SceneView && entry.sceneView)
        {
            entry.sceneView.transform.SetParent(viewContainer, false);
            var sr = entry.sceneView.GetComponent<RectTransform>();
            if (sr) StretchToFill(sr);
            entry.sceneView.SetActive(false);
            rt.sceneView = entry.sceneView;
            Debug.Log($"[HomeTabBar] SceneView \"{entry.sceneView.name}\" 已移入 viewContainer");
        }
        else if (entry.mode == TabMountMode.UIPanelPrefab && entry.prefab)
        {
            var go = Instantiate(entry.prefab, viewContainer, false);
            var rt2 = go.GetComponent<RectTransform>();
            if (rt2) StretchToFill(rt2);
            go.SetActive(false);
            rt.uiPanel = go.GetComponent<UIPanelBase>();
            if (rt.uiPanel)
            {
                Debug.Log($"[HomeTabBar] UIPanel \"{entry.prefab.name}\" Instantiate 成功, 调 OnOpen(null)");
                rt.uiPanel.OnOpen(null);
            }
            else
            {
                Debug.LogWarning($"[HomeTabBar] Prefab {entry.prefab.name} 上未找到 UIPanelBase 组件");
            }
        }
        else if (entry.mode == TabMountMode.TabViewPrefab && entry.prefab)
        {
            var go = Instantiate(entry.prefab, viewContainer, false);
            var rt2 = go.GetComponent<RectTransform>();
            if (rt2) StretchToFill(rt2);
            go.SetActive(false);
            rt.homeTabView = go.GetComponent<HomeTabViewBase>();
            if (rt.homeTabView)
            {
                Debug.Log($"[HomeTabBar] TabView \"{entry.prefab.name}\" Instantiate 成功, 调 OnTabInit()");
                rt.homeTabView.OnTabInit();
            }
            else
            {
                Debug.LogWarning($"[HomeTabBar] Prefab {entry.prefab.name} 上未找到 HomeTabViewBase 组件");
            }
        }
        else
        {
            string pn2 = entry.prefab ? entry.prefab.name : "null";
            string svn2 = entry.sceneView ? entry.sceneView.name : "null";
            Debug.LogError($"[HomeTabBar] CreateTab 无法创建: mode={entry.mode}, prefab={pn2}, sceneView={svn2}");
        }

        return rt;
    }

    private void RefreshButtonStates()
    {
        foreach (var entry in tabs)
        {
            if (entry.button != null)
                entry.button.interactable = entry.tabId != _activeTabId;
        }
    }

    private void Start()
    {
        if (tabs.Count == 0)
        {
            Debug.LogWarning("[HomeTabBar] 未配置任何页签");
            return;
        }

        Debug.Log($"[HomeTabBar] Start: {tabs.Count} 个页签, viewContainer={(viewContainer != null ? viewContainer.name : "null")}");

        // 绑定按钮点击事件
        foreach (var entry in tabs)
        {
            if (entry.button == null)
            {
                Debug.LogWarning($"[HomeTabBar] 页签 \"{entry.tabId}\" 未绑定按钮！请在 Inspector 拖入 Button。");
                continue;
            }

            string capturedId = entry.tabId; // 闭包变量捕获
            entry.button.onClick.AddListener(() =>
            {
                Debug.Log($"[HomeTabBar] 按钮点击: tabId=\"{capturedId}\"");
                UiClickSound.Play();
                SwitchTo(capturedId);
            });
            Debug.Log($"[HomeTabBar] 已绑定按钮: tabId=\"{entry.tabId}\", button={entry.button.name}");
        }

        // 初始刷新红点角标（先强制重算确保数据源就绪）
        RedDotService.Instance.ForceRecompute();
        for (int i = 0; i < tabs.Count; i++)
        {
            if (tabs[i].badge != null)
                tabs[i].badge.Refresh();
        }

        // 默认页签
        string startTab = string.IsNullOrEmpty(defaultTabId) ? tabs[0].tabId : defaultTabId;
        SwitchTo(startTab);
    }

    private void OnDestroy()
    {
        // 清理 UIPanelBase 实例（调 OnClose）
        foreach (var kv in _runtime)
        {
            if (kv.Value.uiPanel != null)
            {
                kv.Value.uiPanel.OnClose();
            }
        }
        _runtime.Clear();
    }

    private static void StretchToFill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }

    // ═══════════════ 数据结构 ═══════════════

    public enum TabMountMode
    {
        /// <summary>已在场景中的 GameObject（不需要 Prefab）。</summary>
        SceneView,
        /// <summary>UIPanelBase 子类 Prefab，走 OnOpen/OnClose 适配器。</summary>
        UIPanelPrefab,
        /// <summary>HomeTabViewBase 子类 Prefab，走 OnTabEnter/Leave/Refresh 标准接口。</summary>
        TabViewPrefab,
    }

    [Serializable]
    public struct TabEntry
    {
        [Tooltip("唯一标识（如 battle / character / shop）。")]
        public string tabId;

        [Tooltip("BottomBar 对应按钮。")]
        public Button button;

        [Tooltip("挂载模式。")]
        public TabMountMode mode;

        [Tooltip("SceneView 模式：直接拖场景中的 GameObject。")]
        public GameObject sceneView;

        [Tooltip("Prefab 模式：拖 Prefab 资源（UIPanelBase 或 HomeTabViewBase 子类）。")]
        public GameObject prefab;

        [Tooltip("红点角标组件（挂载在按钮子物体上，可为空）。")]
        public RedDotBadge badge;
    }

    private sealed class TabRuntime
    {
        public GameObject sceneView;
        public UIPanelBase uiPanel;
        public HomeTabViewBase homeTabView;
    }
}
