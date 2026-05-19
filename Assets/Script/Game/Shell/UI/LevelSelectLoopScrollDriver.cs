using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 与 <see cref="LoopVerticalScrollRect"/> 同物体；双 Prefab（章节头 / 关卡行）池化，数据来自 <see cref="ChapterLevelListBuilder"/>。
/// 两种 Cell 根节点必须挂 <see cref="LayoutElement"/>，且 <b>Preferred Height 必须设为大于 0</b>（章节、关卡可不同高度）；
/// 未设置时 Loop 用包内尺寸工具算出的高度为 0，会导致填不满视口、滚动异常或看起来像不显示。
/// </summary>
[RequireComponent(typeof(LoopVerticalScrollRect))]
[DisallowMultipleComponent]
public class LevelSelectLoopScrollDriver : MonoBehaviour, LoopScrollPrefabSource, LoopScrollDataSource
{
    [SerializeField] private GameObject chapterHeaderPrefab;
    [SerializeField] private GameObject levelRowPrefab;

    private readonly Stack<Transform> _poolChapter = new Stack<Transform>();
    private readonly Stack<Transform> _poolLevel = new Stack<Transform>();

    private LoopVerticalScrollRect _loop;
    /// <summary>回收的 Cell 只能挂在这里，不能挂 <see cref="ScrollRect.content"/>：Loop 会把 content 子节点数当作 cell 数做 Refill/复用。</summary>
    private RectTransform _cellPoolRoot;
    private List<LevelSelectFlatRow> _rows = new List<LevelSelectFlatRow>();

    private void Awake()
    {
        _loop = GetComponent<LoopVerticalScrollRect>();
        EnsureCellPoolRoot();
    }

    private RectTransform EnsureCellPoolRoot()
    {
        if (_cellPoolRoot != null)
            return _cellPoolRoot;

        if (_loop == null)
            _loop = GetComponent<LoopVerticalScrollRect>();

        var parent = _loop != null ? _loop.transform : transform;
        var go = new GameObject("LevelSelectCellPool", typeof(RectTransform));
        _cellPoolRoot = go.GetComponent<RectTransform>();
        _cellPoolRoot.SetParent(parent, false);
        _cellPoolRoot.anchorMin = Vector2.zero;
        _cellPoolRoot.anchorMax = Vector2.zero;
        _cellPoolRoot.pivot = Vector2.zero;
        _cellPoolRoot.anchoredPosition = Vector2.zero;
        _cellPoolRoot.sizeDelta = Vector2.zero;
        return _cellPoolRoot;
    }

    /// <summary>在 <see cref="TableManager.Init"/> 之后调用；可由 <see cref="LevelSelectPanel.OnOpen"/> 触发。</summary>
    public void RefreshFromTable()
    {
        if (TableManager.Instance == null)
        {
            GameErrorPresenter.Show(GameErrorCodes.TableManagerMissing);
            _rows = new List<LevelSelectFlatRow>();
            ApplyToLoop();
            return;
        }

        TableManager.Instance.Init();
#if USE_FB_TABLE
        var dict = TableManager.Instance.GetTable<ProtoTable.ChapterLevel>();
#else
        var dict = new Dictionary<int, object>();
#endif
        _rows = ChapterLevelListBuilder.Build(dict);
        ApplyToLoop();
    }

    private void ApplyToLoop()
    {
        if (_loop == null)
            _loop = GetComponent<LoopVerticalScrollRect>();
        if (_loop == null)
            return;

        // 弹窗关闭再打开：Loop 不会在 OnDisable 里收齐 cell；先绑好 Source 再 ClearCells，把 Content 上残留全部退回池并归零内部索引。
        _loop.prefabSource = this;
        _loop.dataSource = this;
        _loop.StopMovement();
        if (Application.isPlaying)
            _loop.ClearCells();

        _loop.totalCount = _rows.Count;
        _loop.RefillCells();
        _loop.verticalNormalizedPosition = 1f;
    }

    public GameObject GetObject(int index)
    {
        if (index < 0 || index >= _rows.Count)
            return null;

        var kind = _rows[index].Kind;
        if (kind == LevelSelectRowKind.ChapterHeader)
            return SpawnOrPop(chapterHeaderPrefab, _poolChapter);
        return SpawnOrPop(levelRowPrefab, _poolLevel);
    }

    private GameObject SpawnOrPop(GameObject prefab, Stack<Transform> pool)
    {
        if (prefab == null)
        {
            Debug.LogError("[LevelSelectLoopScrollDriver] Cell Prefab 未配置。");
            return null;
        }

        if (pool.Count > 0)
        {
            var t = pool.Pop();
            t.gameObject.SetActive(true);
            return t.gameObject;
        }

        var go = Instantiate(prefab, EnsureCellPoolRoot(), false);
        go.SetActive(false);
        return go;
    }

    public void ReturnObject(Transform trans)
    {
        if (trans == null)
            return;

        trans.SendMessage("ScrollCellReturn", SendMessageOptions.DontRequireReceiver);
        trans.gameObject.SetActive(false);
        trans.SetParent(EnsureCellPoolRoot(), false);

        var header = trans.GetComponent<LevelSelectChapterHeaderCell>();
        if (header != null)
        {
            _poolChapter.Push(trans);
            return;
        }

        if (trans.GetComponent<LevelSelectLevelRowCell>() != null)
            _poolLevel.Push(trans);
    }

    public void ProvideData(Transform transform, int idx)
    {
        if (idx < 0 || idx >= _rows.Count)
            return;

        var row = _rows[idx];
        var header = transform.GetComponent<LevelSelectChapterHeaderCell>();
        if (header != null)
        {
            header.Bind(row);
            return;
        }

        transform.GetComponent<LevelSelectLevelRowCell>()?.Bind(row);
    }
}
