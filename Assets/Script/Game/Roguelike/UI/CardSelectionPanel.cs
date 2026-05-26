using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 选卡面板：3 卡牌 + 刷新按钮（免费 1 次 + 广告 1 次）。由 UIManager 打开。
/// </summary>
public class CardSelectionPanel : UIPanelBase
{
    [Tooltip("3 个卡牌槽位")]
    public CardView[] cardSlots;

    [Tooltip("刷新按钮（Button 组件）")]
    public Button refreshButton;

    [Tooltip("刷新按钮文字（如 刷新(1) / 看广告刷新 / 隐藏）")]
    public TextMeshProUGUI refreshCountText;

    public GameObject panelRoot;

    private Action<int> _onCardSelected;
    private Action _onRefreshRequested;
    private Action _onAdRefreshRequested;

    private int _freeRefreshCount;
    private int _adRefreshCount;

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

        // 清除旧监听（包含 Prefab 上可能残留的旧 OnRefreshButtonClick）
        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveAllListeners();
            refreshButton.onClick.AddListener(OnRefreshClicked);
        }

        Show(p.Cards, p.OnCardSelected, p.OnRefreshRequested, p.OnAdRefreshRequested,
            p.FreeRefreshCount, p.AdRefreshCount);
    }

    public override void OnClose()
    {
        if (refreshButton != null)
            refreshButton.onClick.RemoveAllListeners();
        Hide();
        base.OnClose();
    }

    public bool Show(List<CardDeck.DrawResult> cards, Action<int> onCardSelected,
        Action onRefreshRequested, Action onAdRefreshRequested,
        int freeRefreshCount, int adRefreshCount)
    {
        _onCardSelected = onCardSelected;
        _onRefreshRequested = onRefreshRequested;
        _onAdRefreshRequested = onAdRefreshRequested;
        _freeRefreshCount = freeRefreshCount;
        _adRefreshCount = adRefreshCount;

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

        UpdateRefreshUI();
        return true;
    }

    public void UpdateRefreshCount(int free, int ad)
    {
        _freeRefreshCount = free;
        _adRefreshCount = ad;
        UpdateRefreshUI();
    }

    private void UpdateRefreshUI()
    {
        if (_freeRefreshCount > 0)
        {
            if (refreshCountText != null)
                refreshCountText.text = $"刷新({_freeRefreshCount})";
            if (refreshButton != null)
                refreshButton.gameObject.SetActive(true);
        }
        else if (_adRefreshCount > 0)
        {
            if (refreshCountText != null)
                refreshCountText.text = "看广告刷新";
            if (refreshButton != null)
                refreshButton.gameObject.SetActive(true);
        }
        else
        {
            if (refreshButton != null)
                refreshButton.gameObject.SetActive(false);
        }
    }

    public void Hide()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
        _onCardSelected = null;
        _onRefreshRequested = null;
        _onAdRefreshRequested = null;
    }

    private void OnCardClick(int index) => _onCardSelected?.Invoke(index);

    private void OnRefreshClicked()
    {
        UiClickSound.Play();

        if (_freeRefreshCount > 0)
        {
            _freeRefreshCount--;
            _onRefreshRequested?.Invoke();
        }
        else if (_adRefreshCount > 0)
        {
            _adRefreshCount--;
            _onAdRefreshRequested?.Invoke();
        }
    }
}

public class CardSelectionOpenPayload
{
    public List<CardDeck.DrawResult> Cards;
    public Action<int> OnCardSelected;
    public Action OnRefreshRequested;
    public Action OnAdRefreshRequested;
    public int FreeRefreshCount;
    public int AdRefreshCount;
}
