using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>First-session guide. Owns presentation only; gameplay retains all business actions.</summary>
[DisallowMultipleComponent]
public sealed class TutorialDirector : MonoBehaviour
{
    public const string StartBattle = "start_battle";
    public const string Move = "move";
    public const string AutoAttack = "auto_attack";
    public const string SelectSkill = "select_skill";
    public const string BondedPassive = "bonded_passive";
    public const string CharacterTab = "character_tab";
    public const string UpgradeHero = "upgrade_hero";
    public const float TargetWaitSeconds = 8f;
    public const float StepTimeoutSeconds = 45f;

    static TutorialDirector instance;
    static float releaseInputAt;
    Coroutine flow;
    TutorialOverlay overlay;
    IDisposable pauseLock;
    string activeStep;
    bool continued, cardApplied, heroUpgraded;
    bool battleIntroductionPending;
    CardSelectionPanel bondedExplanationPanel;
    float visibleDeadline;

    public static TutorialDirector Current => instance;
    public static bool IsShowing => instance != null && instance.overlay != null && instance.overlay.IsVisible;
    public static bool IsBattleIntroductionPending => instance != null && instance.battleIntroductionPending;
    public static bool BlocksGameplayInput => BattleLoadingSceneController.IsTransitionVisible || (IsShowing && instance.activeStep != Move) || Time.unscaledTime < releaseInputAt;
    public string ActiveStep => activeStep;
    public TutorialOverlay Overlay => overlay;

    public static TutorialDirector Ensure()
    {
        if (instance == null) new GameObject("TutorialDirector").AddComponent<TutorialDirector>();
        return instance;
    }

    public static bool BlocksGameplayPointer(Vector2 screenPoint) => BlocksGameplayInput ||
        (IsShowing && instance.overlay.ContainsControl(screenPoint));

    public static TutorialProgressData Progress
    {
        get
        {
            var data = PlayerProfileService.Instance.Data;
            return data.tutorial ?? (data.tutorial = new TutorialProgressData());
        }
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.activeSceneChanged += OnSceneChanged;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        EventBus.Subscribe<HeroLevelUpEvent>(OnHeroUpgraded, owner: this);
        EventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied, owner: this);
    }

    void OnDisable() { Cancel(); }
    void Update()
    {
        // Independent watchdog also releases the overlay if a flow coroutine exits unexpectedly.
        if (IsShowing && Time.unscaledTime >= visibleDeadline)
        {
            if (!string.IsNullOrEmpty(activeStep)) Record(activeStep, true);
            Cancel();
        }
    }
    void OnDestroy()
    {
        Cancel();
        SceneManager.activeSceneChanged -= OnSceneChanged;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        EventBus.Unsubscribe<HeroLevelUpEvent>(OnHeroUpgraded);
        EventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
        if (instance == this) instance = null;
    }
    void OnSceneChanged(Scene previous, Scene next) { Cancel(); }
    void OnSceneUnloaded(Scene scene) { Cancel(); }
    void OnApplicationPause(bool paused) { if (paused) Cancel(); }
    void OnPlayerDied(PlayerDiedEvent _) { Cancel(); }
    void OnHeroUpgraded(HeroLevelUpEvent _)
    {
        heroUpgraded = true;
        // A user may upgrade through an alternate entry point before the guide reaches that button.
        if (!Progress.skipped && Progress.battleSettled) Record(UpgradeHero, false);
    }

    public void EnterHome(Button startButton, HomeTabBar tabs)
    {
        if (Progress.skipped) return;
        StartFlow(HomeFlow(startButton, tabs));
    }

    public void EnterBattle(PlayerController player)
    {
        if (Progress.skipped || player == null) return;
        StartFlow(BattleFlow(player));
        battleIntroductionPending = flow != null && (!Progress.HasHandled(Move) || !Progress.HasHandled(AutoAttack));
    }

    public void EnterCards(CardSelectionPanel panel)
    {
        if (panel == null || Progress.skipped) return;
        bool explainSelection = !Progress.HasHandled(SelectSkill);
        bool explainBond = !Progress.HasHandled(BondedPassive) && panel.TutorialBondedPassiveTarget() != null;
        if (!explainSelection && !explainBond) return;
        cardApplied = false;
        StartFlow(CardsFlow(panel));
    }

    public void NotifyBattleStarted()
    {
        if (!Progress.skipped) Record(StartBattle, false);
        Cancel();
    }

    public void NotifyCardApplied()
    {
        cardApplied = true;
        // Fallback for any path that applies a card without a prior panel confirmation; the
        // normal flow records "三选一" in NotifyCardSelectionConfirmed at confirm time.
        if (!Progress.skipped && !Progress.HasHandled(SelectSkill)) Record(SelectSkill, false);
        if (activeStep == BondedPassive)
        {
            // Fallback; the bond step's "picked a card" is normally handled by NotifyCardSelectionConfirmed.
            if (!Progress.skipped) Record(BondedPassive, false);
            Cancel();
        }
        else if (activeStep == SelectSkill) Cancel();
    }

    public void NotifyCardSelectionConfirmed(CardSelectionPanel panel)
    {
        if (panel == null) return;
        if (panel == bondedExplanationPanel)
        {
            bondedExplanationPanel = null;
            // Confirmation disables the card targets before the animation applies the skill.
            // Remember the explanation independently of the overlay's current visibility.
            if (!Progress.skipped) Record(BondedPassive, false);
            if (activeStep == BondedPassive) Cancel();
        }
        else if (activeStep == SelectSkill)
        {
            // "三选一" is taught the moment a card is confirmed. Record here, not in
            // NotifyCardApplied: confirming disables the card buttons, so Step() breaks on
            // TargetsAvailable and clears activeStep before the delayed apply callback runs.
            if (!Progress.skipped) Record(SelectSkill, false);
            Cancel();
        }
    }

    public void NotifyBattleResult()
    {
        Cancel();
        if (Progress.skipped || Progress.battleSettled) return;
        Progress.battleSettled = true;
        Save();
    }

    public void SkipAll()
    {
        Progress.skipped = true;
        Cancel();
        Save();
    }

    public void Continue() { continued = true; }

    public void Cancel()
    {
        if (flow != null) StopCoroutine(flow);
        flow = null;
        battleIntroductionPending = false;
        Hide();
    }

    void StartFlow(IEnumerator next)
    {
        Cancel();
        if (isActiveAndEnabled) flow = StartCoroutine(next);
    }

    IEnumerator HomeFlow(Button start, HomeTabBar tabs)
    {
        yield return new WaitForSecondsRealtime(.5f);
        if (!Progress.battleSettled)
        {
            yield return Step(StartBattle, "开始第一次挑战", "点击「开始游戏」，进入关卡。\n移动躲避敌人，攻击会自动进行。", 1,
                () => Rects(start), () => false, () => HomeReady(), false, false);
            yield break;
        }
        if (Progress.HasHandled(UpgradeHero)) yield break;

        if (tabs == null || tabs.GetTabButton("character") == null)
            GameLog.Warning("[Tutorial] HomeFlow：未找到「character」页签按钮（tabs 或按钮为空），角色/升级引导将在超时后跳过。");

        yield return Step(CharacterTab, "让角色变得更强", "战斗告一段落，去「角色」页看看。\n使用金币升级，可以提升角色属性。", 5,
            () => Rects(tabs != null ? tabs.GetTabButton("character") : null),
            () => tabs != null && tabs.ActiveTabId == "character", () => HomeReady(), false, false);
        if (Progress.skipped || tabs == null || tabs.ActiveTabId != "character") yield break;

        var character = FindObjectOfType<CharacterSelectionPanel>();
        if (character == null) { Record(UpgradeHero, true); yield break; }
        character.ShowTutorialStats();
        yield return null;
        var upgrade = character.TutorialUpgradeButton;
        heroUpgraded = false;
        if (upgrade != null && upgrade.gameObject.activeInHierarchy && upgrade.IsInteractable())
        {
            yield return Step(UpgradeHero, "升级一次角色", "点击「升级」，消耗金币提升属性。\n升级成功后，这次入门指引就完成啦！", 5,
                () => Rects(upgrade), () => heroUpgraded,
                () => HomeReady() && character != null && character.isActiveAndEnabled && tabs.ActiveTabId == "character", false, false);
        }
        else
        {
            yield return Step(UpgradeHero, "升级，让下一局更轻松", "金币不足或等级已达上限时，可以先继续挑战。\n以后回到「角色 → 基础属性」即可升级。", 5,
                null, () => continued, () => HomeReady() && character != null && character.isActiveAndEnabled,
                false, false, "知道了", true);
        }
    }

    IEnumerator BattleFlow(PlayerController player)
    {
        yield return new WaitForSecondsRealtime(.6f);
        float distance = 0;
        Vector3 last = player != null ? player.transform.position : Vector3.zero;
        yield return Step(Move, "拖动屏幕，移动角色", "按住空白区域并拖动，试着移动一小段。\n边走边躲，别让敌人围住你。", 2,
            null, () =>
            {
                if (player == null) return false;
                var now = player.transform.position;
                float moved = Vector2.Distance(last, now);
                if (!player.IsArenaEntryLocked && player.HasMovementInput && moved < 1f) distance += moved;
                last = now;
                return distance >= .8f;
            }, () => BattleReady(player), false, true, movementSubject: player.transform);

        if (Progress.skipped || player == null) { battleIntroductionPending = false; yield break; }
        yield return Step(AutoAttack, "攻击会自动进行", "角色会自动释放已装备的技能。\n你只需移动躲避、拾取能量，再选择技能变强。", 3,
            null, () => continued, () => BattleReady(player), true, false, "继续战斗");
        battleIntroductionPending = false;
    }

    IEnumerator CardsFlow(CardSelectionPanel panel)
    {
        yield return new WaitForSecondsRealtime(.4f);
        // 顺序敏感：先「三选一」再「羁绊」。羁绊步依赖三选一已教过，颠倒会让羁绊提示盖过首次选卡教学。
        if (Progress.skipped || cardApplied || panel == null || !panel.isActiveAndEnabled ||
            (panel.panelRoot != null && !panel.panelRoot.activeInHierarchy)) yield break;
        yield return Step(SelectSkill, "选择一个技能", "三选一：点击任意一张技能卡。\n可以获得新技能，或强化已有技能。", 4,
            () => panel != null ? panel.TutorialCardTargets() : Array.Empty<RectTransform>(),
            () => cardApplied,
            () => CardsReady(panel), false, false,
            pointerTarget: () => panel != null ? panel.TutorialPointerTarget() : null);

        if (Progress.skipped || cardApplied || panel == null || !panel.isActiveAndEnabled ||
            (panel.panelRoot != null && !panel.panelRoot.activeInHierarchy)) yield break;
        if (panel != null && panel.TutorialBondedPassiveTarget() != null)
        {
            yield return Step(BondedPassive, "羁绊被动，解锁突破", "获得对应的羁绊被动，才能突破技能。\n获得后，继续升级关联主动技能即可突破。", 0,
                () => panel != null ? panel.TutorialCardTargets() : Array.Empty<RectTransform>(),
                () => continued || cardApplied,
                () => CardsReady(panel) && panel.TutorialBondedPassiveTarget() != null,
                false, false, "知道了", pointerTarget: () => panel != null ? panel.TutorialBondedPassiveTarget() : null,
                onShown: () => bondedExplanationPanel = panel);
        }
    }

    bool CardsReady(CardSelectionPanel panel) => panel != null && panel.isActiveAndEnabled &&
        (panel.panelRoot == null || panel.panelRoot.activeInHierarchy) &&
        (UIManager.Instance == null || (UIManager.Instance.CanPresentTutorial &&
        (UIManager.Instance.Top == null || UIManager.Instance.Top == panel)));

    bool HomeReady() => SceneManager.GetActiveScene().name == "Home" && UIManager.Instance != null &&
        UIManager.Instance.CanPresentTutorial && UIManager.Instance.Top == null &&
        (RewardRevealQueue.Instance == null || !RewardRevealQueue.Instance.IsShowing);

    bool BattleReady(PlayerController player) => !BattleLoadingSceneController.IsTransitionVisible && player != null && player.gameObject.activeInHierarchy && !player.IsArenaEntryLocked &&
        UIManager.Instance != null && UIManager.Instance.CanPresentTutorial && UIManager.Instance.Top == null;

    IEnumerator Step(string id, string title, string message, int stage, Func<RectTransform[]> targets,
        Func<bool> completed, Func<bool> context, bool pause, bool movement, string action = null, bool skipOnContinue = false,
        Func<RectTransform> pointerTarget = null, Transform movementSubject = null, Action onShown = null)
    {
        if (Progress.HasHandled(id)) yield break;
        continued = false;
        float waitingSince = Time.unscaledTime;
        RectTransform[] resolved = null;
        while (true)
        {
            if (Progress.skipped) yield break;
            if (completed()) { Record(id, skipOnContinue); yield break; }
            resolved = targets != null ? targets() : null;
            if (context() && (targets == null || TutorialOverlay.TargetsAvailable(resolved))) break;
            if (Time.unscaledTime - waitingSince >= TargetWaitSeconds)
            {
                Record(id, true);
                yield break;
            }
            yield return null;
        }

        if (overlay == null) overlay = TutorialOverlay.Create(transform, SkipAll, Continue);
        continued = false;
        activeStep = id;
        if (pause)
        {
            var ui = UIManager.Instance;
            if (ui == null) { activeStep = null; Record(id, true); yield break; }
            pauseLock = ui.AcquirePauseLock();
        }
        overlay.Show(title, message, stage, resolved, movement, action,
            pointerTarget != null ? pointerTarget() : null, movementSubject);
        onShown?.Invoke();
        visibleDeadline = Time.unscaledTime + StepTimeoutSeconds;
        float shownAt = Time.unscaledTime;
        while (!Progress.skipped)
        {
            if (completed()) { Record(id, skipOnContinue); break; }
            // Another modal, target destruction, death and scene changes always release the screen.
            if (!context() || (targets != null && !TutorialOverlay.TargetsAvailable(resolved))) break;
            if (Time.unscaledTime - shownAt >= StepTimeoutSeconds) { Record(id, true); break; }
            yield return null;
        }
        Hide();
    }

    void Hide()
    {
        if (overlay != null && overlay.IsVisible)
        {
            overlay.Hide();
            releaseInputAt = Time.unscaledTime + .15f;
        }
        activeStep = null;
        pauseLock?.Dispose();
        pauseLock = null;
    }

    static RectTransform[] Rects(Button button) => button != null && button.transform is RectTransform rect
        ? new[] { rect } : Array.Empty<RectTransform>();

    static void Record(string id, bool skip)
    {
        if (Progress.Record(id, skip)) Save();
    }
    static void Save() { PlayerProfileService.Instance.MarkDirtyAndSave(); }
}
