using TMPro;
using UnityEngine;

/// <summary>
/// 红点角标组件：挂载在按钮子物体上，指定 sourceKey 即可自动响应 RedDotChangedEvent。
/// 超 99 显示 "99+"，count=0 自动隐藏。
/// </summary>
[DisallowMultipleComponent]
public class RedDotBadge : MonoBehaviour
{
    [Header("红点配置")]
    [Tooltip("对应 RedDotService 中的节点路径，如 battle/achievement、character、shop。")]
    [SerializeField] private string sourceKey = "";

    [Header("UI 引用")]
    [Tooltip("红点根节点（含圆圈 + 文字），count=0 时隐藏。")]
    [SerializeField] private GameObject badgeRoot;

    [Tooltip("数字文本（TMP），超过 99 自动截断显示 99+。")]
    [SerializeField] private TextMeshProUGUI countText;

    private void OnEnable()
    {
        EventBus.Subscribe<RedDotChangedEvent>(OnRedDotChanged, owner: this);
        Refresh();
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<RedDotChangedEvent>(OnRedDotChanged);
    }

    private void OnRedDotChanged(RedDotChangedEvent e)
    {
        if (e.sourceKey == sourceKey)
            Refresh();
    }

    public void Refresh()
    {
        if (string.IsNullOrEmpty(sourceKey)) return;
        int count = RedDotService.Instance.GetCount(sourceKey);
        bool visible = count > 0;

        if (badgeRoot != null)
            badgeRoot.SetActive(visible);

        if (countText != null && visible)
            countText.text = count > 99 ? "99+" : count.ToString();
    }

#if UNITY_EDITOR
    [ContextMenu("自动绑定子控件")]
    private void Reset()
    {
        badgeRoot = transform.Find("BadgeRoot")?.gameObject ?? gameObject;
        countText = transform.Find("CountText")?.GetComponent<TextMeshProUGUI>();
    }
#endif
}
