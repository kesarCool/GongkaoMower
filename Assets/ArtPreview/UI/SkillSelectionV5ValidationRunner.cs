#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SkillSelectionV5ValidationRunner : UIValidationRunnerBase
{
    private bool StarsOnly => UnityEditor.SessionState.GetBool("SkillSelectionV5.StarsOnly", false);
    protected override string OutputFolder => StarsOnly ? "SkillSelectionV5/LayeredStars" : "SkillSelectionV5/After";
    protected override string ValidationNote => "Actual Game scene, UGUI prefabs, skills and energy data; pointer events in paused time, cancellation and re-entry checks. In-memory profile; no WeChat device validation.";
    private PlayerSkills skills;
    private CardSelectionPanel panel;
    private int selections, refreshes, selectedIndex;
    private float savedTimeScale;
    private bool saved;
    private bool savedBackground;
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private void Awake() { savedBackground = Application.runInBackground; Application.runInBackground = true; }

    protected override IEnumerator RunChecks()
    {
        savedTimeScale = Time.timeScale; saved = true;
        TutorialDirector.Progress.skipped = true;
        TutorialDirector.Ensure().StopAllCoroutines();
        yield return SceneManager.UnloadSceneAsync("Home");
        SelectedLevelContext.Set(1, 101);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/Game.unity", LoadSceneMode.Additive);
        SceneManager.SetActiveScene(SceneManager.GetSceneByName("Game"));
        yield return new WaitForSecondsRealtime(.4f);
        foreach (var s in FindObjectsOfType<SpawnerWaves>()) { s.StopAllCoroutines(); s.enabled = false; }
        foreach (var s in FindObjectsOfType<CardSelectionSystem>()) { s.StopAllCoroutines(); s.enabled = false; }
        foreach (var s in FindObjectsOfType<BattleOutcomeCoordinator>()) { s.StopAllCoroutines(); s.enabled = false; }
        foreach (var s in FindObjectsOfType<DynamicJoystick>()) s.enabled = false;
        foreach (var enemy in FindObjectsOfType<EnemyBase>()) Destroy(enemy.gameObject);
        UIManager.Instance.CloseAllStack();
        skills = FindObjectOfType<PlayerSkills>(); skills.ClearAllSkills(); skills.enabled = false;
        skills.TryAddSkill(SkillId.AutoProjectileSword); skills.TryAddSkill(SkillId.LineBeam);
        skills.GetComponent<PlayerController>().enabled = false;
        skills.GetComponent<PlayerHealth>().SetInvulnerable(true);
        BattlePlayerPositioning.Teleport(skills.transform, new Vector2(15, 15));
        Camera.main.transform.position = new Vector3(15, 15, -10);
        yield return null;

        var activeCards = new List<CardDeck.DrawResult> { Card(SkillId.LightningStrike, 0), Card(SkillId.LineBeam, 2), Card(SkillId.AutoProjectileSword, 4) };
        Show(activeCards);
        Record("All cards have five stars and no middle Lv text", panel.cardSlots.All(c => c.starRow != null && c.starRow.stars.Length == 5 && !c.levelText.gameObject.activeSelf));
        Record("New, upgrade and five-star previews use the correct target", panel.cardSlots[0].starRow.PreviewLevel == 1 && panel.cardSlots[1].starRow.PreviewLevel == 3 && panel.cardSlots[2].starRow.PreviewLevel == 5);
        Record("Only the five-star preview is red", !panel.cardSlots[0].starRow.IsRed && !panel.cardSlots[1].starRow.IsRed && panel.cardSlots[2].starRow.IsRed);
        Record("Every card has five persistent empty backings and separate filled overlays", panel.cardSlots.All(c => c.starRow.emptyStars != null && c.starRow.emptyStars.Length == 5 &&
            c.starRow.emptyStars.All(s => s.gameObject.activeInHierarchy && s.sprite == c.starRow.emptySprite) &&
            c.starRow.stars.Select((s, i) => s.transform.parent == c.starRow.emptyStars[i].transform && s.sprite == c.starRow.filledSprite).All(ok => ok)));
        var backingColors = panel.cardSlots[1].starRow.emptyStars.Select(s => s.color).ToArray();
        float minAlpha = 1, maxAlpha = 0;
        var pending = panel.cardSlots[1].starRow.stars[2];
        for (int n = 0; n < 18; n++) { yield return new WaitForSecondsRealtime(.06f); minAlpha = Mathf.Min(minAlpha, pending.color.a); maxAlpha = Mathf.Max(maxAlpha, pending.color.a); }
        Record("Only the next filled star fades fully off and on while paused", Time.timeScale == 0 && minAlpha < .05f && maxAlpha > .95f);
        Record("Empty star backings stay opaque and stationary through the blink", panel.cardSlots[1].starRow.emptyStars.Select((s, i) => s.color == backingColors[i] && s.color.a == 1 && s.rectTransform.localScale == Vector3.one).All(ok => ok));
        Record("Filled overlays keep exact centered alignment with their backing", panel.cardSlots.All(c => c.starRow.stars.Select((s, i) => {
            var backing = c.starRow.emptyStars[i].rectTransform;
            return s.rectTransform.sizeDelta == backing.sizeDelta && s.rectTransform.localScale == Vector3.one &&
                Vector3.Distance(s.rectTransform.TransformPoint(s.rectTransform.rect.center), backing.TransformPoint(backing.rect.center)) < .001f;
        }).All(ok => ok)));
        Record("Owned filled stars stay opaque and future stars have no filled overlay", panel.cardSlots[1].starRow.stars.Take(2).All(s => s.color.a == 1) &&
            panel.cardSlots[0].starRow.stars.Skip(1).All(s => !s.gameObject.activeSelf));
        Record("Description and star footer reserve separate vertical space", panel.cardSlots.All(c =>
            -c.starRow.GetComponent<RectTransform>().anchoredPosition.y > -c.descriptionScroll.viewport.anchoredPosition.y + c.descriptionScroll.viewport.rect.height + 24));
        yield return Capture("01-star-level-previews-1080x1920");
        if (StarsOnly)
        {
            yield return RecordStarBlink();
            Time.timeScale = savedTimeScale;
            yield break;
        }

        SkillId bond = skills.skillCatalog.Get(SkillId.AutoProjectileSword).bondedPassiveId;
        var mixed = new List<CardDeck.DrawResult> { Card(bond, 0), Card(SkillId.LineBeam, 1), Card(SkillId.LightningStrike, 0) };
        Show(mixed);
        yield return new WaitForSecondsRealtime(.15f);
        var matched = panel.GetComponentsInChildren<SkillSlotCell>().Where(c => {
            var field = typeof(SkillSlotCell).GetField("highlightFrame", Hidden); var image = (Image)field.GetValue(c); return image != null && image.gameObject.activeSelf;
        }).ToArray();
        Record("Bond highlights both equipped icon and the card's matching icon", matched.Length == 2);
        float maximumScale = 1;
        for (int n = 0; n < 8; n++)
        {
            foreach (var cell in matched) maximumScale = Mathf.Max(maximumScale, cell.shimmerImage.transform.parent.localScale.x);
            yield return new WaitForSecondsRealtime(.05f);
        }
        Record("Entire matched icons have a larger bounce and masked moving shine", maximumScale > 1.19f && matched.All(c => c.shimmerImage.gameObject.activeSelf && c.shimmerImage.transform.parent.GetComponent<Mask>() != null));
        Record("Matched icons scale about centered anchors and pivot without horizontal drift", matched.All(c => {
            var rt = (RectTransform)c.shimmerImage.transform.parent;
            return rt.anchorMin == new Vector2(.5f, .5f) && rt.anchorMax == new Vector2(.5f, .5f) && rt.pivot == new Vector2(.5f, .5f) && Mathf.Abs(rt.anchoredPosition.x) < .001f;
        }));
        var slots = panel.GetComponentsInChildren<SkillSlotCell>();
        Record("Equipped skill levels use original Lv badges and no slot stars", slots.All(c => c.starRow == null) && slots.Count(c => {
            var text = (TMPro.TMP_Text)typeof(SkillSlotCell).GetField("levelText", Hidden).GetValue(c);
            return text != null && text.gameObject.activeInHierarchy && text.text.StartsWith("Lv.");
        }) == 2);
        yield return Capture("02-bond-icon-feedback-1080x1920");

        Show(activeCards, .5f);
        panel.cardSlots[0].OnClick();
        Record("Open protection rejects early selection without disabling cards", !panel.IsCardConfirmationPending && panel.cardSlots.All(c => c.clickButton.interactable));
        yield return new WaitForSecondsRealtime(.55f);
        var card = panel.cardSlots[1];
        var e = Pointer(card);
        Down(card, e); yield return new WaitForSecondsRealtime(.11f);
        Record("Pointer down compresses the face without applying a skill", card.pressVisual.localScale.x < .98f && selections == 0 && !card.IsConfirming);
        ExecuteEvents.Execute(card.gameObject, e, ExecuteEvents.pointerExitHandler);
        UpClick(card, e); yield return new WaitForSecondsRealtime(.5f);
        Record("Sliding out cancels selection and restores the card", selections == 0 && !panel.IsCardConfirmationPending && Mathf.Abs(card.pressVisual.localScale.x - 1) < .001f);
        e = Pointer(card); Down(card, e);
        ExecuteEvents.Execute(card.gameObject, e, ExecuteEvents.beginDragHandler);
        UpClick(card, e); yield return null;
        Record("Dragging cannot also choose a card", selections == 0 && !panel.IsCardConfirmationPending);

        e = Pointer(card); Down(card, e); yield return new WaitForSecondsRealtime(.1f); UpClick(card, e);
        yield return new WaitForSecondsRealtime(.1f);
        panel.cardSlots[0].OnClick(); panel.refreshButton.onClick.Invoke();
        Record("Release starts visible feedback before confirmation", card.IsConfirming && panel.IsCardConfirmationPending && selections == 0 && panel.cardSlots[0].GetComponent<CanvasGroup>().alpha < .95f);
        Record("Selection feedback locks other cards and refresh", !panel.refreshButton.interactable && panel.cardSlots.All(c => !c.clickButton.interactable) && refreshes == 0);
        yield return new WaitForSecondsRealtime(.5f);
        Record("Paused-time feedback invokes exactly one selected callback", selections == 1 && selectedIndex == 1 && Time.timeScale == 0);
        Show(activeCards);
        Record("Reopening resets face transforms, opacity and locks", panel.cardSlots.All(c => c.clickButton.interactable && c.GetComponent<CanvasGroup>().alpha == 1 && c.pressVisual.localScale == Vector3.one) && panel.refreshButton.interactable);
        yield return new WaitForSecondsRealtime(.35f);
        e = Pointer(panel.cardSlots[0]); Down(panel.cardSlots[0], e); UpClick(panel.cardSlots[0], e);
        UIManager.Instance.CloseAllStack();
        yield return new WaitForSecondsRealtime(.5f);
        Record("Closing during feedback cancels the pending callback", selections == 1);
        Show(mixed); yield return new WaitForSecondsRealtime(.35f);
        panel.SetRewardRequestPending(true); panel.cardSlots[0].OnClick();
        Record("Pending share refresh still blocks card confirmation", !panel.IsCardConfirmationPending && selections == 1);
        panel.SetRewardRequestPending(false);
        panel.refreshButton.onClick.Invoke();
        Record("Refresh still invokes its original business callback once", refreshes == 1);
        Show(activeCards, 1.2f);
        yield return new WaitForSecondsRealtime(.4f); panel.cardSlots[0].OnClick();
        Record("Boss input delay is enforced before any selection animation", !panel.IsCardConfirmationPending && panel.cardSlots[0].clickButton.interactable);
        yield return new WaitForSecondsRealtime(.9f); panel.cardSlots[0].OnClick();
        yield return new WaitForSecondsRealtime(.5f);
        Record("Boss-delay selection completes without leaving disabled cards stuck", selections == 2);

        Show(mixed);
        changeResolution?.Invoke(1080, 2400); yield return Capture("03-long-screen-1080x2400");
        Record("Long-screen paper stays inside the screen", OnScreen(panel.selectionLayout.paper));
        changeResolution?.Invoke(1080, 1440); yield return Capture("04-compact-screen-1080x1440");
        Record("Compact paper and footer stay inside the screen", OnScreen(panel.selectionLayout.paper) && OnScreen(panel.selectionLayout.hint));
        changeResolution?.Invoke(1080, 1920); UIManager.Instance.CloseAllStack();
        Time.timeScale = 0;
        var hud = FindObjectOfType<GameLayer>();
        var energy = skills.GetComponent<PlayerEnergy>(); energy.enabled = false;
        energy.energy = Mathf.Max(1, energy.EnergyRequiredForNextCard / 2); hud.RefreshEnergyProgress();
        yield return new WaitForSecondsRealtime(.5f);
        float expected = (float)energy.energy / energy.EnergyRequiredForNextCard;
        Record("Energy HUD uses twenty bright slanted segments with correct progress", hud.energyPresentation != null && hud.energyPresentation.segments.segmentCount == 20 && Mathf.Abs(hud.energyPresentation.DisplayedProgress - expected) < .02f);
        Canvas.ForceUpdateCanvases();
        Record("Energy segments have an active CanvasRenderer", hud.energyPresentation.segments.GetComponent<CanvasRenderer>() != null && !hud.energyPresentation.segments.canvasRenderer.cull);
        Record("Energy HUD identifies itself with caption, orb, count and level", hud.energyPresentation.caption.text == "升级能量" && hud.energyPresentation.energyOrb.sprite.name == "energy_orb" && hud.energyPresentation.countText.text.Contains(" / ") && hud.energyProgressText.text.StartsWith("Lv"));
        yield return Capture("05-energy-hud-1080x1920");
        int before = energy.CompletedCardSelectionCount;
        typeof(PlayerEnergy).GetField("_triggerCount", Hidden).SetValue(energy, before + 1); energy.energy = 0; hud.RefreshEnergyProgress();
        yield return new WaitForSecondsRealtime(.1f);
        Record("Level-up produces a full energy flash and upgrade caption", hud.energyPresentation.DisplayedProgress > .99f && hud.energyPresentation.caption.text.Contains("升级！"));
        yield return new WaitForSecondsRealtime(.9f);
        Record("New level settles back to its real remaining-energy value", hud.energyPresentation.DisplayedProgress < .03f && hud.energyProgressText.text == "Lv" + (energy.CompletedCardSelectionCount + 1));
        typeof(GameLayer).GetMethod("ShowBossHp", Hidden).Invoke(hud, new object[] { "首领" });
        Record("Boss health still replaces all energy decorations and labels", !hud.energyProgressSlider.gameObject.activeSelf && !hud.energyProgressText.gameObject.activeSelf && hud.bossHpSlider.gameObject.activeSelf);
        typeof(GameLayer).GetMethod("HideBossHp", Hidden).Invoke(hud, null);
        Record("Energy display returns after the Boss bar is hidden", hud.energyProgressSlider.gameObject.activeSelf && !hud.bossHpSlider.gameObject.activeSelf);
        Time.timeScale = 1;
        var system = FindObjectOfType<CardSelectionSystem>(true);
        typeof(CardSelectionSystem).GetField("_isSelecting", Hidden).SetValue(system, true);
        typeof(CardSelectionSystem).GetField("_currentCards", Hidden).SetValue(system, activeCards);
        typeof(CardSelectionSystem).GetField("_selectionUsesUIManager", Hidden).SetValue(system, true);
        panel = UIManager.Instance.Open<CardSelectionPanel>(new CardSelectionOpenPayload { Cards = activeCards, InputDelay = .3f,
            OnCardSelected = index => typeof(CardSelectionSystem).GetMethod("OnCardSelected", Hidden).Invoke(system, new object[] { index }) },
            new UiOpenOptions { PauseTime = true, UseUnscaledTime = true, CloseOnBack = false });
        changeResolution?.Invoke(540, 960);
        yield return new WaitForSecondsRealtime(.4f);
        yield return RecordActualSelection(panel.cardSlots[0]);
        Record("Actual game selection applies its skill once, closes the panel and resumes battle", skills.GetSkillLevel(SkillId.LightningStrike) == 1 && UIManager.Instance.Top == null && Time.timeScale > 0);
        changeResolution?.Invoke(1080, 1920);
        Time.timeScale = savedTimeScale;
    }
    private IEnumerator RecordActualSelection(CardView card)
    {
        string folder = Path.Combine(output, "selection-motion"); Directory.CreateDirectory(Path.Combine(folder, "frames"));
        var times = new List<float>();
        float start = Time.realtimeSinceStartup;
        bool down = false, up = false;
        var pointer = Pointer(card);
        while (Time.realtimeSinceStartup - start < 1.25f)
        {
            float elapsed = Time.realtimeSinceStartup - start;
            if (!down && elapsed >= .15f) { Down(card, pointer); down = true; }
            if (!up && elapsed >= .38f) { UpClick(card, pointer); up = true; }
            yield return new WaitForEndOfFrame();
            times.Add(Time.realtimeSinceStartup - start);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "frames", "frame-" + (times.Count - 1).ToString("D4") + ".png"));
            yield return new WaitForSecondsRealtime(.04f);
        }
        File.WriteAllText(Path.Combine(folder, "frame-times.json"), "[" + string.Join(",", times.Select(t => t.ToString(CultureInfo.InvariantCulture))) + "]");
        File.WriteAllText(Path.Combine(folder, "motion.json"), "{\"clock\":\"unscaled UI\",\"retimed\":false,\"frames\":" + times.Count + "}");
    }
    private IEnumerator RecordStarBlink()
    {
        changeResolution?.Invoke(540, 960);
        yield return new WaitForSecondsRealtime(.15f);
        string folder = Path.Combine(output, "star-motion"); Directory.CreateDirectory(Path.Combine(folder, "frames"));
        var times = new List<float>();
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 1.35f)
        {
            yield return new WaitForEndOfFrame();
            times.Add(Time.realtimeSinceStartup - start);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "frames", "frame-" + (times.Count - 1).ToString("D4") + ".png"));
            yield return new WaitForSecondsRealtime(.04f);
        }
        File.WriteAllText(Path.Combine(folder, "frame-times.json"), "[" + string.Join(",", times.Select(t => t.ToString(CultureInfo.InvariantCulture))) + "]");
        File.WriteAllText(Path.Combine(folder, "motion.json"), "{\"clock\":\"unscaled UI\",\"retimed\":false,\"frames\":" + times.Count + "}");
        changeResolution?.Invoke(1080, 1920);
    }
    private CardDeck.DrawResult Card(SkillId id, int level)
    {
        var deck = UnityEditor.AssetDatabase.LoadAssetAtPath<CardDeck>("Assets/ScriptableObject/Roguelike/Card Deck/CardDeck.asset");
        var def = skills.skillCatalog.Get(id);
        return new CardDeck.DrawResult { skillId = id, skillDef = def, currentLevel = level, targetLevel = level + 1,
            template = id.IsPassive() ? deck.passiveTemplate : level == 0 ? deck.newSkillTemplate : level + 1 >= def.maxLevel ? deck.breakthroughTemplate : deck.upgradeTemplate };
    }
    private void Show(List<CardDeck.DrawResult> cards, float delay = .3f)
    {
        panel = UIManager.Instance.Open<CardSelectionPanel>(new CardSelectionOpenPayload { Cards = cards, FreeRefreshCount = 1, AdRefreshCount = 1, InputDelay = delay,
            OnCardSelected = index => { selections++; selectedIndex = index; }, OnRefreshRequested = () => refreshes++ },
            new UiOpenOptions { PauseTime = true, UseUnscaledTime = true, CloseOnBack = false });
        Canvas.ForceUpdateCanvases(); panel.selectionLayout.FitToSafeArea();
    }
    private static PointerEventData Pointer(CardView card)
    {
        var e = new PointerEventData(EventSystem.current) { pointerId = -1, button = PointerEventData.InputButton.Left, eligibleForClick = true };
        e.position = RectTransformUtility.WorldToScreenPoint(null, card.background.rectTransform.TransformPoint(card.background.rectTransform.rect.center));
        e.pressPosition = e.position;
        e.pointerPress = card.gameObject;
        return e;
    }
    private static void Down(CardView card, PointerEventData e) { ExecuteEvents.Execute(card.gameObject, e, ExecuteEvents.pointerDownHandler); }
    private static void UpClick(CardView card, PointerEventData e)
    {
        ExecuteEvents.Execute(card.gameObject, e, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(card.gameObject, e, ExecuteEvents.pointerClickHandler);
    }
    private static bool OnScreen(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        return corners.All(p => { var s = RectTransformUtility.WorldToScreenPoint(null, p); return s.x >= -1 && s.x <= Screen.width + 1 && s.y >= -1 && s.y <= Screen.height + 1; });
    }
    protected override void OnDestroy() { Application.runInBackground = savedBackground; if (saved) Time.timeScale = savedTimeScale; base.OnDestroy(); }
}
#endif
