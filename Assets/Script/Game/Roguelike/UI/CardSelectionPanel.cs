using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 选卡面板：控制3张卡牌的显示、选择、刷新。由 <see cref="UIManager"/> 打开，或回退为 <see cref="CardSelectionSystem"/> 直接驱动。
/// </summary>
public class CardSelectionPanel : UIPanelBase
{
    [Tooltip("3个卡牌槽位（按左中右或三角形排列）")]
    public CardView[] cardSlots;

    [Tooltip("刷新按钮")]
    public GameObject refreshButton;

    [Tooltip("剩余刷新次数显示文本")]
    public TextMeshProUGUI refreshCountText;

    [Tooltip("面板根物体（控制显示/隐藏）")]
    public GameObject panelRoot;

    private Action<int> _onCardSelected;
    private Action _onRefreshRequested;

    private void Awake()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    public override void OnOpen(object payload)
    {
        var p = payload as CardSelectionOpenPayload;
        if (p == null)
        {
            Debug.LogError("[CardSelectionPanel] OnOpen 需要 CardSelectionOpenPayload");
            return;
        }
        Show(p.Cards, p.OnCardSelected, p.OnRefreshRequested, p.RemainingRefresh);
    }

    public override void OnClose()
    {
        Hide();
        base.OnClose();
    }

    /// <summary>
    /// 显示选卡面板（无 UIManager 时的回退路径）
    /// </summary>
    public bool Show(List<CardDeck.DrawResult> cards, Action<int> onCardSelected, Action onRefreshRequested, int refreshCount)
    {
        _onCardSelected = onCardSelected;
        _onRefreshRequested = onRefreshRequested;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (cardSlots == null || cardSlots.Length == 0)
        {
            Debug.LogError("[CardSelectionPanel] cardSlots is null or empty");
            return false;
        }

        for (int i = 0; i < cardSlots.Length; i++)
        {
            int index = i;
            if (i < cards.Count)
                cardSlots[i].Bind(cards[i], () => OnCardClick(index));
            else
                cardSlots[i].Hide();
        }

        UpdateRefreshCount(refreshCount);
        return true;
    }

    public void UpdateRefreshCount(int count)
    {
        if (refreshCountText != null)
            refreshCountText.text = $"刷新({count})";

        if (refreshButton != null)
            refreshButton.SetActive(count > 0);
    }

    public void Hide()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        _onCardSelected = null;
        _onRefreshRequested = null;
    }

    private void OnCardClick(int index) => _onCardSelected?.Invoke(index);

    /// <summary>
    /// 刷新按钮点击（由Inspector的Button组件调用或代码绑定）
    /// </summary>
    public void OnRefreshButtonClick()
    {
        UiClickSound.Play();
        _onRefreshRequested?.Invoke();
    }
}

/// <summary>
/// 交给 <see cref="UIManager.Open{T}"/> 的载荷。
/// </summary>
public class CardSelectionOpenPayload
{
    public List<CardDeck.DrawResult> Cards;
    public Action<int> OnCardSelected;
    public Action OnRefreshRequested;
    public int RemainingRefresh;
}
