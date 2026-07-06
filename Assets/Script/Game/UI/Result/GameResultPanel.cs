using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>单个奖励物品展示信息。</summary>
public sealed class RewardItemEntry
{
    public int itemId;
    public string itemName;
    public string iconPath;
    public int count;
    public int grade;
    public string description;
}

/// <summary>
/// 传入 <see cref="GameResultPanel"/> 的结算数据（由 <see cref="BattleOutcomeCoordinator"/> 组装）。
/// </summary>
public sealed class GameResultViewModel
{
    public bool victory;
    public float battleDurationUnscaled;
    public int killCount;
    /// <summary>通关星级（1~3），仅胜利时有效。</summary>
    public int stars;
    public List<string> unlockedCharacters;
    /// <summary>本次获得的奖励物品列表（含金币）。</summary>
    public List<RewardItemEntry> rewardItems;
    /// <summary>完成的波次数 / 总波次数。</summary>
    public int completedWaves;
    public int totalWaves;
}

/// <summary>
/// 局内结算 UI：胜负图、时长、击杀、技能伤害列表、退出/重开/下一关。
/// </summary>
[DisallowMultipleComponent]
public class GameResultPanel : UIPanelBase
{
    [Header("胜负标题")]
    [SerializeField] private TextMeshProUGUI textBanner;

    [Header("波次统计")]
    [SerializeField] private TextMeshProUGUI textWave;

    [Header("被动技能")]
    [SerializeField] private Transform passiveSkillListParent;
    [SerializeField] private GameObject passiveSkillRowPrefab;

    [Header("技能行预制体（默认空则运行时 Load：见 skillRowPrefabResourcesPath）")]
    [SerializeField] private GameObject skillDamageRowPrefab;
    [SerializeField] private TextMeshProUGUI unlockLabel;
    [SerializeField] private Button unlockGoButton;

    [Header("奖励物品")]
    [Tooltip("ItemCell 预制体（挂 ItemCell 脚本）。")]
    [SerializeField] private GameObject itemCellPrefab;
    [Tooltip("ScrollViewReward 的 Content 节点。")]
    [SerializeField] private Transform scrollViewRewardContent;

    [Header("星级展示")]
    [SerializeField] private TextMeshProUGUI starText;

    [Header("金币展示（可选：汇总行，与 ScrollViewReward 互斥时留空其一）")]
    [SerializeField] private TextMeshProUGUI goldEarnedText;
    [SerializeField] private TextMeshProUGUI goldBalanceText;

    [SerializeField] private string skillRowPrefabResourcesPath = string.Empty;

    private TextMeshProUGUI _textTime;
    private TextMeshProUGUI _textKillNum;
    private TextMeshProUGUI _textLevel;
    private Transform _scrollContent;
    private Button _btnExit;
    private Button _btnAgain;
    private Button _btnNext;

    private GameResultViewModel _vm;
    private bool _openedWithPauseOnly;

    private void Awake()
    {
        if (textBanner == null)
            textBanner = transform.Find("TextBanner")?.GetComponent<TextMeshProUGUI>();
        _textTime = transform.Find("TextTime")?.GetComponent<TextMeshProUGUI>();
        _textKillNum = transform.Find("TextKillNum")?.GetComponent<TextMeshProUGUI>();
        _textLevel = transform.Find("Textlevel")?.GetComponent<TextMeshProUGUI>();
        _scrollContent = transform.Find("Scroll View/Viewport/Content");
        _btnExit = transform.Find("ButtonExit")?.GetComponent<Button>();
        _btnAgain = transform.Find("ButtonAgain")?.GetComponent<Button>();
        _btnNext = transform.Find("ButtonNext")?.GetComponent<Button>();

        EnsureScrollContentLayout();

        if (_btnExit != null) _btnExit.onClick.AddListener(OnExitClicked);
        if (_btnAgain != null) _btnAgain.onClick.AddListener(OnAgainClicked);
        if (_btnNext != null) _btnNext.onClick.AddListener(OnNextClicked);
    }

    private void EnsureRewardContentLayout()
    {
        if (scrollViewRewardContent == null) return;
        var go = scrollViewRewardContent.gameObject;

        // 已有任意 LayoutGroup 就不再强加，尊重 Prefab 上的手动布局
        if (go.GetComponent<HorizontalOrVerticalLayoutGroup>() != null) return;

        var v = go.AddComponent<VerticalLayoutGroup>();
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlHeight = true;
        v.childControlWidth = true;
        v.childForceExpandHeight = false;
        v.childForceExpandWidth = true;
        v.spacing = 6f;
        v.padding = new RectOffset(8, 8, 8, 8);

        if (go.GetComponent<ContentSizeFitter>() == null)
        {
            var f = go.AddComponent<ContentSizeFitter>();
            f.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    private void EnsureScrollContentLayout()
    {
        if (_scrollContent == null) return;
        var go = _scrollContent.gameObject;
        if (go.GetComponent<VerticalLayoutGroup>() == null)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            v.spacing = 6f;
            v.padding = new RectOffset(8, 8, 8, 8);
        }

        if (go.GetComponent<ContentSizeFitter>() == null)
        {
            var f = go.AddComponent<ContentSizeFitter>();
            f.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    public override void OnOpen(object payload)
    {
        // 结算弹出时清空 DDOL 下的局内对象池，避免残留占用与脏状态。
        GameObjectPool.ClearAllPools();

        _vm = payload as GameResultViewModel;
        if (_vm == null)
            _vm = new GameResultViewModel { victory = false, battleDurationUnscaled = 0f, killCount = 0 };

        if (!AppliedPauseLock && Time.timeScale > 0f)
        {
            Time.timeScale = 0f;
            _openedWithPauseOnly = true;
        }

        bool win = _vm.victory;

        // 新解锁角色提示
        bool hasUnlock = _vm.unlockedCharacters != null && _vm.unlockedCharacters.Count > 0;
        if (unlockLabel != null)
        {
            if (hasUnlock)
            {
                var nameTags = new List<string>();
                foreach (var n in _vm.unlockedCharacters)
                    nameTags.Add($"<size=+12><color=#FFD700><b>{n}</b></color></size>");
                string names = string.Join(" ", nameTags);
                unlockLabel.text = $"新角色解锁：{names}";
                unlockLabel.gameObject.SetActive(true);
            }
            else
            {
                unlockLabel.gameObject.SetActive(false);
            }
        }
        if (unlockGoButton != null)
        {
            if (hasUnlock)
            {
                unlockGoButton.gameObject.SetActive(true);
                unlockGoButton.onClick.RemoveAllListeners();
                unlockGoButton.onClick.AddListener(() =>
                {
                    Time.timeScale = 1f;
                    SceneManager.LoadScene("Home");
                    Resources.UnloadUnusedAssets();
                });
            }
            else
            {
                unlockGoButton.gameObject.SetActive(false);
            }
        }

        // 奖励物品（ScrollViewReward + ItemCell）
        BuildRewardItems();

        // 金币汇总行（可选，与 ScrollViewReward 互斥时留空）
        if (goldEarnedText != null)
        {
            int goldEarned = 0;
            if (win && _vm.rewardItems != null)
            {
                foreach (var ri in _vm.rewardItems)
                    if (ri.itemId == 1) { goldEarned = ri.count; break; }
            }
            if (win && goldEarned > 0)
            {
                goldEarnedText.text = $"金币 <color=#FFD700>+{goldEarned}</color>";
                goldEarnedText.gameObject.SetActive(true);
            }
            else { goldEarnedText.gameObject.SetActive(false); }
        }
        if (goldBalanceText != null)
        {
            if (win)
            {
                int balance = PlayerProfileService.Instance.Gold;
                goldBalanceText.text = $"余额：{balance}";
                goldBalanceText.gameObject.SetActive(true);
            }
            else { goldBalanceText.gameObject.SetActive(false); }
        }

        if (textBanner != null)
        {
            BattleChineseFontRuntime.EnsureLoaded();
            BattleChineseFontRuntime.ApplyToTMP(textBanner);
            if (win)
            {
                textBanner.text = "战斗胜利";
                textBanner.color = new Color(1f, 0.639f, 0f); // #FFA300
            }
            else
            {
                textBanner.text = "战斗失败";
                textBanner.color = new Color(0.616f, 0.616f, 0.616f); // #9d9d9d
            }
        }

        if (_textTime != null)
        {
            int total = Mathf.FloorToInt(_vm.battleDurationUnscaled);
            int mm = total / 60;
            int ss = total % 60;
            _textTime.text = $"时长：{mm:00}:{ss:00}";
        }

        if (starText != null)
        {
            if (win && _vm.stars > 0)
            {
                int s = Mathf.Clamp(_vm.stars, 1, 3);
                BattleChineseFontRuntime.EnsureLoaded();
                BattleChineseFontRuntime.ApplyToTMP(starText);
                starText.text = new string('★', s) + new string('☆', 3 - s);
                starText.gameObject.SetActive(true);
            }
            else
            {
                starText.gameObject.SetActive(false);
            }
        }

        if (_textKillNum != null)
            _textKillNum.text = $"击杀数量：{_vm.killCount}";

        // 波次统计
        if (textWave != null)
        {
            if (_vm.totalWaves > 0)
            {
                BattleChineseFontRuntime.EnsureLoaded();
                BattleChineseFontRuntime.ApplyToTMP(textWave);
                textWave.text = $"完成波次：{_vm.completedWaves}/{_vm.totalWaves}";
                textWave.gameObject.SetActive(true);
            }
            else
            {
                textWave.gameObject.SetActive(false);
            }
        }

        RefreshLevelLabel();

        if (_btnAgain != null) _btnAgain.gameObject.SetActive(!win);
        if (_btnNext != null)
        {
            _btnNext.gameObject.SetActive(win);
            if (win)
            {
                PlayerProfileService.Instance.LoadOrCreate();
                bool hasNext = SelectedLevelContext.HasSelection &&
                               ChapterLevelNavigation.TryGetNext(
                                   SelectedLevelContext.ChapterId,
                                   SelectedLevelContext.LevelId,
                                   out _,
                                   out int nextLv) &&
                               PlayerProfileService.Instance.IsLevelUnlocked(nextLv);
                _btnNext.interactable = hasNext;
            }
        }

        BuildSkillRows();
        BuildPassiveSkillList();

        transform.SetAsLastSibling();
    }

    public override void OnClose()
    {
        if (_openedWithPauseOnly)
        {
            Time.timeScale = 1f;
            _openedWithPauseOnly = false;
        }
    }

    private void RefreshLevelLabel()
    {
        if (_textLevel == null) return;

        if (!SelectedLevelContext.HasSelection)
        {
            _textLevel.text = string.Empty;
            return;
        }

        if (TableManager.Instance != null)
            TableManager.Instance.Init();

        int levelId = SelectedLevelContext.LevelId;
        string mapName = ChapterLevelDisplay.ResolveMapName(levelId);
        _textLevel.text = ChapterLevelDisplay.FormatLevelLabel(levelId, mapName);
    }

    private void BuildRewardItems()
    {
        EnsureRewardContentLayout();

        if (scrollViewRewardContent == null)
        {
            Debug.LogWarning("[GameResult] scrollViewRewardContent 未拖入，跳过奖励展示。");
            return;
        }
        if (itemCellPrefab == null)
        {
            Debug.LogWarning("[GameResult] itemCellPrefab 未拖入，跳过奖励展示。");
            return;
        }

        // 清空旧 children
        for (int i = scrollViewRewardContent.childCount - 1; i >= 0; i--)
            Destroy(scrollViewRewardContent.GetChild(i).gameObject);

        if (_vm == null || _vm.rewardItems == null || _vm.rewardItems.Count == 0)
        {
            GameLog.Info($"[GameResult] 无奖励物品展示（vm={_vm != null}, items={_vm?.rewardItems?.Count ?? -1}）");
            return;
        }

        GameLog.Info($"[GameResult] 开始生成 {_vm.rewardItems.Count} 个奖励 ItemCell...");

        foreach (var entry in _vm.rewardItems)
        {
            GameObject go = Instantiate(itemCellPrefab, scrollViewRewardContent, false);
            var cell = go.GetComponent<ItemCell>();
            if (cell == null)
            {
                Debug.LogWarning($"[GameResult] ItemCell 预制体上未找到 ItemCell 脚本，已销毁。prefab={itemCellPrefab.name}");
                Destroy(go);
                continue;
            }

            Sprite icon = null;
            if (!string.IsNullOrEmpty(entry.iconPath))
                icon = Resources.Load<Sprite>(entry.iconPath);

            GameLog.Info($"[GameResult] ItemCell.Bind: id={entry.itemId} name={entry.itemName} count={entry.count} grade={entry.grade} icon={icon != null} desc={entry.description}");
            cell.Bind(icon, entry.itemName, entry.count, entry.grade, entry.description ?? "");
        }
    }

    private void BuildSkillRows()
    {
        if (_scrollContent == null) return;

        for (int i = _scrollContent.childCount - 1; i >= 0; i--)
            Destroy(_scrollContent.GetChild(i).gameObject);

        GameObject rowPrefab = skillDamageRowPrefab;
        if (rowPrefab == null && !string.IsNullOrEmpty(skillRowPrefabResourcesPath))
            rowPrefab = Resources.Load<GameObject>(skillRowPrefabResourcesPath);

        PlayerSkills ps = FindObjectOfType<PlayerSkills>();
        if (rowPrefab == null || ps == null) return;

        var ids = new List<SkillId>(8);
        ps.GetEquippedSkillIdsOrdered(ids);

        for (int i = 0; i < ids.Count; i++)
        {
            SkillId id = ids[i];
            if (id == SkillId.None) continue;

            GameObject row = Instantiate(rowPrefab, _scrollContent, false);
            var cell = row.GetComponent<GameResultSkillDamageCell>();
            if (cell == null) cell = row.AddComponent<GameResultSkillDamageCell>();

            SkillDefinitionBase def = ps.skillCatalog != null ? ps.skillCatalog.Get(id) : null;
            int lv = ps.GetSkillLevel(id);
            int maxLv = def != null ? def.maxLevel : 5;
            bool isBreakthrough = lv >= maxLv;
            string nm = def != null && !string.IsNullOrEmpty(def.displayName)
                ? $"Lv.{lv} {def.displayName}"
                : id.ToString();
            Sprite ic = def != null ? def.icon : null;
            float dmg = BattleRunMetrics.GetSkillDamage(id);
            cell.Bind(ic, nm, dmg);
            cell.SetBreakthrough(isBreakthrough);
        }
    }

    private void BuildPassiveSkillList()
    {
        if (passiveSkillListParent == null || passiveSkillRowPrefab == null) return;

        for (int i = passiveSkillListParent.childCount - 1; i >= 0; i--)
            Destroy(passiveSkillListParent.GetChild(i).gameObject);

        PlayerSkills ps = FindObjectOfType<PlayerSkills>();
        if (ps == null) return;

        var ids = new List<SkillId>();
        ps.GetEquippedPassiveIdsOrdered(ids);

        SkillCatalog catalog = ps.skillCatalog;
        if (catalog == null) catalog = Resources.Load<SkillCatalog>("SkillCatalog");

        foreach (SkillId id in ids)
        {
            var def = catalog != null ? catalog.Get(id) : null;
            int level = ps.GetPassiveSkillLevel(id);

            GameObject row = Instantiate(passiveSkillRowPrefab, passiveSkillListParent, false);

            var cell = row.GetComponent<GamePassiveSkillCell>();
            if (cell != null)
            {
                string name = def != null ? def.displayName : id.ToString();
                Sprite icon = def != null ? def.icon : null;
                cell.Bind(icon, name, level);
            }
        }
    }

    private void OnExitClicked()
    {
        UiClickSound.PlayClose();
        Time.timeScale = 1f;
        SceneManager.LoadScene("Home");
        Resources.UnloadUnusedAssets();
    }

    private void OnAgainClicked()
    {
        UiClickSound.Play();
        Time.timeScale = 1f;

        // 本关重开：SelectedLevelContext 不变，直接重载 Game。
        GameObjectPool.ClearAllPools();
        SceneManager.LoadScene("Game");
    }

    private void OnNextClicked()
    {
        UiClickSound.Play();
        if (!SelectedLevelContext.HasSelection)
        {
            GameErrorPresenter.Show(GameErrorCodes.LevelNoContext);
            return;
        }

        int ch = SelectedLevelContext.ChapterId;
        int lv = SelectedLevelContext.LevelId;
        if (!ChapterLevelNavigation.TryGetNext(ch, lv, out int nch, out int nlv))
        {
            GameErrorPresenter.Show(GameErrorCodes.LevelNoNext);
            return;
        }

        PlayerProfileService.Instance.LoadOrCreate();
        if (!PlayerProfileService.Instance.IsLevelUnlocked(nlv))
        {
            GameErrorPresenter.Show(GameErrorCodes.LevelLocked, null, nlv);
            return;
        }

        SelectedLevelContext.Set(nch, nlv);

        Time.timeScale = 1f;
        SceneManager.LoadScene(BattleFlowLauncher.BattleLoadingSceneName);
    }
}
