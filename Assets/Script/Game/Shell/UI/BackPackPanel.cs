using System.Collections.Generic;
using ProtoTable;
using UnityEngine;
using UnityEngine.UI;

public struct BackPackItemInfo
{
    public int itemId;
    public string itemName;
    public string iconPath;
    public int count;
    public int grade;
    public string description;
}

[DisallowMultipleComponent]
public class BackPackPanel : UIPanelBase, LoopScrollPrefabSource, LoopScrollDataSource
{
    [Header("列表")]
    [SerializeField] private LoopVerticalScrollRect loopScroll;
    [SerializeField] private GameObject itemCellPrefab;

    [Header("空状态")]
    [SerializeField] private GameObject emptyHint;

    [Header("按钮")]
    [SerializeField] private Button closeButton;

    private RectTransform _cellPoolRoot;
    private readonly Stack<Transform> _pool = new Stack<Transform>();
    private readonly List<BackPackItemInfo> _items = new List<BackPackItemInfo>();

    private void Awake() => EnsureCellPoolRoot();

    public override void OnOpen(object payload)
    {
        BattleChineseFontRuntime.ApplyToHierarchy(transform);
        if (closeButton != null) closeButton.onClick.AddListener(OnCloseClicked);
        BuildItemList();
    }

    public override void OnClose()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(OnCloseClicked);
    }

    private void OnCloseClicked()
    {
        UiClickSound.PlayClose();
        UIManager.Instance.CloseTop();
    }

    // ── 数据构建 ──

    private void BuildItemList()
    {
        _items.Clear();
        TableManager.Instance?.EnsureLoaded();

        var data = PlayerProfileService.Instance.Data;
        if (data?.itemIds == null || data.itemIds.Length == 0) { RefreshUI(); return; }

        var itemTable = TableManager.Instance?.GetTable<ItemTable>();

        for (int i = 0; i < data.itemIds.Length; i++)
        {
            int id = data.itemIds[i];
            int count = i < data.itemCounts.Length ? data.itemCounts[i] : 0;
            if (count <= 0) continue;

            ItemTable itemRow = null;
            if (itemTable != null && itemTable.TryGetValue(id, out var obj))
                itemRow = obj as ItemTable;

            _items.Add(new BackPackItemInfo
            {
                itemId = id,
                itemName = itemRow?.ItemName ?? $"物品{id}",
                iconPath = itemRow?.IconPath ?? "",
                count = count,
                grade = itemRow?.Grade ?? 0,
                description = itemRow?.Description ?? "",
            });
        }

        RefreshUI();
    }

    private void RefreshUI()
    {
        if (loopScroll == null) loopScroll = GetComponent<LoopVerticalScrollRect>();
        if (loopScroll == null) return;

        loopScroll.prefabSource = this;
        loopScroll.dataSource = this;
        loopScroll.StopMovement();
        if (Application.isPlaying) loopScroll.ClearCells();

        loopScroll.totalCount = _items.Count;
        loopScroll.RefillCells();
        loopScroll.verticalNormalizedPosition = 1f;

        if (emptyHint != null) emptyHint.SetActive(_items.Count == 0);
    }

    // ── LoopScrollPrefabSource ──

    private RectTransform EnsureCellPoolRoot()
    {
        if (_cellPoolRoot != null) return _cellPoolRoot;
        var go = new GameObject("BackPackCellPool", typeof(RectTransform));
        _cellPoolRoot = go.GetComponent<RectTransform>();
        _cellPoolRoot.SetParent(transform, false);
        _cellPoolRoot.anchorMin = Vector2.zero;
        _cellPoolRoot.anchorMax = Vector2.zero;
        _cellPoolRoot.pivot = Vector2.zero;
        _cellPoolRoot.anchoredPosition = Vector2.zero;
        _cellPoolRoot.sizeDelta = Vector2.zero;
        return _cellPoolRoot;
    }

    public GameObject GetObject(int index)
    {
        if (index < 0 || index >= _items.Count || itemCellPrefab == null) return null;
        if (_pool.Count > 0) { var t = _pool.Pop(); t.gameObject.SetActive(true); return t.gameObject; }
        var go = Instantiate(itemCellPrefab, EnsureCellPoolRoot(), false);
        go.SetActive(false);
        return go;
    }

    public void ReturnObject(Transform trans)
    {
        if (trans == null) return;
        trans.gameObject.SetActive(false);
        trans.SetParent(EnsureCellPoolRoot(), false);
        _pool.Push(trans);
    }

    // ── LoopScrollDataSource ──

    public void ProvideData(Transform transform, int idx)
    {
        if (idx < 0 || idx >= _items.Count) return;
        var info = _items[idx];
        var cell = transform.GetComponent<ItemCell>();
        if (cell == null) return;

        Sprite icon = null;
        if (!string.IsNullOrEmpty(info.iconPath))
            icon = Resources.Load<Sprite>(info.iconPath);
        cell.Bind(icon, info.itemName, info.count, info.grade, info.description);
    }
}
