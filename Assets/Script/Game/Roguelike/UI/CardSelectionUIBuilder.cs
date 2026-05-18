#if UNITY_EDITOR
using UnityEditor;
#endif
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 选卡UI构建器：一键生成 CardSelectionPanel 预制体结构
/// 使用方法：GameObject → Roguelike → Create Card Selection UI
/// </summary>
public class CardSelectionUIBuilder : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("GameObject/Roguelike/Create Card Selection UI", false, 10)]
    public static void CreateCardSelectionUI()
    {
        // 查找或创建 Canvas
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        // 创建面板根节点
        GameObject panelGO = new GameObject("CardSelectionPanel", typeof(RectTransform));
        panelGO.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // 添加半透明黑底
        Image bgImage = panelGO.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.85f);
        bgImage.raycastTarget = true;

        // 添加 CardSelectionPanel 脚本
        CardSelectionPanel panel = panelGO.AddComponent<CardSelectionPanel>();
        panel.panelRoot = panelGO;

        // 创建标题
        GameObject titleGO = CreateText(panelGO.transform, "Title", "选择你的能力");
        SetRect(titleGO.GetComponent<RectTransform>(),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(-200f, -100f), new Vector2(200f, -40f));
        var titleText = titleGO.GetComponent<TextMeshProUGUI>();
        titleText.fontSize = 48;
        titleText.alignment = TextAlignmentOptions.Center;
        BattleChineseFontRuntime.ApplyToTMP(titleText);

        // 创建3个卡牌槽位容器
        GameObject cardsContainer = new GameObject("CardsContainer", typeof(RectTransform));
        cardsContainer.transform.SetParent(panelGO.transform, false);
        RectTransform containerRect = cardsContainer.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.sizeDelta = new Vector2(1200f, 500f);

        // 添加水平布局
        HorizontalLayoutGroup hLayout = cardsContainer.AddComponent<HorizontalLayoutGroup>();
        hLayout.spacing = 40f;
        hLayout.childAlignment = TextAnchor.MiddleCenter;
        hLayout.childControlWidth = false;
        hLayout.childControlHeight = false;
        hLayout.childForceExpandWidth = false;
        hLayout.childForceExpandHeight = false;

        // 创建3张卡牌
        panel.cardSlots = new CardView[3];
        for (int i = 0; i < 3; i++)
        {
            panel.cardSlots[i] = CreateCardView(cardsContainer.transform, $"Card_{i + 1}");
        }

        // 创建刷新按钮
        GameObject refreshGO = new GameObject("RefreshButton", typeof(RectTransform));
        refreshGO.transform.SetParent(panelGO.transform, false);
        SetRect(refreshGO.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(-100f, 80f), new Vector2(100f, 140f));

        Button refreshBtn = refreshGO.AddComponent<Button>();
        Image refreshImg = refreshGO.AddComponent<Image>();
        refreshImg.color = new Color(0.3f, 0.6f, 0.9f);
        panel.refreshButton = refreshGO;

        // 刷新按钮文字
        GameObject refreshTextGO = CreateText(refreshGO.transform, "Text", "刷新(1)");
        refreshTextGO.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 60f);
        panel.refreshCountText = refreshTextGO.GetComponent<TextMeshProUGUI>();
        panel.refreshCountText.fontSize = 28;
        BattleChineseFontRuntime.ApplyToTMP(panel.refreshCountText);

        // 绑定按钮事件
        refreshBtn.onClick.AddListener(() => {
            if (panel != null) panel.OnRefreshButtonClick();
        });

        // 添加 EventSystem（如果没有）
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        // 选中面板
        Selection.activeGameObject = panelGO;
        Debug.Log("[CardSelectionUIBuilder] 选卡面板创建完成！请将 CardSelectionPanel 拖到 PlayerSkills 的 CardSelectionSystem 组件中。");
    }
#endif

    private static CardView CreateCardView(Transform parent, string name)
    {
        // 卡牌根节点
        GameObject cardGO = new GameObject(name, typeof(RectTransform));
        cardGO.transform.SetParent(parent, false);
        RectTransform cardRect = cardGO.GetComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(280f, 400f);

        CardView cardView = cardGO.AddComponent<CardView>();

        // 背景底图
        GameObject bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(cardGO.transform, false);
        SetRect(bgGO.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        cardView.background = bgGO.GetComponent<Image>();
        cardView.background.color = new Color(0.2f, 0.8f, 0.3f); // 默认绿色
        cardView.background.raycastTarget = true;

        // 边框光效
        GameObject glowGO = new GameObject("BorderGlow", typeof(RectTransform), typeof(Image));
        glowGO.transform.SetParent(cardGO.transform, false);
        SetRect(glowGO.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(-10f, -10f), new Vector2(10f, 10f));
        cardView.borderGlow = glowGO.GetComponent<Image>();
        cardView.borderGlow.color = new Color(1f, 1f, 1f, 0.5f);
        cardView.borderGlow.raycastTarget = false;

        // 标签图标
        GameObject labelIconGO = new GameObject("LabelIcon", typeof(RectTransform), typeof(Image));
        labelIconGO.transform.SetParent(cardGO.transform, false);
        SetRect(labelIconGO.GetComponent<RectTransform>(),
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(10f, -70f), new Vector2(70f, -10f));
        cardView.labelIcon = labelIconGO.GetComponent<Image>();
        cardView.labelIcon.raycastTarget = false;

        // 标签文字
        GameObject labelTextGO = CreateText(cardGO.transform, "LabelText", "新");
        SetRect(labelTextGO.GetComponent<RectTransform>(),
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(10f, -40f), new Vector2(70f, -10f));
        cardView.labelText = labelTextGO.GetComponent<TextMeshProUGUI>();
        cardView.labelText.fontSize = 24;
        cardView.labelText.color = Color.yellow;
        cardView.labelText.raycastTarget = false;
        BattleChineseFontRuntime.ApplyToTMP(cardView.labelText);

        // 技能图标
        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(cardGO.transform, false);
        SetRect(iconGO.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.7f),
            new Vector2(-60f, -60f), new Vector2(60f, 60f));
        iconGO.GetComponent<RectTransform>().sizeDelta = new Vector2(120f, 120f);
        cardView.icon = iconGO.GetComponent<Image>();
        cardView.icon.color = Color.white;
        cardView.icon.raycastTarget = false;

        // 技能名称
        GameObject titleGO = CreateText(cardGO.transform, "Title", "技能名称");
        SetRect(titleGO.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.45f),
            new Vector2(-120f, -30f), new Vector2(120f, 30f));
        cardView.titleText = titleGO.GetComponent<TextMeshProUGUI>();
        cardView.titleText.fontSize = 32;
        cardView.titleText.fontStyle = FontStyles.Bold;
        cardView.titleText.raycastTarget = false;
        BattleChineseFontRuntime.ApplyToTMP(cardView.titleText);

        // 描述文字
        GameObject descGO = CreateText(cardGO.transform, "Desc", "技能描述\n升级预览");
        SetRect(descGO.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.4f),
            new Vector2(-120f, -20f), new Vector2(120f, 20f));
        cardView.descText = descGO.GetComponent<TextMeshProUGUI>();
        cardView.descText.fontSize = 20;
        cardView.descText.raycastTarget = false;
        cardView.descText.enableWordWrapping = true;
        cardView.descText.overflowMode = TextOverflowModes.Truncate;
        BattleChineseFontRuntime.ApplyToTMP(cardView.descText);

        // 添加按钮（具体监听由 CardView.Bind → WireButton 绑定，避免与子 Graphic 射线冲突）
        Button btn = cardGO.AddComponent<Button>();
        btn.targetGraphic = cardView.background;
        cardView.clickButton = btn;

        return cardView;
    }

    private static GameObject CreateText(Transform parent, string name, string defaultText)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = defaultText;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        BattleChineseFontRuntime.ApplyToTMP(text);

        return go;
    }

    private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }
}
