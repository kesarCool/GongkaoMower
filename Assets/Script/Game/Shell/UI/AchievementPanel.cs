using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 成就列表弹窗（Home 大厅打开，不暂停游戏）。
/// 使用 LoopScrollRect 虚拟滚动，所有阶段打平排序。
/// </summary>
[DisallowMultipleComponent]
public class AchievementPanel : UIPanelBase, LoopScrollPrefabSource, LoopScrollDataSource
{
    [Header("列表")]
    [SerializeField] private LoopVerticalScrollRect loopScroll;
    [SerializeField] private AchievementCell cellPrefab;

    [Header("空态")]
    [SerializeField] private GameObject emptyHint;

    [Header("按钮")]
    [SerializeField] private Button closeButton;

    /// <summary>扁平化条目：stage + 所属 group。</summary>
    private struct FlatItem
    {
        public AchievementService.StageInfo stage;
        public AchievementService.Group group;
    }

    private readonly List<FlatItem> _flat = new List<FlatItem>();
    private readonly Stack<Transform> _pool = new Stack<Transform>();
    private RectTransform _poolRoot;

    private void Awake()
    {
        _poolRoot = CreatePoolRoot();
    }

    public override void OnOpen(object payload)
    {
        BattleChineseFontRuntime.ApplyToHierarchy(transform);

        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseClicked);

        RebuildAndRefresh();
    }

    public override void OnClose()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(OnCloseClicked);
    }

    /// <summary>重建数据并刷新 LoopScrollRect。</summary>
    private void RebuildAndRefresh()
    {
        // 1. 打平所有阶段
        _flat.Clear();
        var groups = AchievementService.Instance.GetAchievementGroups();
        foreach (var group in groups)
        {
            foreach (var stage in group.stages)
            {
                _flat.Add(new FlatItem { stage = stage, group = group });
            }
        }

        // 2. 全局排序：可领取 → 进行中 → 已领取，同状态按 sortOrder
        _flat.Sort((a, b) =>
        {
            int GetPriority(FlatItem item)
            {
                if (item.stage.isClaimed) return 2;
                if (item.stage.isCompleted) return 0;
                return 1;
            }
            int pa = GetPriority(a);
            int pb = GetPriority(b);
            if (pa != pb) return pa.CompareTo(pb);
            return a.group.sortOrder.CompareTo(b.group.sortOrder);
        });

        // 3. 刷新 LoopScrollRect
        if (loopScroll == null) loopScroll = GetComponent<LoopVerticalScrollRect>();
        if (loopScroll == null) return;

        loopScroll.prefabSource = this;
        loopScroll.dataSource = this;
        loopScroll.StopMovement();
        if (Application.isPlaying) loopScroll.ClearCells();
        loopScroll.totalCount = _flat.Count;
        loopScroll.RefillCells();
        loopScroll.verticalNormalizedPosition = 0f;

        // 4. 空态
        if (emptyHint != null) emptyHint.SetActive(_flat.Count == 0);
    }

    private void OnCellClaimClicked(int groupId, int stage)
    {
        var result = AchievementService.Instance.Claim(groupId, stage);
        if (result.success)
        {
            AchievementService.Instance.MarkDirty();
            RebuildAndRefresh();
            BattleChineseFontRuntime.ApplyToHierarchy(transform);

            string msg = $"领取成功！获得钻石 ×{result.rewardCount}";
            if (UIManager.Instance != null)
                UIManager.Instance.ShowToast(msg, 2f);
        }
    }

    private void OnCloseClicked()
    {
        UiClickSound.PlayClose();
        UIManager.Instance.CloseTop();
    }

    // ── LoopScrollPrefabSource ──

    private RectTransform CreatePoolRoot()
    {
        var go = new GameObject("CellPool", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    public GameObject GetObject(int index)
    {
        if (index < 0 || index >= _flat.Count || cellPrefab == null) return null;
        if (_pool.Count > 0)
        {
            var t = _pool.Pop();
            t.gameObject.SetActive(true);
            return t.gameObject;
        }
        var cell = Instantiate(cellPrefab, _poolRoot, false);
        cell.gameObject.SetActive(false);
        return cell.gameObject;
    }

    public void ReturnObject(Transform trans)
    {
        if (trans == null) return;
        trans.gameObject.SetActive(false);
        trans.SetParent(_poolRoot, false);
        _pool.Push(trans);
    }

    // ── LoopScrollDataSource ──

    public void ProvideData(Transform transform, int idx)
    {
        if (idx < 0 || idx >= _flat.Count) return;
        var item = _flat[idx];
        var cell = transform.GetComponent<AchievementCell>();
        if (cell == null) return;
        cell.Bind(item.stage, item.group, OnCellClaimClicked);
    }
}
