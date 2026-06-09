using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通用物品 Tooltip，挂在 UIManager.overlayRoot 下共享 Canvas。
/// contentPanel 用 VerticalLayoutGroup + ContentSizeFitter(Vertical) 自适应高度；
/// 宽度在代码里根据文本内容动态计算。
/// </summary>
[DisallowMultipleComponent]
public class ItemTooltip : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descText;
    [SerializeField] private Button dismissMask;
    [SerializeField] private RectTransform contentPanel;

    [Header("尺寸限制")]
    [SerializeField] private float minWidth = 280f;
    [SerializeField] private float maxWidth = 800f;
    [SerializeField] private float maxHeight = 600f;

    private static ItemTooltip _instance;

    private static ItemTooltip Instance
    {
        get
        {
            if (_instance == null)
            {
                if (UIManager.Instance == null)
                {
                    Debug.LogWarning("[ItemTooltip] UIManager.Instance 不存在");
                    return null;
                }
                _instance = UIManager.Instance.GetOrCreateItemTooltip();
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (dismissMask != null) dismissMask.onClick.AddListener(Hide);
        gameObject.SetActive(false);
    }

    public static void Show(string itemName, string description, RectTransform anchorRect)
    {
        var tip = Instance;
        if (tip == null) return;
        tip.PopulateAndShow(itemName, description, anchorRect);
    }

    public static void Show(string itemName, string description)
    {
        var tip = Instance;
        if (tip == null) return;
        tip.PopulateAndShow(itemName, description, null);
    }

    public static void Hide()
    {
        if (_instance != null) _instance.gameObject.SetActive(false);
    }

    private void PopulateAndShow(string itemName, string description, RectTransform anchorRect)
    {
        if (nameText != null) nameText.text = itemName ?? "";
        if (descText != null) descText.text = description ?? "";

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (contentPanel == null) return;

        // 1. 计算合适宽度：取 name/desc 中较长者，限制在 [minWidth, maxWidth]
        float nameW = nameText != null ? nameText.GetPreferredValues(itemName ?? "", maxWidth, 0).x : 0f;
        float descW = descText != null ? descText.GetPreferredValues(description ?? "", maxWidth, 0).x : 0f;
        float textW = Mathf.Max(nameW, descW);

        // padding 预留（VerticalLayoutGroup 左右 padding 各 20）
        float panelW = Mathf.Clamp(textW + 44f, minWidth, maxWidth);

        // 2. 设宽度 + 强制重建布局 → 高度由 ContentSizeFitter 自动计算
        contentPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, panelW);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentPanel);

        // 3. 高度截断（过长描述限制 600）
        Vector2 size = contentPanel.sizeDelta;
        size.y = Mathf.Min(size.y, maxHeight);
        contentPanel.sizeDelta = size;

        // 4. 定位到 anchor 旁边
        PositionNear(anchorRect);
    }

    private void PositionNear(RectTransform anchorRect)
    {
        if (contentPanel == null) return;

        if (anchorRect == null)
        {
            contentPanel.anchoredPosition = Vector2.zero;
            return;
        }

        Vector3 anchorWorld = anchorRect.position;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)transform, anchorWorld, null, out Vector2 anchorLocal);

        Vector2 panelSize = contentPanel.sizeDelta;
        // pivot 默认 (0.5, 0.5) → 半个尺寸即是边界
        float halfW = panelSize.x * 0.5f;
        float halfH = panelSize.y * 0.5f;

        // 默认放 anchor 上方
        float gap = 12f;
        float targetX = anchorLocal.x;
        float targetY = anchorLocal.y + anchorRect.rect.height * 0.5f + halfH + gap;

        // 屏幕边界裁切
        Rect parentRect = ((RectTransform)transform).rect;
        float minX = parentRect.xMin + halfW;
        float maxX = parentRect.xMax - halfW;
        float minY = parentRect.yMin + halfH;
        float maxY = parentRect.yMax - halfH;

        targetX = Mathf.Clamp(targetX, minX, maxX);

        // 上方放不下 → 放下方
        if (targetY > maxY)
            targetY = anchorLocal.y - anchorRect.rect.height * 0.5f - halfH - gap;
        targetY = Mathf.Clamp(targetY, minY, maxY);

        contentPanel.anchoredPosition = new Vector2(targetX, targetY);
    }

    private void Reset()
    {
        minWidth = 280f;
        maxWidth = 800f;
        maxHeight = 600f;
    }
}
