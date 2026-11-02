#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SkillSelectionUIV4PreviewRunner : MonoBehaviour
{
    public Action<int, int> changeResolution;
    private CardSelectionPanel panel;
    private PlayerSkills skills;
    private CardDeck deck;
    private UIManager uiManager;
    private int freeCalls, adCalls, selectedCalls;
    private readonly List<Check> checks = new List<Check>();
    private string output;
    private List<CardDeck.DrawResult> passives;

    private IEnumerator Start()
    {
        output = Path.GetFullPath("美术资源/UI设计/技能选卡_Unity实装");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "preview-status.txt"), "running");
        Setup();
        Time.timeScale = 0;
        Show(passives, 1, 1);
        yield return Capture("01_三个被动_免费刷新_1080x1920");
        Record("主动与被动各保留五槽", panel.activeCountText.text == "2/5" && panel.passiveCountText.text == "3/5", panel.activeCountText.text + ", " + panel.passiveCountText.text);
        Record("升级等级与战斗HUD一致", panel.playerLevelText.text == "Lv.5", panel.playerLevelText.text);
        Record("通过现有UIManager打开并保留字体材质", uiManager.Top == panel && panel.cardSlots[0].titleText.fontSharedMaterial.name == "SkillSelectTextV4", panel.cardSlots[0].titleText.fontSharedMaterial.name);
        Record("未拥有被动的组合与上方已选主动联动", MatchFrameCount() == 1 && panel.cardSlots[0].BondedRowCount == 1, "HUD 高亮=" + MatchFrameCount());
        Record("免费态隐藏广告图标", !panel.adVideoIcon.activeSelf && panel.refreshButton.gameObject.activeInHierarchy, panel.refreshCountText.text);

        panel.refreshButton.onClick.Invoke();
        Record("免费刷新回调一次且转广告态", freeCalls == 1 && panel.adVideoIcon.activeSelf, panel.refreshCountText.text);
        yield return Capture("02_三个被动_广告刷新_1080x1920");
        panel.refreshButton.onClick.Invoke();
        Record("广告未成功时保留次数及按钮", adCalls == 1 && panel.refreshButton.gameObject.activeInHierarchy && panel.refreshCountText.text.Contains("1次"), "首次广告回调模拟失败");
        panel.refreshButton.onClick.Invoke();
        Record("广告成功后耗尽隐藏整个刷新入口", adCalls == 2 && !panel.refreshButton.gameObject.activeInHierarchy && !panel.selectionLayout.refreshArea.gameObject.activeSelf, "第二次广告回调模拟成功");
        Record("耗尽收起152高度", Mathf.Abs(panel.selectionLayout.paper.sizeDelta.y - 1472) < 1, panel.selectionLayout.paper.sizeDelta.ToString());
        yield return Capture("03_三个被动_次数用尽_1080x1920");

        skills.TryAddPassive(SkillId.PassiveDefense);
        skills.TryLevelUp(SkillId.AutoProjectileSword); skills.TryLevelUp(SkillId.AutoProjectileSword); skills.TryLevelUp(SkillId.AutoProjectileSword);
        var actives = new List<CardDeck.DrawResult> { Card(SkillId.LightningStrike, 0), Card(SkillId.LineBeam, 1), Card(SkillId.AutoProjectileSword, 4) };
        Show(actives, 1, 1);
        Record("全主动候选收起组合行并清除残留高亮", MatchFrameCount() == 0 && Mathf.Abs(panel.selectionLayout.paper.sizeDelta.y - 1480) < 1, panel.selectionLayout.paper.sizeDelta.ToString());
        Record("三类主动卡模板正确", panel.cardSlots[0].labelText.text == "新技能" && panel.cardSlots[1].labelText.text == "升级" && panel.cardSlots[2].labelText.text == "满级", "新技能/升级/满级");
        yield return Capture("04_新技能_升级_满级_1080x1920");
        panel.cardSlots[1].clickButton.onClick.Invoke();
        yield return new WaitForSecondsRealtime(0.3f);
        Record("暂停时间下选卡动画与回调正常", selectedCalls == 1 && !panel.cardSlots[0].clickButton.interactable, "选卡回调=" + selectedCalls);
        Show(passives, 1, 1);
        Record("再次打开恢复透明度与按钮", panel.cardSlots[0].clickButton.interactable && panel.cardSlots[0].GetComponent<CanvasGroup>().alpha == 1, "刷新/重开不残留淡出");
        Record("已拥有被动不再触发组合提醒", MatchFrameCount() == 0, "HUD 高亮=" + MatchFrameCount());

        var mixed = new List<CardDeck.DrawResult> { Card(SkillId.PassiveDefense, 1), Card(SkillId.OrbitingBlades, 0), Card(SkillId.LineBeam, 1) };
        Show(mixed, 0, 1);
        yield return Capture("05_主动被动混合_1080x1920");
        changeResolution(1080, 2400);
        yield return Capture("06_长屏适配_1080x2400");
        changeResolution(1080, 1440);
        yield return Capture("07_紧凑屏适配_1080x1440");
        changeResolution(360, 640);
        yield return Capture("08_手机尺寸_360x640");

        changeResolution(1080, 1920);
        Show(actives, 1, 1);
        var longCard = panel.cardSlots[0];
        var original = longCard.descText.text;
        longCard.descText.text = original + "\n" + original + "\n" + original;
        longCard.descText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, longCard.descText.preferredHeight);
        Canvas.ForceUpdateCanvases();
        longCard.descriptionScroll.verticalNormalizedPosition = 0;
        yield return null;
        Record("长文案完整保留并可滚动", longCard.descText.text.Length > original.Length && longCard.descriptionScroll.content.rect.height > longCard.descriptionScroll.viewport.rect.height && longCard.descriptionScroll.content.anchoredPosition.y > 0, "长文本不使用 Truncate；滚动偏移=" + longCard.descriptionScroll.content.anchoredPosition.y);
        Show(passives, 0, 1);
        yield return Capture("09_最终预览_1080x1920");
        var report = new Report { checks = checks, passed = checks.TrueForAll(c => c.passed), note = "Unity 编辑器实际 UGUI 渲染；隔离测试场景；未连接真实广告SDK，未进行微信真机验证。" };
        File.WriteAllText(Path.Combine(output, "验证报告.json"), JsonUtility.ToJson(report, true));
        File.WriteAllText(Path.Combine(output, "preview-status.txt"), report.passed ? "passed" : "failed");
        Debug.Log("[技能选卡 V4] 验证完成：" + checks.Count + " 项，" + (report.passed ? "通过" : "有失败项") + "。" + output);
    }

    private void Setup()
    {
        var camera = new GameObject("Preview Camera").AddComponent<Camera>();
        camera.orthographic = true; camera.transform.position = new Vector3(0, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(217, 221, 201, 255);
        deck = AssetDatabase.LoadAssetAtPath<CardDeck>("Assets/ScriptableObject/Roguelike/Card Deck/CardDeck.asset");
        SelectedLevelContext.Set(1, 102);
        var managerGO = new GameObject("Preview card manager"); managerGO.SetActive(false);
        var manager = managerGO.AddComponent<RoguelikeCardManager>(); manager.cardDeck = deck; manager.enabled = false; managerGO.SetActive(true);
        var player = new GameObject("Preview PlayerSkills"); player.SetActive(false);
        skills = player.AddComponent<PlayerSkills>(); skills.skillCatalog = deck.skillCatalog; skills.startingSkill = SkillId.AutoProjectileSword;
        skills.enabled = false; player.SetActive(true);
        skills.TryAddSkill(SkillId.LineBeam); skills.TryLevelUp(SkillId.LineBeam);
        skills.TryAddPassive(SkillId.PassiveAttack); skills.TryAddPassive(SkillId.PassiveMoveSpeed); skills.TryAddPassive(SkillId.PassiveMaxHp);
        var energy = player.AddComponent<PlayerEnergy>(); energy.enabled = false; energy.energy = 18;
        typeof(PlayerEnergy).GetField("_triggerCount", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(energy, 4);
        var canvasGO = new GameObject("Preview Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = 0;
        var bg = new GameObject("Preview backdrop", typeof(RectTransform), typeof(Image)); bg.transform.SetParent(canvasGO.transform, false);
        var bgRT = (RectTransform)bg.transform; bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one; bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color32(217, 221, 201, 255); bg.GetComponent<Image>().raycastTarget = false;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CardSelect/CardSelectionPanel.prefab");
        var uiGO = new GameObject("Preview UIManager"); uiGO.SetActive(false); uiGO.transform.SetParent(canvasGO.transform, false);
        uiManager = uiGO.AddComponent<UIManager>();
        var serialized = new SerializedObject(uiManager);
        serialized.FindProperty("stackRoot").objectReferenceValue = canvasGO.GetComponent<RectTransform>();
        serialized.FindProperty("overlayRoot").objectReferenceValue = canvasGO.GetComponent<RectTransform>();
        serialized.FindProperty("panelPrefabs").arraySize = 1;
        serialized.FindProperty("panelPrefabs").GetArrayElementAtIndex(0).objectReferenceValue = prefab;
        serialized.ApplyModifiedPropertiesWithoutUndo(); uiGO.SetActive(true);
        new GameObject("Preview EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        passives = new List<CardDeck.DrawResult> { Card(SkillId.PassiveDefense, 0), Card(SkillId.PassiveMaxHp, 1), Card(SkillId.PassiveMoveSpeed, 1) };
    }

    private CardDeck.DrawResult Card(SkillId id, int current)
    {
        var def = deck.skillCatalog.Get(id);
        return new CardDeck.DrawResult { skillId = id, skillDef = def, currentLevel = current, targetLevel = current + 1,
            template = id.IsPassive() ? deck.passiveTemplate : current == 0 ? deck.newSkillTemplate : current + 1 >= def.maxLevel ? deck.breakthroughTemplate : deck.upgradeTemplate };
    }

    private void Show(List<CardDeck.DrawResult> cards, int free, int ad)
    {
        panel = uiManager.Open<CardSelectionPanel>(new CardSelectionOpenPayload { Cards = cards, FreeRefreshCount = free, AdRefreshCount = ad,
            OnCardSelected = index => selectedCalls++, OnRefreshRequested = () => { freeCalls++; panel.UpdateRefreshCount(0, 1); },
            OnAdRefreshRequested = () => { adCalls++; if (adCalls >= 2) panel.UpdateRefreshCount(0, 0); } },
            new UiOpenOptions { PauseTime = true, UseUnscaledTime = true, CloseOnBack = false });
        Canvas.ForceUpdateCanvases(); panel.selectionLayout.FitToSafeArea();
    }

    private IEnumerator Capture(string name)
    {
        yield return new WaitForSecondsRealtime(0.3f);
        yield return null;
        Canvas.ForceUpdateCanvases(); panel.selectionLayout.FitToSafeArea();
        Record("可见内容位于屏幕内 " + name, IsOnScreen(panel.selectionLayout.paper) && IsOnScreen(panel.selectionLayout.hint), Screen.width + "x" + Screen.height);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"));
        yield return null; yield return null;
    }

    private static bool IsOnScreen(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        foreach (var corner in corners)
        {
            Vector2 p = RectTransformUtility.WorldToScreenPoint(null, corner);
            if (p.x < -1 || p.y < -1 || p.x > Screen.width + 1 || p.y > Screen.height + 1) return false;
        }
        return true;
    }

    private int MatchFrameCount()
    {
        int count = 0;
        var hud = panel.transform.Find("DesignRootV4/EquippedHUDV4/Skill");
        foreach (Transform cell in hud)
        {
            var frame = cell.Find("MatchFrame"); if (frame != null && frame.gameObject.activeSelf) count++;
        }
        return count;
    }

    private void Record(string name, bool passed, string detail) => checks.Add(new Check { name = name, passed = passed, detail = detail });
    [Serializable] private class Check { public string name, detail; public bool passed; }
    [Serializable] private class Report { public bool passed; public string note; public List<Check> checks; }
}
#endif
