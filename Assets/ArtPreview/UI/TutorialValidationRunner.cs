#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Real Home/Game integration with the shared in-memory player fixture.</summary>
public sealed class TutorialValidationRunner : UIValidationRunnerBase
{
    protected override string OutputFolder => "Tutorial";
    protected override string ValidationNote => "Actual Home/Game scenes, in-memory profile, real movement/card/upgrade handlers. Editor screenshots and simulated capsule bounds; no WeChat device validation.";
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    bool previousBackground;
    TutorialDirector guide;
    readonly Dictionary<string, int?> unlockPrefs = new Dictionary<string, int?>();
    GameObject unlockValidationHost;
    CardPoolProgression unlockValidationProgression;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        previousBackground = Application.runInBackground;
        Application.runInBackground = true;
    }

    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
    static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);

    protected override IEnumerator RunChecks()
    {
        changeResolution(1080, 1920);
        var legacy = new PlayerSaveData { version = 5, tutorial = null,
            levels = new[] { new LevelProgressEntry { levelId = 101, cleared = true } } };
        legacy.MigrateToLatest();
        Record("Existing cleared accounts are not forced into onboarding", legacy.version == 6 && legacy.tutorial.skipped);
        var newAccount = new PlayerSaveData { version = 5, tutorial = null };
        newAccount.MigrateToLatest();
        Record("Unfinished legacy accounts remain eligible", !newAccount.tutorial.skipped);
        newAccount.tutorial.Record("done", false); newAccount.tutorial.Record("done", false);
        newAccount.tutorial.Record("opted_out", true);
        var roundtrip = JsonUtility.FromJson<PlayerSaveData>(JsonUtility.ToJson(newAccount));
        Record("Progress roundtrip distinguishes completion and opt-out and is idempotent",
            roundtrip.tutorial.completed.Length == 1 && roundtrip.tutorial.HasCompleted("done") &&
            roundtrip.tutorial.HasHandled("opted_out") && !roundtrip.tutorial.HasCompleted("opted_out"));

        fixture.SetProgress(0);
        guide = TutorialDirector.Ensure();
        ResetProgress();
        var start = Field<Button>(hub, "startGameButton");
        guide.EnterHome(start, tabs);
        yield return WaitForStep(TutorialDirector.StartBattle);
        yield return null; Canvas.ForceUpdateCanvases();
        Record("Home automatically presents a skippable start guide", guide.ActiveStep == TutorialDirector.StartBattle && TutorialDirector.IsShowing);
        Record("Start aperture passes raycasts to the actual button", Hits(start.transform, ScreenPoint((RectTransform)start.transform)));
        Record("Dialogue blocks raw gameplay input", TutorialDirector.BlocksGameplayInput);
        Record("Skip button receives raycasts above the mask", Hits(guide.Overlay.SkipButtonRect, ScreenPoint(guide.Overlay.SkipButtonRect)));
        yield return Capture("01-home-1080x1920");
        foreach (var size in new[] { new Vector2Int(1080, 1440), new Vector2Int(1080, 2400), new Vector2Int(360, 640) })
        {
            changeResolution(size.x, size.y); yield return new WaitForSecondsRealtime(.2f); Canvas.ForceUpdateCanvases(); yield return null;
            Record("Guide controls stay on screen " + size, Inside(guide.Overlay.Dialog) && Inside(guide.Overlay.SkipButtonRect));
            yield return Capture("home-" + size.x + "x" + size.y);
        }
        changeResolution(1080, 1920); yield return null;
        PlatformMenuExclusion.EditorMenuRectOverride = new Rect(.73f, .035f, .24f, .035f);
        yield return null; yield return null;
        Vector2 skipTop = ScreenPoint(guide.Overlay.SkipButtonRect) + new Vector2(0, guide.Overlay.SkipButtonRect.rect.height * guide.Overlay.GetComponent<Canvas>().scaleFactor * .5f);
        Record("Skip stays below the WeChat capsule", skipTop.y < Screen.height * .93f);
        PlatformMenuExclusion.EditorMenuRectOverride = null;

        guide.Overlay.SkipButtonRect.GetComponent<Button>().onClick.Invoke();
        yield return new WaitForSecondsRealtime(.2f);
        Record("Skipping immediately releases input without faking completion", !TutorialDirector.IsShowing &&
            !TutorialDirector.BlocksGameplayInput && TutorialDirector.Progress.skipped && !TutorialDirector.Progress.HasCompleted(TutorialDirector.StartBattle));
        var profile = PlayerProfileService.Instance;
        Set(profile, "_loaded", false); profile.LoadOrCreate();
        guide.EnterHome(start, tabs); yield return new WaitForSecondsRealtime(.7f);
        Record("Skip survives save reload and suppresses replay", TutorialDirector.Progress.skipped && !TutorialDirector.IsShowing);

        ResetProgress(); guide.EnterHome(start, tabs); yield return WaitForStep(TutorialDirector.StartBattle);
        UIManager.Instance.ShowConfirm("验证", "其它弹窗优先，指引必须让路。", _ => { });
        yield return null; yield return null;
        Record("Opening another modal releases tutorial without completing it", !TutorialDirector.IsShowing && !TutorialDirector.Progress.HasCompleted(TutorialDirector.StartBattle));
        UIManager.Instance.CloseConfirm(); yield return new WaitForSecondsRealtime(.3f);

        guide.EnterHome(start, tabs); yield return WaitForStep(TutorialDirector.StartBattle);
        Set(guide, "visibleDeadline", Time.unscaledTime - 1);
        yield return null; yield return null;
        Record("Independent timeout releases overlay and records only an opt-out", !TutorialDirector.IsShowing &&
            TutorialDirector.Progress.HasHandled(TutorialDirector.StartBattle) && !TutorialDirector.Progress.HasCompleted(TutorialDirector.StartBattle));
        ResetProgress(); guide.EnterHome(null, tabs);
        yield return new WaitForSecondsRealtime(TutorialDirector.TargetWaitSeconds + .8f);
        Record("Missing target times out without ever blocking the screen", !TutorialDirector.IsShowing && TutorialDirector.Progress.HasHandled(TutorialDirector.StartBattle));

        ResetProgress(); guide.EnterHome(start, tabs); yield return WaitForStep(TutorialDirector.StartBattle);
        RememberUnlockPref("tutorial_seen"); PlayerPrefs.SetInt("tutorial_seen", 0);
        start.onClick.Invoke();
        yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Game" && FindObjectOfType<PlayerController>() != null, 40);
        Record("Real start action saves progress and loads Game", SceneManager.GetActiveScene().name == "Game" && TutorialDirector.Progress.HasCompleted(TutorialDirector.StartBattle));
        foreach (var spawner in FindObjectsOfType<SpawnerWaves>()) { spawner.StopAllCoroutines(); spawner.enabled = false; }
        foreach (var enemy in FindObjectsOfType<EnemyBase>()) Destroy(enemy.gameObject);
        foreach (var outcome in FindObjectsOfType<BattleOutcomeCoordinator>()) { outcome.StopAllCoroutines(); outcome.enabled = false; }
        var player = FindObjectOfType<PlayerController>();
        yield return WaitForStep(TutorialDirector.Move);
        Record("Movement prompt keeps battle running and accepts gameplay input", guide.ActiveStep == TutorialDirector.Move && Time.timeScale > 0 && !TutorialDirector.BlocksGameplayInput);
        Record("First battle has no retired operation alert", PlayerPrefs.GetInt("tutorial_seen", 0) == 0 &&
            !FindObjectsOfType<UiConfirmDialog>().Any(dialog => dialog.titleText != null && dialog.titleText.text == "操作指引"));
        yield return Capture("02-movement-1080x1920");
        CheckMovementTrail(player, "1080x1920");
        foreach (var size in new[] { new Vector2Int(1080, 1440), new Vector2Int(1080, 2400) })
        {
            changeResolution(size.x, size.y); yield return new WaitForSecondsRealtime(.2f);
            CheckMovementTrail(player, size.x + "x" + size.y);
            yield return Capture("02-movement-" + size.x + "x" + size.y);
        }
        changeResolution(1080, 1920); yield return new WaitForSecondsRealtime(.2f);
        var joystick = FindObjectsOfType<DynamicJoystick>(true).FirstOrDefault();
        joystick.useExternalTouchEvents = true; joystick.debugLogs = false;
        Vector2 touch = new Vector2(Screen.width * .35f, Screen.height * .3f);
        joystick.OnTouchDown(touch); joystick.OnTouchMove(touch + Vector2.right * 220);
        yield return WaitUntil(() => TutorialDirector.Progress.HasCompleted(TutorialDirector.Move), 4);
        joystick.OnTouchUp();
        Record("Actual joystick movement completes the movement step", TutorialDirector.Progress.HasCompleted(TutorialDirector.Move));
        yield return WaitForStep(TutorialDirector.AutoAttack);
        Record("Auto attack explanation holds a shared pause lock", guide.ActiveStep == TutorialDirector.AutoAttack && Time.timeScale == 0);
        yield return Capture("03-auto-attack-1080x1920");
        guide.Continue(); yield return new WaitForSecondsRealtime(.2f);
        Record("Continue finishes explanation and restores time", TutorialDirector.Progress.HasCompleted(TutorialDirector.AutoAttack) && Time.timeScale > 0 && !TutorialDirector.IsShowing);
        yield return CheckUnlockQueue(player);

        ResetProgress(TutorialDirector.StartBattle, TutorialDirector.Move);
        guide.EnterBattle(player); yield return WaitForStep(TutorialDirector.AutoAttack);
        var otherPause = UIManager.Instance.AcquirePauseLock();
        guide.SkipAll();
        Record("Skipping guide preserves another owner's pause", Time.timeScale == 0);
        otherPause.Dispose();
        Record("Final pause owner restores previous time", Time.timeScale > 0);
        ResetProgress(TutorialDirector.StartBattle, TutorialDirector.Move);
        guide.EnterBattle(player); yield return WaitForStep(TutorialDirector.AutoAttack);
        guide.enabled = false;
        Record("Disabling controller releases its overlay and pause", !TutorialDirector.IsShowing && Time.timeScale > 0);
        guide.enabled = true;

        ResetProgress(TutorialDirector.StartBattle, TutorialDirector.Move, TutorialDirector.AutoAttack);
        var cardSystem = FindObjectOfType<CardSelectionSystem>();
        cardSystem.BeginSelectionFromManager();
        yield return WaitForStep(TutorialDirector.SelectSkill);
        var panel = UIManager.Instance.Top as CardSelectionPanel;
        Record("First selection presents guide over real skill cards", panel != null && guide.ActiveStep == TutorialDirector.SelectSkill && guide.Overlay.Targets.Length > 0);
        Record("Skill card aperture receives input", panel != null && Hits(panel.cardSlots[0].transform, ScreenPoint((RectTransform)panel.cardSlots[0].transform)));
        yield return Capture("04-skills-1080x1920");
        yield return CheckSkillPointers(panel, cardSystem);
        yield return CheckBondedGuide(panel, cardSystem);
        panel.cardSlots[0].clickButton.onClick.Invoke();
        yield return new WaitForSecondsRealtime(.8f);
        Record("Successful real card handler completes guide and resumes battle", TutorialDirector.Progress.HasCompleted(TutorialDirector.SelectSkill) && !TutorialDirector.IsShowing && Time.timeScale > 0);
        ResetProgress(TutorialDirector.StartBattle, TutorialDirector.Move, TutorialDirector.AutoAttack);
        cardSystem.BeginSelectionFromManager(); yield return WaitForStep(TutorialDirector.SelectSkill);
        guide.SkipAll();
        panel = UIManager.Instance.Top as CardSelectionPanel;
        Record("Skipping card guide leaves selection open and paused", panel != null && panel.isActiveAndEnabled && Time.timeScale == 0);
        panel.cardSlots[0].clickButton.onClick.Invoke(); yield return new WaitForSecondsRealtime(.8f);
        Record("Normal selection remains usable after skipping", Time.timeScale > 0 && UIManager.Instance.Top == null);

        ResetProgress(TutorialDirector.StartBattle, TutorialDirector.Move, TutorialDirector.AutoAttack, TutorialDirector.SelectSkill);
        var result = UIManager.Instance.Open<GameResultPanel>(new GameResultViewModel { victory = true, killCount = 10 });
        yield return new WaitForSecondsRealtime(.4f);
        Record("Actual settlement arms only the later character guide", TutorialDirector.Progress.battleSettled && !TutorialDirector.IsShowing);
        result.GetType().GetMethod("ReturnToHome", Private).Invoke(result, null);
        yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Home" && FindObjectOfType<HomeHubController>() != null, 20);
        yield return WaitForStep(TutorialDirector.CharacterTab);
        tabs = FindObjectOfType<HomeTabBar>(); hub = FindObjectOfType<HomeHubController>();
        yield return Capture("05-character-tab-1080x1920");
        tabs.GetTabButton("character").onClick.Invoke();
        yield return WaitForStep(TutorialDirector.UpgradeHero);
        var character = FindObjectOfType<CharacterSelectionPanel>();
        Record("Character tab leads to the real upgrade button", character != null && guide.ActiveStep == TutorialDirector.UpgradeHero && !character.IsSkillView);
        yield return Capture("06-upgrade-1080x1920");
        int goldBefore = PlayerProfileService.Instance.Gold;
        int levelBefore = PlayerProfileService.Instance.GetHeroLevel(character.SelectedCharacterId);
        character.TutorialUpgradeButton.onClick.Invoke(); yield return new WaitForSecondsRealtime(.5f);
        Record("Successful upgrade spends gold, increases level and completes onboarding",
            PlayerProfileService.Instance.GetHeroLevel(character.SelectedCharacterId) == levelBefore + 1 &&
            PlayerProfileService.Instance.Gold < goldBefore && TutorialDirector.Progress.HasCompleted(TutorialDirector.UpgradeHero) && !TutorialDirector.IsShowing);
        guide.EnterHome(Field<Button>(hub, "startGameButton"), tabs); yield return new WaitForSecondsRealtime(.7f);
        Record("Completed onboarding does not repeat", !TutorialDirector.IsShowing);

        ResetProgress(TutorialDirector.StartBattle, TutorialDirector.Move, TutorialDirector.AutoAttack, TutorialDirector.SelectSkill, TutorialDirector.CharacterTab);
        TutorialDirector.Progress.battleSettled = true;
        PlayerProfileService.Instance.SpendGold(PlayerProfileService.Instance.Gold);
        yield return new WaitForSecondsRealtime(1);
        guide.EnterHome(Field<Button>(hub, "startGameButton"), tabs); yield return WaitForStep(TutorialDirector.UpgradeHero);
        Record("Insufficient gold shows an explanation with a continue action", guide.ActiveStep == TutorialDirector.UpgradeHero && guide.Overlay.Targets == null);
        yield return Capture("07-insufficient-gold-1080x1920");
        guide.Continue(); yield return new WaitForSecondsRealtime(.2f);
        Record("Insufficient gold never fakes a successful upgrade", !TutorialDirector.IsShowing &&
            TutorialDirector.Progress.HasHandled(TutorialDirector.UpgradeHero) && !TutorialDirector.Progress.HasCompleted(TutorialDirector.UpgradeHero));
        guide.Cancel();
        changeResolution(1080, 1920);
    }

    void ResetProgress(params string[] completed)
    {
        if (guide != null) guide.Cancel();
        PlayerProfileService.Instance.Data.tutorial = new TutorialProgressData { completed = completed };
        PlayerProfileService.Instance.MarkDirtyAndSave();
    }

    void RememberUnlockPref(string key)
    {
        if (!unlockPrefs.ContainsKey(key)) unlockPrefs[key] = PlayerPrefs.HasKey(key) ? (int?)PlayerPrefs.GetInt(key) : null;
    }

    IEnumerator CheckUnlockQueue(PlayerController player)
    {
        const SkillId skill = SkillId.LineBeam;
        const int level = 102;
        string seenKey = "unlock_skill_seen_" + level + "_" + (int)skill;
        string globalKey = "global_skill_unlock_" + (int)skill;
        RememberUnlockPref(seenKey); RememberUnlockPref(globalKey);
        int previousChapter = SelectedLevelContext.ChapterId, previousLevel = SelectedLevelContext.LevelId;
        SelectedLevelContext.Set(1, level); PlayerPrefs.SetInt(seenKey, 0);
        unlockValidationProgression = ScriptableObject.CreateInstance<CardPoolProgression>();
        unlockValidationProgression.unlockConditions = new[] { new CardPoolProgression.UnlockCondition { skill = skill, unlockAtLevel = level } };
        var skills = FindObjectOfType<PlayerSkills>();
        unlockValidationHost = new GameObject("TutorialUnlockQueueValidation"); unlockValidationHost.SetActive(false);
        var unlock = unlockValidationHost.AddComponent<GameUnlockSkill>();
        unlock.progression = unlockValidationProgression; unlock.skillCatalog = skills.skillCatalog;
        ResetProgress(TutorialDirector.StartBattle, TutorialDirector.Move, TutorialDirector.SelectSkill);
        guide.EnterBattle(player);
        unlockValidationHost.SetActive(true);
        yield return new WaitForSecondsRealtime(.2f);
        Record("Unlock queue waits even before the battle explanation becomes visible", TutorialDirector.IsBattleIntroductionPending &&
            !GameUnlockSkill.CanPresentUnlock() && !UIManager.Instance.IsConfirmationActive(UIManager.Instance.ConfirmationVersion));
        yield return WaitForStep(TutorialDirector.AutoAttack);
        Record("Skill unlock cannot cover the new battle explanation", guide.ActiveStep == TutorialDirector.AutoAttack &&
            !UIManager.Instance.IsConfirmationActive(UIManager.Instance.ConfirmationVersion));
        guide.Continue();
        yield return WaitUntil(() => UIManager.Instance.IsConfirmationActive(UIManager.Instance.ConfirmationVersion), 4);
        var dialog = FindObjectOfType<UiConfirmDialog>();
        Record("Queued skill introduction opens after tutorial completes", dialog != null && dialog.titleText.text == "技能解锁" &&
            !TutorialDirector.IsShowing && !TutorialDirector.IsBattleIntroductionPending && Time.timeScale == 0);
        yield return Capture("09-skill-unlock-after-tutorial-1080x1920");

        int originalVersion = UIManager.Instance.ConfirmationVersion;
        var system = FindObjectOfType<CardSelectionSystem>();
        var deck = RoguelikeCardManager.Instance != null ? RoguelikeCardManager.Instance.cardDeck : system.deck;
        var payload = new CardSelectionOpenPayload
        {
            Cards = new List<CardDeck.DrawResult> { new CardDeck.DrawResult { skillId = skill, skillDef = skills.skillCatalog.Get(skill),
                template = deck.newSkillTemplate, currentLevel = 0, targetLevel = 1 } },
            OnCardSelected = _ => { }, FreeRefreshCount = 0, AdRefreshCount = 0
        };
        var cards = UIManager.Instance.Open<CardSelectionPanel>(payload, new UiOpenOptions { PauseTime = true, UseUnscaledTime = true });
        yield return new WaitForSecondsRealtime(.4f);
        Record("Card selection safely replaces skill alert without marking it read", UIManager.Instance.Top == cards && Time.timeScale == 0 &&
            !UIManager.Instance.IsConfirmationActive(originalVersion) && PlayerPrefs.GetInt(seenKey, 0) == 0 && !GameUnlockSkill.CanPresentUnlock());
        UIManager.Instance.CloseTop();
        yield return WaitUntil(() => UIManager.Instance.IsConfirmationActive(UIManager.Instance.ConfirmationVersion), 4);
        dialog = FindObjectOfType<UiConfirmDialog>();
        Record("Interrupted skill alert retries after selection closes", dialog != null && dialog.titleText.text == "技能解锁" &&
            UIManager.Instance.ConfirmationVersion > originalVersion && PlayerPrefs.GetInt(seenKey, 0) == 0);
        dialog.okButton.onClick.Invoke(); yield return new WaitForSecondsRealtime(.15f);
        Record("Acknowledged unlock alone marks read and releases its pause", PlayerPrefs.GetInt(seenKey, 0) == 1 &&
            PlayerPrefs.GetInt(globalKey, 0) >= level && Time.timeScale > 0 && !UIManager.Instance.IsConfirmationActive(UIManager.Instance.ConfirmationVersion));

        Destroy(unlockValidationHost); yield return null;
        PlayerPrefs.SetInt(seenKey, 0);
        unlockValidationHost = new GameObject("TutorialUnlockCleanupValidation"); unlockValidationHost.SetActive(false);
        unlock = unlockValidationHost.AddComponent<GameUnlockSkill>();
        unlock.progression = unlockValidationProgression; unlock.skillCatalog = skills.skillCatalog;
        unlockValidationHost.SetActive(true);
        yield return WaitUntil(() => UIManager.Instance.IsConfirmationActive(UIManager.Instance.ConfirmationVersion), 4);
        var otherPause = UIManager.Instance.AcquirePauseLock();
        unlock.enabled = false;
        Record("Disabling unlock owner cleans its alert and preserves another pause", !UIManager.Instance.IsConfirmationActive(UIManager.Instance.ConfirmationVersion) &&
            Time.timeScale == 0 && PlayerPrefs.GetInt(seenKey, 0) == 0);
        otherPause.Dispose();
        Record("Last pause owner resumes battle after unlock interruption", Time.timeScale > 0);
        Destroy(unlockValidationHost); Destroy(unlockValidationProgression);
        unlockValidationHost = null; unlockValidationProgression = null;
        SelectedLevelContext.Set(previousChapter, previousLevel);
    }

    void CheckMovementTrail(PlayerController player, string size)
    {
        var trail = guide.Overlay.MovementTrail;
        var body = player.GetComponentsInChildren<SpriteRenderer>().First(r => r.enabled && r.sprite != null);
        float feet = Camera.main.WorldToScreenPoint(new Vector3(body.bounds.center.x, body.bounds.min.y, body.bounds.center.z)).y;
        float trailTop = RectTransformUtility.WorldToScreenPoint(null, trail.TransformPoint(new Vector3(0, trail.rect.yMax))).y;
        Record("Figure-eight trail stays below the character " + size, trailTop < feet - 16 && Inside(trail));
        Record("Movement trail does not intercept gestures " + size, !trail.GetComponent<TutorialGestureGraphic>().raycastTarget);
    }

    IEnumerator CheckSkillPointers(CardSelectionPanel panel, CardSelectionSystem system)
    {
        var original = Field<List<CardDeck.DrawResult>>(system, "_currentCards");
        var selected = Field<Action<int>>(panel, "_onCardSelected");
        var refreshed = Field<Action>(panel, "_onRefreshRequested");
        var adRefreshed = Field<Action>(panel, "_onAdRefreshRequested");
        int free = Field<int>(panel, "_freeRefreshCount"), ad = Field<int>(panel, "_adRefreshCount");
        var deck = RoguelikeCardManager.Instance != null ? RoguelikeCardManager.Instance.cardDeck : system.deck;
        var skills = FindObjectOfType<PlayerSkills>();
        var sets = new[]
        {
            new[] { SkillId.PassiveAttack, SkillId.PassiveCritRate, SkillId.HomingMissileBasic },
            new[] { SkillId.LineBeam, SkillId.PassiveMaxHp, SkillId.PassiveMoveSpeed },
            new[] { SkillId.PassiveAttack, SkillId.PassiveCritRate, SkillId.PassiveMaxHp }
        };
        int[] expected = { 2, 0, 1 };
        string[] names = { "active-right", "active-left", "all-passive-middle" };
        for (int i = 0; i < sets.Length; i++)
        {
            var cards = sets[i].Select(id => new CardDeck.DrawResult
            {
                skillId = id, skillDef = skills.skillCatalog.Get(id), currentLevel = 0, targetLevel = 1,
                template = id.IsPassive() ? deck.passiveTemplate : deck.newSkillTemplate
            }).ToList();
            panel.Show(cards, selected, refreshed, adRefreshed, free, ad);
            yield return WaitForStep(TutorialDirector.SelectSkill);
            yield return null; Canvas.ForceUpdateCanvases();
            Record("Skill pointer chooses " + names[i], guide.Overlay.PointerTarget == panel.cardSlots[expected[i]].transform);
            Record("All skill cards remain clickable " + names[i], panel.cardSlots.All(slot => Hits(slot.transform, ScreenPoint((RectTransform)slot.transform))));
            yield return Capture("04-skills-" + names[i] + "-1080x1920");
        }
        panel.Show(original, selected, refreshed, adRefreshed, free, ad);
        yield return WaitForStep(TutorialDirector.SelectSkill);
    }

    IEnumerator CheckBondedGuide(CardSelectionPanel panel, CardSelectionSystem system)
    {
        var original = Field<List<CardDeck.DrawResult>>(system, "_currentCards");
        var selected = Field<Action<int>>(panel, "_onCardSelected");
        var refreshed = Field<Action>(panel, "_onRefreshRequested");
        var adRefreshed = Field<Action>(panel, "_onAdRefreshRequested");
        int free = Field<int>(panel, "_freeRefreshCount"), ad = Field<int>(panel, "_adRefreshCount");
        var deck = RoguelikeCardManager.Instance != null ? RoguelikeCardManager.Instance.cardDeck : system.deck;
        var playerSkills = FindObjectOfType<PlayerSkills>();
        var catalog = playerSkills.skillCatalog;
        Func<SkillId[], List<CardDeck.DrawResult>> cards = ids => ids.Select(id => new CardDeck.DrawResult
        {
            skillId = id, skillDef = catalog.Get(id), currentLevel = 0, targetLevel = 1,
            template = id.IsPassive() ? deck.passiveTemplate : deck.newSkillTemplate
        }).ToList();
        var equipped = new List<SkillId>(); playerSkills.GetEquippedSkillIdsOrdered(equipped);
        var linked = equipped.Select(id => catalog.Get(id)).First(def => def != null && def.bondedPassiveId != SkillId.None);
        var bonded = cards(new[] { linked.bondedPassiveId, SkillId.PassiveAttack, SkillId.HomingMissileBasic });
        var activeOnly = cards(new[] { SkillId.LineBeam, SkillId.HomingMissileBasic, SkillId.FieldGenerator });

        ResetProgress(TutorialDirector.StartBattle, TutorialDirector.Move, TutorialDirector.AutoAttack);
        panel.Show(bonded, selected, refreshed, adRefreshed, free, ad);
        yield return WaitForStep(TutorialDirector.BondedPassive);
        yield return null; Canvas.ForceUpdateCanvases();
        var target = panel.TutorialBondedPassiveTarget();
        Record("Bond explanation precedes first selection and points to the visible bonded passive",
            guide.ActiveStep == TutorialDirector.BondedPassive && target != null && guide.Overlay.PointerTarget == target &&
            target.GetComponent<CardView>().BoundSkillId.IsPassive() && target.GetComponent<CardView>().BondedRowCount > 0 &&
            !TutorialDirector.Progress.HasCompleted(TutorialDirector.SelectSkill));
        Record("Bond explanation keeps all three card choices clickable", panel.cardSlots.All(slot => Hits(slot.transform, ScreenPoint((RectTransform)slot.transform))));
        var text = string.Join(" ", guide.Overlay.GetComponentsInChildren<TMPro.TMP_Text>().Select(t => t.text));
        Record("Bond explanation specifies matching passive and later active upgrades", text.Contains("对应的羁绊被动") && text.Contains("继续升级关联主动技能") && text.Contains("知道了"));
        yield return Capture("08-bonded-passive-1080x1920");
        guide.Continue(); yield return WaitForStep(TutorialDirector.SelectSkill);
        Record("Acknowledging bond explanation resumes basic selection without choosing a card",
            TutorialDirector.Progress.HasCompleted(TutorialDirector.BondedPassive) && guide.ActiveStep == TutorialDirector.SelectSkill &&
            panel.isActiveAndEnabled && Time.timeScale == 0 && !TutorialDirector.Progress.HasCompleted(TutorialDirector.SelectSkill));
        var saved = JsonUtility.FromJson<TutorialProgressData>(JsonUtility.ToJson(TutorialDirector.Progress));
        Record("Bond acknowledgement survives serialization", saved.HasCompleted(TutorialDirector.BondedPassive));
        TutorialDirector.Progress.Record(TutorialDirector.SelectSkill, false); PlayerProfileService.Instance.MarkDirtyAndSave();
        guide.Cancel(); panel.Show(bonded, selected, refreshed, adRefreshed, free, ad);
        yield return new WaitForSecondsRealtime(.65f);
        Record("Acknowledged bond explanation does not repeat", !TutorialDirector.IsShowing);

        ResetProgress(TutorialDirector.StartBattle, TutorialDirector.Move, TutorialDirector.AutoAttack, TutorialDirector.SelectSkill);
        panel.Show(activeOnly, selected, refreshed, adRefreshed, free, ad);
        yield return new WaitForSecondsRealtime(.65f);
        Record("Unrelated cards do not consume the pending bond explanation", !TutorialDirector.IsShowing && !TutorialDirector.Progress.HasHandled(TutorialDirector.BondedPassive));
        panel.Show(bonded, selected, refreshed, adRefreshed, free, ad);
        yield return WaitForStep(TutorialDirector.BondedPassive);
        Record("Bond explanation still triggers after basic onboarding is complete", guide.ActiveStep == TutorialDirector.BondedPassive);
        panel.Show(activeOnly, selected, refreshed, adRefreshed, free, ad);
        yield return new WaitForSecondsRealtime(.3f);
        Record("Refreshing away bonded cards releases the hint without completing it", !TutorialDirector.IsShowing && !TutorialDirector.Progress.HasHandled(TutorialDirector.BondedPassive));
        panel.Show(bonded, selected, refreshed, adRefreshed, free, ad);
        yield return WaitForStep(TutorialDirector.BondedPassive);
        guide.SkipAll(); yield return null;
        Record("Skipping bond explanation preserves paused selection and records opt-out", !TutorialDirector.IsShowing &&
            TutorialDirector.Progress.skipped && !TutorialDirector.Progress.HasCompleted(TutorialDirector.BondedPassive) && panel.isActiveAndEnabled && Time.timeScale == 0);

        foreach (int index in new[] { 2, 0 })
        {
            ResetProgress(TutorialDirector.StartBattle, TutorialDirector.Move, TutorialDirector.AutoAttack, TutorialDirector.SelectSkill);
            Set(system, "_currentCards", bonded);
            panel.Show(bonded, selected, refreshed, adRefreshed, free, ad);
            yield return WaitForStep(TutorialDirector.BondedPassive);
            string choice = index == 2 ? "active skill" : "bonded passive";
            panel.cardSlots[index].clickButton.onClick.Invoke();
            Record("Confirming an " + choice + " saves bond acknowledgement before animation ends",
                panel.IsCardConfirmationPending && TutorialDirector.Progress.HasCompleted(TutorialDirector.BondedPassive));
            yield return new WaitForSecondsRealtime(.8f);
            Record("Selecting an " + choice + " closes the real selection normally", UIManager.Instance.Top == null && Time.timeScale > 0);
            var profile = PlayerProfileService.Instance;
            Set(profile, "_loaded", false); profile.LoadOrCreate();
            Record("Bond acknowledgement from an " + choice + " survives storage reload", TutorialDirector.Progress.HasCompleted(TutorialDirector.BondedPassive));
            system.BeginSelectionFromManager();
            panel = UIManager.Instance.Top as CardSelectionPanel;
            original = Field<List<CardDeck.DrawResult>>(system, "_currentCards");
            panel.Show(bonded, selected, refreshed, adRefreshed, free, ad);
            yield return new WaitForSecondsRealtime(.65f);
            Record("Bond hint does not repeat after choosing an " + choice, !TutorialDirector.IsShowing);
        }

        // Refresh/modal interruption can hide the explanation before a later card is confirmed.
        ResetProgress(TutorialDirector.StartBattle, TutorialDirector.Move, TutorialDirector.AutoAttack, TutorialDirector.SelectSkill);
        panel.Show(bonded, selected, refreshed, adRefreshed, free, ad);
        yield return WaitForStep(TutorialDirector.BondedPassive);
        panel.Show(activeOnly, selected, refreshed, adRefreshed, free, ad);
        yield return new WaitForSecondsRealtime(.65f);
        Record("Refreshing without selecting still leaves the bond step pending", !TutorialDirector.Progress.HasHandled(TutorialDirector.BondedPassive));
        panel.TryBeginCardConfirmation(panel.cardSlots[0]);
        Record("Later confirmation remembers a previously visible bond explanation", TutorialDirector.Progress.HasCompleted(TutorialDirector.BondedPassive));

        Set(system, "_currentCards", original);
        ResetProgress(TutorialDirector.StartBattle, TutorialDirector.Move, TutorialDirector.AutoAttack, TutorialDirector.BondedPassive);
        panel.Show(original, selected, refreshed, adRefreshed, free, ad);
        yield return WaitForStep(TutorialDirector.SelectSkill);
    }

    IEnumerator WaitForStep(string step)
    {
        float until = Time.unscaledTime + 12;
        while ((guide.ActiveStep != step || !TutorialDirector.IsShowing) && Time.unscaledTime < until)
        {
            // General regression checks acknowledge the extra conditional explanation when it appears.
            if (step != TutorialDirector.BondedPassive && guide.ActiveStep == TutorialDirector.BondedPassive && TutorialDirector.IsShowing)
                guide.Continue();
            yield return null;
        }
    }
    static IEnumerator WaitUntil(Func<bool> condition, float seconds)
    {
        float until = Time.unscaledTime + seconds;
        while (!condition() && Time.unscaledTime < until) yield return null;
    }

    static Vector2 ScreenPoint(RectTransform rect)
    {
        var canvas = rect.GetComponentInParent<Canvas>();
        return RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            rect.TransformPoint(rect.rect.center));
    }
    static bool Hits(Transform target, Vector2 screen)
    {
        if (EventSystem.current == null) return false;
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, results);
        return results.Count > 0 && (results[0].gameObject.transform == target || results[0].gameObject.transform.IsChildOf(target));
    }
    static bool Inside(RectTransform rect)
    {
        var points = new Vector3[4]; rect.GetWorldCorners(points);
        return points.All(p => { var s = RectTransformUtility.WorldToScreenPoint(null, p); return s.x >= -1 && s.y >= -1 && s.x <= Screen.width + 1 && s.y <= Screen.height + 1; });
    }

    protected override void OnDestroy()
    {
        if (unlockValidationHost != null) Destroy(unlockValidationHost);
        if (unlockValidationProgression != null) Destroy(unlockValidationProgression);
        foreach (var preference in unlockPrefs)
        {
            if (preference.Value.HasValue) PlayerPrefs.SetInt(preference.Key, preference.Value.Value);
            else PlayerPrefs.DeleteKey(preference.Key);
        }
        PlayerPrefs.Save();
        if (guide != null) guide.Cancel();
        Application.runInBackground = previousBackground;
        PlatformMenuExclusion.EditorMenuRectOverride = null;
        base.OnDestroy();
    }
}
#endif
