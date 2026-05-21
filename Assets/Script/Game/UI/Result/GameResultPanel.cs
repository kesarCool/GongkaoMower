using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 传入 <see cref="GameResultPanel"/> 的结算数据（由 <see cref="BattleOutcomeCoordinator"/> 组装）。
/// </summary>
public sealed class GameResultViewModel
{
    public bool victory;
    public float battleDurationUnscaled;
    public int killCount;
}

/// <summary>
/// 局内结算 UI：胜负图、时长、击杀、技能伤害列表、退出/重开/下一关。
/// </summary>
[DisallowMultipleComponent]
public class GameResultPanel : UIPanelBase
{
    [Header("可选：未拖则用 Resources.Load<Sprite>(\"pic_sl\"/\"pic_sb\")")]
    [SerializeField] private Sprite winBannerSprite;
    [SerializeField] private Sprite loseBannerSprite;

    [Header("技能行预制体（默认空则运行时 Load：见 skillRowPrefabResourcesPath）")]
    [SerializeField] private GameObject skillDamageRowPrefab;

    [SerializeField] private string skillRowPrefabResourcesPath = string.Empty;

    private Image _bannerImage;
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
        _bannerImage = transform.Find("Image")?.GetComponent<Image>();
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

        if (_bannerImage != null)
        {
            Sprite sp = win ? ResolveWinSprite() : ResolveLoseSprite();
            if (sp != null) _bannerImage.sprite = sp;
        }

        if (_textTime != null)
        {
            int total = Mathf.FloorToInt(_vm.battleDurationUnscaled);
            int mm = total / 60;
            int ss = total % 60;
            _textTime.text = $"时长：{mm:00}:{ss:00}";
        }

        if (_textKillNum != null)
            _textKillNum.text = $"击杀数量：{_vm.killCount}";

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

    private Sprite ResolveWinSprite()
    {
        if (winBannerSprite != null) return winBannerSprite;
        return Resources.Load<Sprite>("pic_sl");
    }

    private Sprite ResolveLoseSprite()
    {
        if (loseBannerSprite != null) return loseBannerSprite;
        return Resources.Load<Sprite>("pic_sb");
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
            string nm = def != null && !string.IsNullOrEmpty(def.displayName) ? def.displayName : id.ToString();
            Sprite ic = def != null ? def.icon : null;
            float dmg = BattleRunMetrics.GetSkillDamage(id);
            cell.Bind(ic, nm, dmg);
        }
    }

    private void OnExitClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Home");
    }

    private void OnAgainClicked()
    {
        Time.timeScale = 1f;

        // 本关重开：SelectedLevelContext 不变，直接重载 Game。
        GameObjectPool.ClearAllPools();
        SceneManager.LoadScene("Game");
    }

    private void OnNextClicked()
    {
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
