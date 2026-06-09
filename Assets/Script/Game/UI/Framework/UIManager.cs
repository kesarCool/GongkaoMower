using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 全局 UI 弹窗：单主栈（模态）+ 顶层确认框（弱 B）。负责遮罩、排序、Escape/返回、唯一实例与暂停栈。
/// 使用：场景里放一个带 Canvas 的根物体，挂上本组件，配置 stackRoot / overlayRoot 与 Prefab 列表。
/// </summary>
[DisallowMultipleComponent]
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("层级")]
    [Tooltip("主栈父节点（建议在 Screen Space Overlay Canvas 下）")]
    [SerializeField] private RectTransform stackRoot;

    [Tooltip("确认框等与主栈独立的顶层父节点（sortingOrder 应高于主 Canvas，或用 sibling 置于更后）")]
    [SerializeField] private RectTransform overlayRoot;

    [Header("遮罩（可选）")]
    [Tooltip("主栈共用一块全屏暗色遮罩；无则仅依赖各 Panel 自带 backgroundBlocker")]
    [SerializeField] private Image stackBackdrop;

    [Header("注册（Prefab 根上挂具体 UIPanelBase 子类，勿用抽象基类本身）")]
    [SerializeField] private List<GameObject> panelPrefabs = new List<GameObject>();

    [Tooltip("弱 B 确认框 Prefab（挂 UiConfirmDialog）")]
    [SerializeField] private UiConfirmDialog confirmDialogPrefab;

    [Tooltip("轻提示 Toast Prefab（挂 UiToastPanel）；CodeDisplay=1 时需要")]
    [SerializeField] private UiToastPanel toastPanelPrefab;

    [Tooltip("Toast 默认显示秒数（ShowToast 未指定时长时）")]
    [SerializeField] private float defaultToastDuration = 1f;

    [Header("Tooltip")]
    [Tooltip("物品 Tooltip Prefab（挂 ItemTooltip），无需 Canvas")]
    [SerializeField] private ItemTooltip itemTooltipPrefab;

    [Header("排序（子物体带 Canvas 且 Override Sorting 时有效）")]
    [SerializeField] private int stackSortingBase = 200;

    [SerializeField] private int overlaySortingBase = 800;

    [Header("返回键")]
    [Tooltip("Standalone / Editor 下 Escape；Android 返回键需另行接 Input 或插件，可调用 CloseTop()/CloseConfirm()")]
    [SerializeField] private bool respondToEscape = true;

    private readonly Dictionary<Type, GameObject> _prefabByType = new Dictionary<Type, GameObject>();
    private readonly Dictionary<Type, UIPanelBase> _instanceByType = new Dictionary<Type, UIPanelBase>();

    /// <summary>主栈底 → 顶</summary>
    private readonly List<UIPanelBase> _stack = new List<UIPanelBase>(8);

    private UiConfirmDialog _confirmInstance;
    private UiToastPanel _toastInstance;
    private ItemTooltip _itemTooltipInstance;

    private int _pauseLocks;
    private float _storedTimeScale = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        foreach (var go in panelPrefabs)
        {
            if (go == null) continue;
            var panel = go.GetComponent<UIPanelBase>();
            if (panel == null)
            {
                Debug.LogWarning($"[UIManager] Prefab 无 UIPanelBase 组件，已跳过：{go.name}");
                continue;
            }
            var t = panel.GetType();
            if (_prefabByType.ContainsKey(t))
            {
                Debug.LogWarning($"[UIManager] 重复注册类型 {t.Name}，已跳过：{go.name}");
                continue;
            }
            _prefabByType[t] = go;
        }

        if (stackBackdrop != null)
            stackBackdrop.gameObject.SetActive(false);

        EnsureCanvasScalerForMobile();
        EnsureFullScreenRect(stackRoot);
        EnsureFullScreenRect(overlayRoot);
        if (stackBackdrop != null)
            EnsureFullScreenRect(stackBackdrop.rectTransform);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        while (_pauseLocks > 0)
            RemovePauseLock();
    }

    private void Update()
    {
        if (!respondToEscape) return;
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (_confirmInstance != null && _confirmInstance.gameObject.activeInHierarchy)
        {
            CloseConfirm();
            return;
        }

        var top = Top;
        if (top != null && top.LastOptions.CloseOnBack)
        {
            UiClickSound.PlayClose();
            CloseTop();
        }
    }

    /// <summary>当前主栈顶（无则 null）</summary>
    public UIPanelBase Top => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;

    /// <summary>获取已由本管理器创建过的面板实例（可能不在栈顶或已隐藏）。</summary>
    public bool TryGetInstance<T>(out T panel) where T : UIPanelBase
    {
        if (_instanceByType.TryGetValue(typeof(T), out var b) && b is T t)
        {
            panel = t;
            return true;
        }
        panel = null;
        return false;
    }

    /// <summary>打开模态面板（默认：暂停 + unscaled + 返回关闭）</summary>
    public T Open<T>(object payload = null) where T : UIPanelBase => Open<T>(payload, UiOpenOptions.ModalDefault);

    /// <summary>打开模态面板并传入框架选项</summary>
    public T Open<T>(object payload, UiOpenOptions options) where T : UIPanelBase
    {
        var type = typeof(T);
        if (type == typeof(UiConfirmDialog))
        {
            Debug.LogError("[UIManager] 请使用 ShowConfirm() 打开确认框，勿 Open<UiConfirmDialog>()。");
            return null;
        }

        CloseConfirmSilently();

        var panel = GetOrCreateInstance(type) as T;
        if (panel == null)
        {
            Debug.LogError($"[UIManager] 未注册或无法创建面板：{type.Name}");
            return null;
        }

        int idx = _stack.IndexOf(panel);
        if (idx >= 0)
        {
            while (_stack.Count - 1 > idx)
                PopTopInternal();
        }
        else
        {
            if (_stack.Count > 0)
                _stack[_stack.Count - 1].gameObject.SetActive(false);

            _stack.Add(panel);
            ParentTo(panel.transform, stackRoot);
            panel.gameObject.SetActive(true);
        }

        ApplyOpenOptions(panel, options);
        panel.OnOpen(payload);
        BattleChineseFontRuntime.EnsureLoaded();
        BattleChineseFontRuntime.ApplyToHierarchy(panel.transform);
        RefreshStackBackdrop();
        RefreshStackSorting();
        return panel;
    }

    /// <summary>关闭主栈顶一层</summary>
    public void CloseTop()
    {
        if (_stack.Count == 0) return;
        PopTopInternal();
        RefreshStackBackdrop();
        RefreshStackSorting();
    }

    /// <summary>关闭主栈全部</summary>
    public void CloseAllStack()
    {
        while (_stack.Count > 0)
            PopTopInternal();
        RefreshStackBackdrop();
    }

    /// <summary>单按钮告警（CodeDisplay=2，隐藏取消）。</summary>
    public void ShowAlert(string title, string message, Action onClosed = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            title = "提示";

        ShowConfirm(title, message, _ =>
        {
            if (_) onClosed?.Invoke();
        }, UiOpenOptions.ModalDefault, showCancel: false);
    }

    /// <summary>轻提示（CodeDisplay=1，不暂停）。</summary>
    public void ShowToast(string message, float durationSeconds = 0f)
    {
        if (string.IsNullOrEmpty(message))
            return;

        if (toastPanelPrefab == null)
        {
            Debug.LogWarning("[UIManager] toastPanelPrefab 未配置，Toast 降级为 Log：" + message);
            Debug.Log("[Toast] " + message);
            return;
        }

        if (_toastInstance == null)
        {
            _toastInstance = Instantiate(toastPanelPrefab, overlayRoot, false);
            _toastInstance.gameObject.SetActive(false);
            BattleChineseFontRuntime.EnsureLoaded();
            BattleChineseFontRuntime.ApplyToHierarchy(_toastInstance.transform);
        }

        ParentTo(_toastInstance.transform, overlayRoot);
        float duration = durationSeconds > 0f ? durationSeconds : defaultToastDuration;
        _toastInstance.Show(message, duration);
        BattleChineseFontRuntime.EnsureLoaded();
        BattleChineseFontRuntime.ApplyToHierarchy(_toastInstance.transform);
        RefreshOverlaySorting();
    }

    /// <summary>获取或创建 ItemTooltip 实例（挂 overlayRoot 下，共享 Canvas）。</summary>
    public ItemTooltip GetOrCreateItemTooltip()
    {
        if (_itemTooltipInstance != null)
            return _itemTooltipInstance;

        if (itemTooltipPrefab == null)
        {
            Debug.LogWarning("[UIManager] itemTooltipPrefab 未配置");
            return null;
        }

        _itemTooltipInstance = Instantiate(itemTooltipPrefab, overlayRoot, false);
        _itemTooltipInstance.gameObject.SetActive(false);
        BattleChineseFontRuntime.EnsureLoaded();
        BattleChineseFontRuntime.ApplyToHierarchy(_itemTooltipInstance.transform);
        return _itemTooltipInstance;
    }

    /// <summary>弱 B：在主栈之上显示确认框</summary>
    public void ShowConfirm(string title, string message, Action<bool> onResult, UiOpenOptions options = default)
    {
        ShowConfirm(title, message, onResult, options, showCancel: true);
    }

    /// <summary>弱 B：在主栈之上显示确认框（可隐藏取消按钮）。</summary>
    public void ShowConfirm(string title, string message, Action<bool> onResult, UiOpenOptions options, bool showCancel)
    {
        if (confirmDialogPrefab == null)
        {
            Debug.LogError("[UIManager] confirmDialogPrefab 未配置");
            onResult?.Invoke(false);
            return;
        }

        EnsureConfirmInstance();

        var o = options.PauseTime || options.UseUnscaledTime || options.CloseOnBack
            ? options
            : new UiOpenOptions { PauseTime = true, UseUnscaledTime = true, CloseOnBack = true };

        ApplyOpenOptions(_confirmInstance, o);
        ParentTo(_confirmInstance.transform, overlayRoot);
        _confirmInstance.Show(title, message, onResult, showCancel);
        BattleChineseFontRuntime.EnsureLoaded();
        BattleChineseFontRuntime.ApplyToHierarchy(_confirmInstance.transform);
        RefreshOverlaySorting();
    }

    private void EnsureConfirmInstance()
    {
        if (_confirmInstance != null)
            return;

        _confirmInstance = Instantiate(confirmDialogPrefab, overlayRoot, false);
        _confirmInstance.gameObject.SetActive(false);
        BattleChineseFontRuntime.EnsureLoaded();
        BattleChineseFontRuntime.ApplyToHierarchy(_confirmInstance.transform);
    }

    /// <summary>关闭确认框并触发 onResult(false)（与点「取消」一致）。</summary>
    public void CloseConfirm()
    {
        CloseConfirmInternal(invokeCancelCallback: true);
    }

    private void CloseConfirmSilently()
    {
        CloseConfirmInternal(invokeCancelCallback: false);
    }

    private void CloseConfirmInternal(bool invokeCancelCallback)
    {
        if (_confirmInstance == null || !_confirmInstance.gameObject.activeInHierarchy) return;

        ReleasePauseFor(_confirmInstance);
        if (invokeCancelCallback)
            _confirmInstance.InvokeCancelIfPending();
        _confirmInstance.OnClose();
        _confirmInstance.gameObject.SetActive(false);
    }

    private UIPanelBase GetOrCreateInstance(Type type)
    {
        if (_instanceByType.TryGetValue(type, out var exist) && exist != null)
            return exist;

        GameObject go;
        if (_prefabByType.TryGetValue(type, out var prefab) && prefab != null)
        {
            go = Instantiate(prefab, stackRoot, false);
        }
        else
        {
            // 纯代码生成面板（如 CharacterSelectionPanel），无需 Prefab
            go = new GameObject(type.Name, typeof(RectTransform));
        }

        go.SetActive(false);
        var panel = go.GetComponent(type) as UIPanelBase;
        if (panel == null)
            panel = go.AddComponent(type) as UIPanelBase;
        if (panel == null)
        {
            Destroy(go);
            return null;
        }
        panel.ResetForPoolOrReuse();
        _instanceByType[type] = panel;
        return panel;
    }

    private void PopTopInternal()
    {
        var top = _stack[_stack.Count - 1];
        ReleasePauseFor(top);
        top.OnClose();
        top.gameObject.SetActive(false);
        _stack.RemoveAt(_stack.Count - 1);

        if (_stack.Count > 0)
            _stack[_stack.Count - 1].gameObject.SetActive(true);
    }

    private static void ParentTo(Transform t, RectTransform parent)
    {
        if (t == null || parent == null) return;
        t.SetParent(parent, false);
        t.localScale = Vector3.one;
        if (t is RectTransform rt)
            EnsureFullScreenRect(rt);
    }

    /// <summary>竖屏手游常用：按宽度等比缩放，设计分辨率 1080×1920。</summary>
    private static void EnsureCanvasScalerForMobile()
    {
        var canvas = Instance != null ? Instance.GetComponentInParent<Canvas>() : null;
        if (canvas == null) return;

        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) return;

        if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
        }
    }

    private static void EnsureFullScreenRect(RectTransform rt)
    {
        if (rt == null) return;

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    private void ApplyOpenOptions(UIPanelBase panel, UiOpenOptions options)
    {
        panel.LastOptions = options;
        if (options.PauseTime)
        {
            if (!panel.AppliedPauseLock)
            {
                AddPauseLock();
                panel.AppliedPauseLock = true;
            }
        }
        else if (panel.AppliedPauseLock)
        {
            RemovePauseLock();
            panel.AppliedPauseLock = false;
        }
    }

    private void ReleasePauseFor(UIPanelBase panel)
    {
        if (panel != null && panel.AppliedPauseLock)
        {
            RemovePauseLock();
            panel.AppliedPauseLock = false;
        }
    }

    private void AddPauseLock()
    {
        if (_pauseLocks == 0)
            _storedTimeScale = Time.timeScale;
        _pauseLocks++;
        Time.timeScale = 0f;
    }

    private void RemovePauseLock()
    {
        _pauseLocks = Mathf.Max(0, _pauseLocks - 1);
        if (_pauseLocks == 0)
            Time.timeScale = _storedTimeScale;
    }

    private void RefreshStackBackdrop()
    {
        if (stackBackdrop == null) return;
        bool on = _stack.Count > 0;
        stackBackdrop.gameObject.SetActive(on);
        if (on)
        {
            stackBackdrop.rectTransform.SetAsFirstSibling();
            for (int i = 0; i < _stack.Count; i++)
                _stack[i].transform.SetAsLastSibling();
        }
    }

    private void RefreshStackSorting()
    {
        for (int i = 0; i < _stack.Count; i++)
        {
            var cv = _stack[i].GetComponent<Canvas>();
            if (cv != null && cv.overrideSorting)
                cv.sortingOrder = stackSortingBase + (i + 1) * 10;
        }
    }

    private void RefreshOverlaySorting()
    {
        if (_confirmInstance == null) return;
        var cv = _confirmInstance.GetComponent<Canvas>();
        if (cv != null && cv.overrideSorting)
            cv.sortingOrder = overlaySortingBase;
    }
}
