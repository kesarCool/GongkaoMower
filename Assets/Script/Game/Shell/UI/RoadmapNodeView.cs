using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 路线图关卡节点：圆形底 + 星级 + 关卡名 + 英雄指示器 + 点击。
/// 运行时由 HomeRoadmapView 动态实例化。
///
/// ════ 视觉控制点 ════
/// 节点尺寸：HomeRoadmapView.nodeSize（Inspector 可调，默认 130）
/// 以下字段全部在 Inspector 可调，改完即时生效无需重编译。
/// </summary>
[DisallowMultipleComponent]
public class RoadmapNodeView : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private Image circleBg;
    [SerializeField] private Image star1;
    [SerializeField] private Image star2;
    [SerializeField] private Image star3;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Button clickButton;
    [SerializeField] private GameObject heroIndicator;
    [SerializeField] private Image heroPortrait;
    [SerializeField] private GameObject battleIcon;

    [Header("V 形连线")]
    [SerializeField] private Image leftUpLine;
    [SerializeField] private Image rightUpLine;

    [Header("状态色")]
    [SerializeField] private Color clearedBgColor = new Color(1f, 0.78f, 0.2f, 1f);
    [SerializeField] private Color currentBgColor = new Color(0.92f, 0.92f, 0.92f, 1f);
    [SerializeField] private Color unlockedBgColor = new Color(0.92f, 0.92f, 0.92f, 1f);
    [SerializeField] private Color lockedBgColor = new Color(0.28f, 0.28f, 0.3f, 1f);
    [SerializeField] private Color lockedTextColor = new Color(0.45f, 0.45f, 0.5f, 1f);

    [Header("星级")]
    [Tooltip("星星 Sprite（共用一张图，颜色区分亮暗）。")]
    [SerializeField] private Sprite starSprite;
    [Tooltip("已获得星星颜色。")]
    [SerializeField] private Color starActiveColor = new Color(1f, 0.82f, 0.15f, 1f);
    [Tooltip("未获得星星颜色。")]
    [SerializeField] private Color starDimColor = new Color(0.15f, 0.15f, 0.15f, 1f);

    [Header("缩放")]
    [SerializeField] private float clearedScale = 1.08f;
    [SerializeField] private float currentScale = 1.12f;

    [Header("弹跳动画")]
    [SerializeField] private float bounceFrequency = 2.5f;
    [SerializeField] private float bounceAmplitude = 6f;

    public int LevelId { get; private set; }
    public int ChapterId { get; private set; }
    public bool IsCleared { get; private set; }
    public bool IsCurrent { get; private set; }
    public bool IsUnlocked { get; private set; }

    private System.Action<RoadmapNodeView> _onClick;

    /// <summary>设置圆形底贴图（仅当 Prefab 未配置 sprite 时作为兜底注入，不覆盖 Inspector 设定值）。</summary>
    public void SetCircleSprite(Sprite sprite)
    {
        if (circleBg != null && sprite != null && circleBg.sprite == null)
        {
            circleBg.sprite = sprite;
            circleBg.enabled = true;
        }
    }

    /// <summary>设置 V 形连线状态并注入 sprite（无 sprite 的 Image 不渲染）。仅显示活跃方向的连线。</summary>
    public void SetLineState(bool leftActive, bool rightActive, Color activeColor, Color dimColor, Sprite lineSprite)
    {
        if (leftUpLine != null)
        {
            if (lineSprite != null && leftUpLine.sprite == null) leftUpLine.sprite = lineSprite;
            leftUpLine.color = activeColor;
            leftUpLine.enabled = leftActive;
        }
        if (rightUpLine != null)
        {
            if (lineSprite != null && rightUpLine.sprite == null) rightUpLine.sprite = lineSprite;
            rightUpLine.color = activeColor;
            rightUpLine.enabled = rightActive;
        }
    }

    /// <summary>隐藏两条连线（最后一个节点用）。</summary>
    public void HideLines()
    {
        if (leftUpLine != null) leftUpLine.enabled = false;
        if (rightUpLine != null) rightUpLine.enabled = false;
    }

    private float _bounceBaseY;
    private float _bounceTime;

    private void Awake()
    {
        if (clickButton != null)
            clickButton.onClick.AddListener(() => _onClick?.Invoke(this));
    }

    private void OnDestroy()
    {
        if (clickButton != null)
            clickButton.onClick.RemoveAllListeners();
    }

    public void Bind(int levelId, int chapterId, bool unlocked, bool cleared, int stars,
        string mapName, bool isCurrent, Sprite heroSprite, System.Action<RoadmapNodeView> onClick)
    {
        LevelId = levelId;
        ChapterId = chapterId;
        IsCleared = cleared;
        IsCurrent = isCurrent;
        IsUnlocked = unlocked;
        _onClick = onClick;

        // ── 圆形底 ──
        if (circleBg != null)
        {
            if (cleared)
            {
                circleBg.color = clearedBgColor;
                circleBg.transform.localScale = Vector3.one * clearedScale;
            }
            else if (isCurrent)
            {
                circleBg.color = currentBgColor;
                circleBg.transform.localScale = Vector3.one * currentScale;
            }
            else if (unlocked)
            {
                circleBg.color = unlockedBgColor;
                circleBg.transform.localScale = Vector3.one;
            }
            else
            {
                circleBg.color = lockedBgColor;
                circleBg.transform.localScale = Vector3.one;
            }
        }

        // ── 星级（图片：已获亮色 / 未获暗色，未通关不显示）──
        int earned = cleared ? Mathf.Clamp(stars, 0, 3) : -1;
        ApplyStarImage(star1, 0, earned);
        ApplyStarImage(star2, 1, earned);
        ApplyStarImage(star3, 2, earned);

        // ── 关卡名 ──
        if (nameText != null)
        {
            BattleChineseFontRuntime.EnsureLoaded();
            BattleChineseFontRuntime.ApplyToTMP(nameText);
            string label = !string.IsNullOrEmpty(mapName) ? mapName : $"关{levelId % 100}";
            nameText.text = label;
            nameText.color = !unlocked && !cleared ? lockedTextColor : Color.white;
        }

        // ── 英雄指示器 ──
        if (heroPortrait != null)
        {
            heroPortrait.sprite = heroSprite;
            heroPortrait.enabled = isCurrent && heroSprite != null;
        }
        else if (isCurrent)
        {
            Debug.LogWarning($"[RoadmapNode] heroPortrait Image 为 null——请检查 Prefab 上是否已拖拽绑定");
        }
        if (heroIndicator != null)
            heroIndicator.SetActive(isCurrent);
        if (battleIcon != null)
            battleIcon.SetActive(isCurrent);

        // ── 按钮 ──
        if (clickButton != null)
            clickButton.interactable = unlocked || cleared; // 已通关也可点（弹详情）

        // ── 记录弹跳基准 ──
        if (heroIndicator != null && heroIndicator.activeSelf)
            _bounceBaseY = heroIndicator.transform.localPosition.y;
    }

    /// <summary>设置单颗星星图片：index < earned → 亮色，否则暗色；earned < 0 → 隐藏全部。</summary>
    private void ApplyStarImage(Image star, int index, int earned)
    {
        if (star == null) return;
        if (earned < 0)
        {
            star.gameObject.SetActive(false);
            return;
        }
        star.gameObject.SetActive(true);
        if (starSprite != null) star.sprite = starSprite;
        star.color = index < earned ? starActiveColor : starDimColor;
    }

    private void Update()
    {
        // 英雄指示器呼吸弹跳
        if (heroIndicator == null || !heroIndicator.activeSelf) return;
        _bounceTime += Time.unscaledDeltaTime * bounceFrequency;
        float offset = Mathf.Sin(_bounceTime) * bounceAmplitude;
        var lp = heroIndicator.transform.localPosition;
        lp.y = _bounceBaseY + offset;
        heroIndicator.transform.localPosition = lp;
    }
}
