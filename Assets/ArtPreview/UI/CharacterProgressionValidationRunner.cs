#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CharacterProgressionValidationRunner : UIValidationRunnerBase
{
    protected override string OutputFolder => "CharacterProgression";
    private CharacterSelectionPanel panel;
    private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    private T Field<T>(string name) { return (T)typeof(CharacterSelectionPanel).GetField(name,Fields).GetValue(panel); }
    private List<CharacterSelectionElement> Cells => Field<List<CharacterSelectionElement>>("_cells");
    private string Selected => Field<string>("_selectedCharId");
    protected override IEnumerator RunChecks()
    {
        tabs.SwitchTo("character");yield return new WaitForSecondsRealtime(.5f);
        panel=FindObjectOfType<CharacterSelectionPanel>();
        Record("Character page created through existing tab",panel!=null&&tabs.ActiveTabId=="character");
        var svc=PlayerProfileService.Instance;
        string knifeId=svc.EquippedCharacterId;
        var knife=Cells.First(c=>c.CharacterDef.characterId==knifeId).CharacterDef;
        var pistol=Cells.First(c=>c.CharacterDef.startingSkill==SkillId.AutoProjectilePistol).CharacterDef;
        var presentation=panel.GetComponent<CharacterPanelPresentation>();
        Record("Five characters retained",Cells.Count==5);
        Record("Character cards have visible positive height",((RectTransform)Cells[0].transform).rect.height>200&&Field<RectTransform>("listContent").rect.height>200&&Cells.All(c=>c.gameObject.activeInHierarchy));
        Record("Nine attributes retained",Field<List<UpgradeAttrRow>>("_attrRows").Count==9);
        Record("Dedicated child prefab references",Field<CharacterSelectionElement>("cellPrefab").name=="CharacterCard"&&Field<GameObject>("attrRowPrefab").name=="CharacterStatRow");
        var statsTab=Field<Toggle>("statsTabToggle");var skillsTab=Field<Toggle>("skillTabToggle");
        Record("Only selected tab has background",statsTab.GetComponent<CharacterTabPresentation>().selectedBackground.activeSelf&&!skillsTab.GetComponent<CharacterTabPresentation>().selectedBackground.activeSelf);
        yield return Capture("character-stats-1080x1920");
        skillsTab.isOn=true;yield return null;
        Record("Skills tab switches views",Field<GameObject>("skillView").activeSelf&&!Field<GameObject>("statsView").activeSelf);
        Record("Promotion checks level and fragments",!Field<Button>("promoteButton").interactable);
        Record("Skill name and complete description bound",!string.IsNullOrEmpty(presentation.skillTitle.text)&&!string.IsNullOrEmpty(Field<TextMeshProUGUI>("detailSkillText").text));
        yield return Capture("character-skills-1080x1920");
        statsTab.isOn=true;yield return null;
        int gold=svc.Gold,lv=svc.GetHeroLevel(knifeId),cost=knife.upgradeData.GetCostForLevel(lv+1);
        Field<Button>("upgradeButton").onClick.Invoke();Field<Button>("upgradeButton").onClick.Invoke();
        yield return new WaitForSecondsRealtime(.6f);
        Record("Upgrade spends correct gold and blocks double click",svc.GetHeroLevel(knifeId)==lv+1&&svc.Gold==gold-cost);
        Record("Upgrade preserves selected hero and all rows",Selected==knifeId&&Field<List<UpgradeAttrRow>>("_attrRows").Count==9);
        Click(pistol.characterId);yield return null;
        Record("Browsing preserves equipped hero",Selected==pistol.characterId&&svc.EquippedCharacterId==knifeId);
        Record("Equip and start buttons remain exclusive",Field<Button>("confirmButton").gameObject.activeSelf&&!Field<Button>("startGameButton").gameObject.activeSelf);
        Record("Equipped marker independent from selected card",Cells.First(c=>c.CharacterDef.characterId==knifeId).transform.Find("EquippedMark").gameObject.activeSelf&&!Cells.First(c=>c.CharacterDef.characterId==pistol.characterId).transform.Find("EquippedMark").gameObject.activeSelf);
        yield return Capture("character-browse-1080x1920");
        Field<Button>("confirmButton").onClick.Invoke();yield return null;
        Record("Equip action persists through profile service",svc.EquippedCharacterId==pistol.characterId&&Field<Button>("startGameButton").gameObject.activeSelf);
        var locked=Cells.FirstOrDefault(c=>!CharacterUnlockEvaluator.IsUnlocked(c.CharacterDef));
        string before=Selected;
        Record("Locked cards keep readable condition and click interaction",locked!=null&&locked.GetComponent<Button>().interactable&&!string.IsNullOrEmpty(locked.transform.Find("Condition").GetComponent<TMP_Text>().text));
        if(locked!=null)locked.GetComponent<Button>().onClick.Invoke();yield return null;
        Record("Locked preview preserves selection",Selected==before);
        Click(knifeId);yield return null;
        foreach(var upgrade in svc.Data.heroUpgrades)if(upgrade.characterId==knifeId)upgrade.level=knife.upgradeData.rareRequiredLevel;
        svc.Data.characterFragmentKeys=new[]{knifeId};svc.Data.characterFragmentValues=new[]{knife.upgradeData.rareFragmentCost+5};
        svc.MarkDirtyAndSave();panel.OnOpen(null);skillsTab.isOn=true;yield return null;
        Record("Promotion enables at actual hero requirements",Field<Button>("promoteButton").interactable);
        Field<Button>("promoteButton").onClick.Invoke();yield return null;yield return null;
        Record("Promotion consumes fragments and updates stage",svc.GetHeroStage(knifeId)==1&&svc.GetFragmentCount(knifeId)==5);
        yield return new WaitForSecondsRealtime(1.2f); // Tier visuals reveal during the existing 1.05-second promotion animation.
        Record("Tier visuals reflect unlocked stage",presentation.tierMarks[0].sprite==presentation.tierUnlocked&&presentation.tierStates[0].text=="已解锁");
        yield return Capture("character-promoted-1080x1920");
        var body=Field<TextMeshProUGUI>("detailSkillText");string original=body.text;
        body.text=string.Concat(Enumerable.Repeat(original+"\n",8));Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(presentation.skillContent);yield return null;
        Record("Long descriptions expand into scrollable content",presentation.skillContent.rect.height>presentation.skillViewport.rect.height&&body.enableWordWrapping);
        body.text=original;Canvas.ForceUpdateCanvases();
        changeResolution(1080,2400);yield return Capture("character-skills-long-1080x2400");
        Record("Long screen enlarges paper without stretching portraits",presentation.paper.rect.height>618&&Mathf.Approximately(Field<Image>("detailPortrait").rectTransform.rect.height,350));
        statsTab.isOn=true;yield return Capture("character-stats-long-1080x2400");
        changeResolution(1080,1440);yield return Capture("character-short-1080x1440");
        Record("Short screen scrolls page body above fixed action",presentation.body.rect.height>presentation.bodyViewport.rect.height);
        changeResolution(360,640);yield return Capture("character-phone-360x640");
        tabs.SwitchTo("battle");yield return null;tabs.SwitchTo("character");yield return null;
        Record("Tab revisit preserves selected hero and row count",Selected==knifeId&&Field<List<UpgradeAttrRow>>("_attrRows").Count==9);
        changeResolution(1080,1920);yield return Capture("character-final-1080x1920");
    }
    private void Click(string id) { Cells.First(c=>c.CharacterDef.characterId==id).GetComponent<Button>().onClick.Invoke(); }
}
#endif

