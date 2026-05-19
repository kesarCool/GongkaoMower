using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 选关纵向列表（简单版）：在 <see cref="ScrollRect.content"/> 下按顺序 Instantiate 行，不经过 LoopScrollRect。
/// 关卡量在几百行以内时足够用；Prefab 上拖 <b>listContent</b>（即 Scroll 的 Content）与两个 Cell 预制体即可。
/// </summary>
[DisallowMultipleComponent]
public class LevelSelectSimpleScrollList : MonoBehaviour
{
    [Tooltip("列表父节点，一般为 ScrollRect 的 Content。")]
    [SerializeField] private RectTransform listContent;

    [Tooltip("可选；有则刷新后滚到顶部。")]
    [SerializeField] private ScrollRect scrollRect;

    [SerializeField] private GameObject chapterHeaderPrefab;
    [SerializeField] private GameObject levelRowPrefab;

    public void RefreshFromTable()
    {
        var content = ResolveContent();
        if (content == null)
        {
            Debug.LogWarning("[LevelSelectSimpleScrollList] 未指定 listContent，且未找到子级 ScrollRect.content。");
            return;
        }

        if (chapterHeaderPrefab == null || levelRowPrefab == null)
        {
            Debug.LogWarning("[LevelSelectSimpleScrollList] 章节或关卡 Prefab 未配置。");
            return;
        }

        if (content.GetComponentInParent<LoopVerticalScrollRect>() != null)
        {
            Debug.LogError("[LevelSelectSimpleScrollList] 当前 Scroll 上挂了 LoopVerticalScrollRect，与本组件冲突。请改成 Unity 自带 ScrollRect，否则行会被 Loop 当成 cell 回收掉。");
            return;
        }

        EnsureVerticalLayout(content);

        // Destroy 本帧末才真正移除；若同一帧内立刻 Instantiate，会与旧子节点叠在一起导致再打开时布局乱。此处必须立即清空。
        for (int i = content.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(content.GetChild(i).gameObject);

        if (TableManager.Instance == null)
        {
            GameErrorPresenter.Show(GameErrorCodes.TableManagerMissing);
            return;
        }

        TableManager.Instance.Init();
#if USE_FB_TABLE
        var dict = TableManager.Instance.GetTable<ProtoTable.ChapterLevel>();
#else
        var dict = new Dictionary<int, object>();
#endif
        var rows = ChapterLevelListBuilder.Build(dict);

        foreach (var row in rows)
        {
            var prefab = row.Kind == LevelSelectRowKind.ChapterHeader ? chapterHeaderPrefab : levelRowPrefab;
            var go = Instantiate(prefab, content, false);
            go.SetActive(true);
            if (row.Kind == LevelSelectRowKind.ChapterHeader)
                go.GetComponent<LevelSelectChapterHeaderCell>()?.Bind(row);
            else
                go.GetComponent<LevelSelectLevelRowCell>()?.Bind(row);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        Canvas.ForceUpdateCanvases();

        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    private RectTransform ResolveContent()
    {
        if (listContent != null)
            return listContent;

        if (scrollRect == null)
            scrollRect = GetComponentInChildren<ScrollRect>(true);
        return scrollRect != null ? scrollRect.content : null;
    }

    private static void EnsureVerticalLayout(RectTransform content)
    {
        if (content.GetComponent<VerticalLayoutGroup>() == null)
        {
            var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 4f;
            v.padding = new RectOffset(8, 8, 8, 8);
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
        }

        if (content.GetComponent<ContentSizeFitter>() == null)
        {
            var f = content.gameObject.AddComponent<ContentSizeFitter>();
            f.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }
}
