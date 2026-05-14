using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 与 <see cref="LoopVerticalScrollRect"/> 同物体；单 Prefab（词条行）池化，数据为 <c>DisplayText</c> 列表。
/// Cell 根挂 <see cref="LayoutElement"/>，且 <b>Preferred Height 必须大于 0</b>，与 <see cref="LevelSelectLoopScrollDriver"/> 约定一致；
/// 池父节点勿挂在 <see cref="ScrollRect.content"/> 上。见 <c>docs/虚拟滚动列表选型.md</c>。
/// </summary>
[RequireComponent(typeof(LoopVerticalScrollRect))]
[DisallowMultipleComponent]
public class LexiconPreviewLoopScrollDriver : MonoBehaviour, LoopScrollPrefabSource, LoopScrollDataSource
{
    [SerializeField] private GameObject entryRowPrefab;

    private LoopVerticalScrollRect _loop;
    /// <summary>回收的 Cell 只能挂在这里，不能挂 <see cref="ScrollRect.content"/>。</summary>
    private RectTransform _cellPoolRoot;
    private readonly Stack<Transform> _pool = new Stack<Transform>();
    private readonly List<string> _lines = new List<string>();

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
        var go = new GameObject("LexiconPreviewCellPool", typeof(RectTransform));
        _cellPoolRoot = go.GetComponent<RectTransform>();
        _cellPoolRoot.SetParent(parent, false);
        _cellPoolRoot.anchorMin = Vector2.zero;
        _cellPoolRoot.anchorMax = Vector2.zero;
        _cellPoolRoot.pivot = Vector2.zero;
        _cellPoolRoot.anchoredPosition = Vector2.zero;
        _cellPoolRoot.sizeDelta = Vector2.zero;
        return _cellPoolRoot;
    }

    public void SetLines(IReadOnlyList<string> displayLines)
    {
        _lines.Clear();
        if (displayLines != null)
        {
            for (var i = 0; i < displayLines.Count; i++)
                _lines.Add(displayLines[i]);
        }

        ApplyToLoop();
    }

    private void ApplyToLoop()
    {
        if (_loop == null)
            _loop = GetComponent<LoopVerticalScrollRect>();
        if (_loop == null || entryRowPrefab == null)
        {
            if (entryRowPrefab == null)
                Debug.LogError("[LexiconPreviewLoopScrollDriver] entryRowPrefab 未在 Inspector 中指定（LexiconPreviewEntryRowCell 预制）。");
            return;
        }

        _loop.prefabSource = this;
        _loop.dataSource = this;
        _loop.StopMovement();
        if (Application.isPlaying)
            _loop.ClearCells();

        _loop.totalCount = _lines.Count;
        _loop.RefillCells();
        _loop.verticalNormalizedPosition = 1f;
    }

    public GameObject GetObject(int index)
    {
        if (index < 0 || index >= _lines.Count || entryRowPrefab == null)
            return null;

        if (_pool.Count > 0)
        {
            var t = _pool.Pop();
            t.gameObject.SetActive(true);
            return t.gameObject;
        }

        var go = Instantiate(entryRowPrefab, EnsureCellPoolRoot(), false);
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

        if (trans.GetComponent<LexiconPreviewEntryRowCell>() != null)
            _pool.Push(trans);
    }

    public void ProvideData(Transform transform, int idx)
    {
        if (idx < 0 || idx >= _lines.Count)
            return;

        transform.GetComponent<LexiconPreviewEntryRowCell>()?.Bind(idx + 1, _lines[idx]);
    }
}
