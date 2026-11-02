#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DialogsValidationRunner : UIValidationRunnerBase
{
    protected override string OutputFolder => "Dialogs";
    static T Field<T>(object o, string name) => (T)o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
    bool audioCaptured, bgmMute, sfxMute, bgmHad, sfxHad;
    int bgmRaw, sfxRaw;
    void CaptureAudio()
    {
        bgmMute = AudioService.Instance.BgmMute; sfxMute = AudioService.Instance.SfxMute;
        bgmHad = PlayerPrefs.HasKey(AudioService.BgmMuteKey); sfxHad = PlayerPrefs.HasKey(AudioService.SfxMuteKey);
        bgmRaw = PlayerPrefs.GetInt(AudioService.BgmMuteKey); sfxRaw = PlayerPrefs.GetInt(AudioService.SfxMuteKey); audioCaptured = true;
    }
    void RestoreAudio()
    {
        if (!audioCaptured) return;
        AudioService.Instance.BgmMute = bgmMute; AudioService.Instance.SfxMute = sfxMute;
        if (bgmHad) PlayerPrefs.SetInt(AudioService.BgmMuteKey, bgmRaw); else PlayerPrefs.DeleteKey(AudioService.BgmMuteKey);
        if (sfxHad) PlayerPrefs.SetInt(AudioService.SfxMuteKey, sfxRaw); else PlayerPrefs.DeleteKey(AudioService.SfxMuteKey);
        PlayerPrefs.Save(); audioCaptured = false;
    }
    protected override void OnDestroy() { RestoreAudio(); base.OnDestroy(); }
    protected override IEnumerator RunChecks()
    {
        CaptureAudio();
        var ui = UIManager.Instance; fixture.SetProgress(1); tabs.SwitchTo("battle");
        var levels = ui.Open<LevelSelectPanel>(null, UiOpenOptions.NonPausingModal); yield return new WaitForSecondsRealtime(.5f);
        var loop = levels.GetComponentInChildren<LoopVerticalScrollRect>(); loop.StopAllCoroutines(); int total = loop.totalCount; loop.ClearCells(); loop.totalCount = total; loop.RefillCells(0); yield return null; yield return null;
        var rows = loop.content.GetComponentsInChildren<LevelSelectLevelRowCell>();
        Record("Level table includes chapter headers and levels", loop.totalCount > 8 && rows.Length > 2);
        Record("Cleared level keeps three stars", rows.Any(r => r.LevelId == 101 && Field<TextMeshProUGUI>(r, "starText").text == "★★★"));
        Record("Available level remains interactable", rows.Any(r => r.LevelId == 102 && Field<Button>(r, "clickButton").interactable));
        Record("Locked level remains disabled", rows.Any(r => r.LevelId == 103 && !Field<Button>(r, "clickButton").interactable));
        yield return Capture("level-select-1080x1920");
        loop.RefillCells(8); yield return null; yield return null;
        var cellDriver = levels.GetComponentInChildren<LevelSelectLoopScrollDriver>();
        foreach (var cell in loop.content.GetComponentsInChildren<LevelSelectThemeCell>()) cellDriver.ProvideData(cell.transform, 9);
        Record("Recycled level cells can become chapter headers", loop.content.GetComponentsInChildren<LevelSelectThemeCell>().All(c => c.chapter.gameObject.activeSelf && !c.level.gameObject.activeSelf));
        loop.RefillCells(0); yield return null; yield return null; rows = loop.content.GetComponentsInChildren<LevelSelectLevelRowCell>();
        Field<Button>(rows.First(r => r.LevelId == 101), "clickButton").onClick.Invoke(); yield return null;
        var dialog = FindObjectOfType<UiConfirmDialog>(); Record("Level click retains enter confirmation", dialog != null && dialog.titleText.text == "进入关卡");
        dialog.cancelButton.onClick.Invoke(); yield return null; ui.CloseTop(); yield return new WaitForSecondsRealtime(.3f);
        int calls = 0; bool result = true;
        ui.ShowConfirm("退出游戏", "确定要退出当前关卡吗？\n进度将不会保存。", v => { calls++; result = v; }); yield return null;
        dialog = FindObjectOfType<UiConfirmDialog>(); yield return Capture("confirm-1080x1920");
        dialog.cancelButton.onClick.Invoke(); yield return null;
        Record("Cancel callback runs once and returns false", calls == 1 && !result);
        ui.ShowConfirm("购买确认", "消耗 <color=#B88126>30 钻石</color>兑换角色碎片？", v => { calls++; result = v; }); yield return null;
        dialog.okButton.onClick.Invoke(); yield return null; Record("Confirm callback runs once and returns true", calls == 2 && result);
        ui.ShowAlert("操作提示", "当前资源不足，请稍后再试。"); yield return null;
        var layout = dialog.GetComponent<DialogThemeLayout>();
        Record("Alert hides cancel and centers confirm", !dialog.cancelButton.gameObject.activeSelf && Mathf.Abs(((RectTransform)dialog.okButton.transform).anchoredPosition.x) < 1);
        yield return Capture("alert-1080x1920"); dialog.okButton.onClick.Invoke(); yield return null;
        Record("Confirmation pause lock released", Mathf.Approximately(Time.timeScale, 1));
        var settings = ui.Open<SettingsPanel>(null, UiOpenOptions.NonPausingModal); yield return new WaitForSecondsRealtime(.35f);
        var music = Field<Toggle>(settings, "soundToggle"); var sound = Field<Toggle>(settings, "sfxToggle");
        music.isOn = false; sound.isOn = false; yield return null;
        Record("Music and sound controls still update AudioService", AudioService.Instance.BgmMute && AudioService.Instance.SfxMute);
        music.isOn = true; sound.isOn = true; yield return null;
        Record("Audio switch labels reflect enabled state", Field<TextMeshProUGUI>(settings, "soundText").text == "音乐开" && Field<TextMeshProUGUI>(settings, "sfxText").text == "音效开");
        var input = Field<InputField>(settings, "giftCodeInput"); var claim = Field<Button>(settings, "claimButton");
        Record("Gift input keeps legacy InputField and WeChat bridge", input != null && input.GetComponent<WechatInputFieldEnabler>() != null && input.textComponent.font != null);
        Record("Gift daily state retained", claim.interactable == GiftCodeService.CanClaimToday && input.interactable == GiftCodeService.CanClaimToday);
        var giftState = PlayerPrefs.GetString("gift_code_claimed_hashes", "") + PlayerPrefs.GetString("gift_code_last_claim_date", "");
        if (claim.interactable) { input.text = "ui-preview-invalid-code"; claim.onClick.Invoke(); yield return null; input.text = ""; }
        Record("Invalid gift test preserves reward history", giftState == PlayerPrefs.GetString("gift_code_claimed_hashes", "") + PlayerPrefs.GetString("gift_code_last_claim_date", ""));
        var toast = FindObjectOfType<UiToastPanel>(); if (toast != null) toast.HideImmediate();
        yield return Capture("settings-1080x1920");
        Field<Button>(settings, "aboutButton").onClick.Invoke(); yield return new WaitForSecondsRealtime(.35f);
        var about = FindObjectOfType<AboutPanel>();
        var credits = about.GetComponentsInChildren<TMP_Text>().First(t => t.text.Contains("game-icons.net"));
        Record("About preserves attribution and license", credits.text.Contains("CC BY 3.0") && credits.text.Contains("即梦") && credits.text.Contains("© 2024-2026"));
        yield return Capture("about-1080x1920");
        Field<Button>(about, "closeButton").onClick.Invoke(); yield return new WaitForSecondsRealtime(.3f);
        Record("About returns to settings", settings.gameObject.activeInHierarchy); ui.CloseTop(); yield return new WaitForSecondsRealtime(.3f);
        ItemTooltip.Show("飞刀娃碎片", "用于解锁飞刀娃，以及角色升阶。\n可从兑换页面获得。"); yield return null;
        var tip = ui.GetOrCreateItemTooltip(); Record("Tooltip preserves full item text", Field<TextMeshProUGUI>(tip, "descText").text.Contains("角色升阶"));
        yield return Capture("item-tooltip-1080x1920");
        string longText = string.Join("\n", Enumerable.Repeat("较长物品说明：展示来源、解锁条件与角色升阶用途，所有文字均应可滚动查看。", 16));
        ItemTooltip.Show("较长物品说明测试", longText); yield return null; yield return null;
        var descriptionScroll = Field<ScrollRect>(tip, "descriptionScroll");
        Record("Long tooltip scrolls instead of clipping text", descriptionScroll.content.rect.height > descriptionScroll.viewport.rect.height && Field<TextMeshProUGUI>(tip, "descText").text == longText);
        var anchor = new GameObject("TooltipEdgeAnchor", typeof(RectTransform)).GetComponent<RectTransform>(); anchor.SetParent(tip.transform.parent, false); anchor.anchorMin = anchor.anchorMax = anchor.pivot = Vector2.one; anchor.anchoredPosition = new Vector2(-12, -12); anchor.sizeDelta = new Vector2(40, 40);
        ItemTooltip.Show("边缘物品", longText, anchor); yield return null;
        var tipPanel = Field<RectTransform>(tip, "contentPanel"); var bounds = ((RectTransform)tip.transform).rect; var pos = tipPanel.anchoredPosition;
        Record("Tooltip stays inside canvas at edge", pos.x + tipPanel.rect.width / 2 <= bounds.xMax + 1 && pos.y + tipPanel.rect.height / 2 <= bounds.yMax + 1);
        yield return Capture("item-tooltip-long-edge-1080x1920"); Destroy(anchor.gameObject); Field<Button>(tip, "dismissMask").onClick.Invoke(); yield return null;
        Record("Tooltip mask dismisses", !tip.gameObject.activeSelf);
        ui.ShowToast("请求失败，请稍后重试\n错误码：E1004", 2); yield return null;
        toast = FindObjectOfType<UiToastPanel>(); Record("Toast does not block input", !toast.GetComponent<CanvasGroup>().blocksRaycasts && toast.GetComponentsInChildren<Graphic>().All(g => !g.raycastTarget));
        Record("Toast remains a small centered surface", ((RectTransform)toast.transform).rect.width <= 900 && ((RectTransform)toast.transform).rect.height < 400);
        Record("Toast retains error code and does not pause", Field<TextMeshProUGUI>(toast, "messageText").text.Contains("E1004") && Mathf.Approximately(Time.timeScale, 1));
        yield return Capture("toast-1080x1920"); yield return new WaitForSecondsRealtime(2);
        Record("Toast still automatically hides", !toast.gameObject.activeSelf);
        changeResolution(1080, 1440); settings = ui.Open<SettingsPanel>(null, UiOpenOptions.NonPausingModal); yield return Capture("settings-short-1080x1440");
        var settingsLayout = settings.GetComponent<DialogThemeLayout>(); Record("Short screen keeps settings inside canvas", settingsLayout.panel.rect.height <= ((RectTransform)settings.transform).rect.height - 100);
        ui.CloseTop(); yield return new WaitForSecondsRealtime(.3f);
        ui.ShowConfirm("较长提示", string.Join("\n", Enumerable.Repeat("这是一段较长的提示说明，按钮应始终留在弹窗底部，正文可以上下滚动。", 14)), _ => { }); yield return null;
        Record("Long confirmation retains scrollable full body", layout.scroll.content.rect.height > layout.scroll.viewport.rect.height);
        yield return Capture("confirm-long-short-1080x1440"); ui.CloseConfirm();
        changeResolution(1080, 2400); levels = ui.Open<LevelSelectPanel>(null, UiOpenOptions.NonPausingModal); yield return Capture("level-select-long-1080x2400"); ui.CloseTop(); yield return new WaitForSecondsRealtime(.3f);
        changeResolution(360, 640); settings = ui.Open<SettingsPanel>(null, UiOpenOptions.NonPausingModal); yield return Capture("settings-phone-360x640"); ui.CloseTop(); yield return new WaitForSecondsRealtime(.3f);
        changeResolution(1080, 1920); RestoreAudio();
        Record("Original audio preferences restored", PlayerPrefs.HasKey(AudioService.BgmMuteKey) == bgmHad && PlayerPrefs.HasKey(AudioService.SfxMuteKey) == sfxHad && (!bgmHad || PlayerPrefs.GetInt(AudioService.BgmMuteKey) == bgmRaw) && (!sfxHad || PlayerPrefs.GetInt(AudioService.SfxMuteKey) == sfxRaw));
        var items = TableManager.Instance.GetTable<ProtoTable.ItemTable>();
        System.IO.File.WriteAllText(System.IO.Path.Combine(output, "backpack-item-reference.txt"), string.Join("\n", items.Values.Cast<ProtoTable.ItemTable>().Select(r => r.ID + " | " + r.ItemName + " | " + r.IconPath + " | " + r.Description)));
    }
}
#endif
