using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Reusable, skippable reveal for items already in the profile. Pure presentation.</summary>
public sealed class RewardRevealPanel : UIPanelBase
{
    public RectTransform panel, content;
    public GridLayoutGroup grid;
    public ScrollRect scroll;
    public RewardRevealCell cardPrefab;
    public TMP_Text heading, subtitle, hint, buttonLabel;
    public Button collectButton, skipButton;
    public Image glow, rays;
    public CharacterCatalog characters;
    public RewardRevealRequest Request { get; private set; }
    public bool IsOpen { get; private set; }
    public bool IsRevealing { get; private set; }
    public int VisibleItemCount => cards.Count;
    readonly List<RewardRevealCell> cards = new List<RewardRevealCell>();
    readonly List<RewardRevealCell> pool = new List<RewardRevealCell>();
    Coroutine reveal;
    bool closing;
    float idle;
    public override PanelTransition GetOpenTransition() => new PanelTransition(TransitionType.FadeAndScale, .16f);
    public override PanelTransition GetCloseTransition() => new PanelTransition(TransitionType.FadeAndScale, .15f);

    public override void OnOpen(object payload)
    {
        Request = payload as RewardRevealRequest;
        if (Request == null) return;
        if (panel == null || content == null || grid == null || scroll == null || cardPrefab == null || heading == null ||
            subtitle == null || hint == null || buttonLabel == null || collectButton == null || skipButton == null || glow == null || rays == null)
        {
            Request = null; IsOpen = false;
            Debug.LogWarning("[Reward reveal] Incomplete prefab configuration; earned rewards remain saved.");
            return;
        }
        closing = false; IsOpen = true; idle = 0;
        RecycleCards();
        heading.text = string.IsNullOrEmpty(Request.title) ? "恭喜获得" : Request.title;
        bool character = !string.IsNullOrEmpty(Request.characterId);
        subtitle.text = character ? "新的伙伴，新的冒险" : "奖励已放入背包";
        var text = new StringBuilder(heading.text + subtitle.text + "点击加速点击继续收下已解锁新伙伴×0123456789,");
        int count = character ? 1 : Request.items.Count;
        int columns = Mathf.Min(3, Mathf.Max(1, count));
        grid.constraintCount = columns;
        grid.cellSize = character ? new Vector2(256, 316) : count == 1 ? new Vector2(256, 304) : new Vector2(228, 304);
        grid.spacing = new Vector2(24, 26);
        grid.padding = new RectOffset(0, 0, 12, 22);
        float width = columns * grid.cellSize.x + (columns - 1) * grid.spacing.x;
        content.sizeDelta = new Vector2(width, 0);
        content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, 1);
        content.anchoredPosition = Vector2.zero;
        for (int i = 0; i < count; i++)
        {
            var card = GetCard();
            if (character) card.BindCharacter(characters != null ? characters.Get(Request.characterId) : null);
            else card.Bind(Request.items[i]);
            text.Append(card.nameLabel.text); cards.Add(card); card.Reveal(0);
        }
        WarmFont(text.ToString() + "奖励已到账，继续你的冒险点击加速揭晓");
        Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        scroll.vertical = content.rect.height > scroll.viewport.rect.height; scroll.verticalNormalizedPosition = 1;
        collectButton.onClick.RemoveListener(Advance); collectButton.onClick.AddListener(Advance);
        skipButton.onClick.RemoveListener(Advance); skipButton.onClick.AddListener(Advance);
        collectButton.interactable = skipButton.interactable = true;
        if (reveal != null) StopCoroutine(reveal);
        reveal = StartCoroutine(Reveal());
    }
    RewardRevealCell GetCard()
    {
        RewardRevealCell card;
        if (pool.Count > 0) { card = pool[pool.Count - 1]; pool.RemoveAt(pool.Count - 1); }
        else card = Instantiate(cardPrefab, content, false);
        card.gameObject.SetActive(true); return card;
    }
    void RecycleCards()
    {
        foreach (var card in cards) { card.gameObject.SetActive(false); pool.Add(card); }
        cards.Clear();
    }
    IEnumerator Reveal()
    {
        IsRevealing = true; hint.text = "点击加速揭晓"; buttonLabel.text = "收下";
        AudioService.Ensure().Play(AudioId.UiSwitch);
        float elapsed = 0, total = .26f + Mathf.Min(8, Mathf.Max(0, cards.Count - 1)) * .055f;
        while (elapsed < total)
        {
            float burst = Mathf.Sin(Mathf.Clamp01(elapsed / total) * Mathf.PI);
            rays.color = new Color(1, .88f, .54f, .12f + burst * .25f);
            glow.color = new Color(1, 1, 1, .45f + burst * .4f);
            for (int i = 0; i < cards.Count; i++) cards[i].Reveal((elapsed - Mathf.Min(i, 8) * .055f) / .26f);
            elapsed += Time.unscaledDeltaTime; yield return null;
        }
        reveal = null; CompleteReveal();
    }
    public void CompleteReveal()
    {
        if (!IsOpen) return;
        if (reveal != null) { StopCoroutine(reveal); reveal = null; }
        IsRevealing = false;
        foreach (var card in cards) card.Reveal(1);
        rays.color = new Color(1, .88f, .54f, .16f); glow.color = new Color(1, 1, 1, .6f);
        hint.text = "奖励已到账，继续你的冒险"; buttonLabel.text = "收下";
    }
    public void Advance()
    {
        if (IsRevealing) { UiClickSound.Play(); CompleteReveal(); return; }
        Dismiss();
    }
    public void Dismiss()
    {
        if (!IsOpen || closing || UIManager.Instance == null || UIManager.Instance.Top != this) return;
        CompleteReveal(); closing = true; collectButton.interactable = false; skipButton.interactable = false;
        UiClickSound.PlayClose(); UIManager.Instance.CloseTop();
    }
    void LateUpdate()
    {
        if (!IsOpen || rays == null) return;
        idle += Time.unscaledDeltaTime;
        rays.rectTransform.localRotation = Quaternion.Euler(0, 0, idle * 7);
    }
    void OnDisable() { if (IsOpen) CompleteReveal(); }
    public override void OnClose()
    {
        if (reveal != null) StopCoroutine(reveal); reveal = null;
        IsRevealing = false; IsOpen = false; Request = null;
        collectButton.onClick.RemoveListener(Advance); skipButton.onClick.RemoveListener(Advance);
        collectButton.interactable = skipButton.interactable = true;
        ItemTooltip.Hide();
    }
    static void WarmFont(string value)
    {
        BattleChineseFontRuntime.EnsureLoaded(); var font = BattleChineseFontRuntime.LoadedFont; if (font == null) return;
        var missing = new StringBuilder(); var seen = new HashSet<char>();
        foreach (char c in value) if (!char.IsWhiteSpace(c) && seen.Add(c) && !font.HasCharacter(c)) missing.Append(c);
        if (missing.Length > 0) { string unused; font.TryAddCharacters(missing.ToString(), out unused); }
    }
}
