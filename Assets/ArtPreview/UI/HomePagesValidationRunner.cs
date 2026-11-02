#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class HomePagesValidationRunner:UIValidationRunnerBase
{
    protected override string OutputFolder=>"HomePages";
    private static T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);
    private ShopView shop;
    private List<ShopCell> ShopCells=>Field<List<ShopCell>>(shop,"_cells");
    private void Buy(ShopCell cell)=>Field<Button>(cell,"btnBuy").onClick.Invoke();
    protected override IEnumerator RunChecks()
    {
        fixture.SetProgress(21);hub.RefreshCurrencyHud();
        tabs.SwitchTo("shop");yield return new WaitForSecondsRealtime(.5f);
        shop=FindObjectOfType<ShopView>();var profile=PlayerProfileService.Instance;
        Record("Shop retains seven configured products",ShopCells.Count==7,ShopCells.Count.ToString());
        var presentation=shop.GetComponent<HomeExchangePresentation>();
        Record("Shop groups two bonuses and five paid products",presentation.bonusContent.childCount==2&&presentation.fragmentContent.childCount==5);
        Record("Countdown uses real service state",!string.IsNullOrEmpty(Field<TextMeshProUGUI>(shop,"textRefreshCountdown").text));
        yield return Capture("exchange-initial-1080x1920");
        var free=ShopCells.First(c=>c.ShopRow.PriceType==0);int freeId=free.ShopRow.ID,gold=profile.Gold,amount=free.ShopRow.ItemNumber;
        Buy(free);yield return null;yield return null;
        Record("Free claim awards exact configured amount",profile.Gold==gold+amount);
        var freeNow=ShopCells.First(c=>c.ShopRow.ID==freeId);
        Record("Free claim refreshes sold state and disables card",!Field<Button>(freeNow,"btnBuy").interactable&&freeNow.GetComponent<ShopCellPresentation>().priceLabel.text=="已领取");
        var ad=ShopCells.First(c=>c.ShopRow.PriceType==3);int diamond=profile.Diamond,adAmount=ad.ShopRow.ItemNumber;
        Buy(ad);yield return null;yield return null;
        Record("Existing ad callback route awards once (project stub)",profile.Diamond==diamond+adAmount);
        var paid=ShopCells.First(c=>c.ShopRow.PriceType==2);int paidId=paid.ShopRow.ID,itemId=paid.ShopRow.ItemID,price=ShopService.GetPrice(paid.ShopRow),before=profile.GetItemCount(itemId);diamond=profile.Diamond;
        Buy(paid);yield return new WaitForSecondsRealtime(.3f);
        var dialog=FindObjectOfType<UiConfirmDialog>();Record("Paid purchase keeps confirmation",dialog!=null&&profile.Diamond==diamond);
        dialog.cancelButton.onClick.Invoke();yield return null;Record("Cancel preserves wallet and purchase count",profile.Diamond==diamond&&profile.GetItemCount(itemId)==before);
        Buy(ShopCells.First(c=>c.ShopRow.ID==paidId));yield return new WaitForSecondsRealtime(.3f);FindObjectOfType<UiConfirmDialog>().okButton.onClick.Invoke();yield return null;yield return null;
        Record("Confirm spends exact currency and delivers item",profile.Diamond==diamond-price&&profile.GetItemCount(itemId)==before+1);
        var remaining=ShopCells.First(c=>c.ShopRow.ID==paidId);Record("Purchase updates remaining limit",remaining.GetComponent<ShopCellPresentation>().limitLabel.text.Contains("2 / 3"));
        profile.SpendDiamond(profile.Diamond);Buy(ShopCells.First(c=>c.ShopRow.ID==paidId));yield return new WaitForSecondsRealtime(.3f);FindObjectOfType<UiConfirmDialog>().okButton.onClick.Invoke();yield return null;
        Record("Insufficient balance grants no item",profile.GetItemCount(itemId)==before+1&&profile.Diamond==0);
        profile.AddDiamond(325);profile.RecordShopPurchase(paidId,2);shop.OnTabRefresh();yield return null;
        Record("Sold fragment card disables action",!Field<Button>(ShopCells.First(c=>c.ShopRow.ID==paidId),"btnBuy").interactable);
        yield return Capture("exchange-purchase-states-1080x1920");
        foreach(var log in profile.Data.shopPurchaseLogs)log.lastResetTimestamp=System.DateTimeOffset.UtcNow.AddDays(-90).ToUnixTimeSeconds();
        ShopService.CheckAndRefresh();shop.OnTabRefresh();yield return null;
        Record("Period refresh restores free claim",Field<Button>(ShopCells.First(c=>c.ShopRow.ID==freeId),"btnBuy").interactable);
        tabs.SwitchTo("lexicon");yield return new WaitForSecondsRealtime(.4f);
        var lexicon=FindObjectOfType<LexiconPreviewPanel>();var driver=Field<LexiconPreviewLoopScrollDriver>(lexicon,"loopDriver");var loop=Field<LoopVerticalScrollRect>(lexicon,"loopScroll");
        var themes=Field<List<Toggle>>(lexicon,"_themeToggles");
        Record("Lexicon retains two themes and ten food filters",themes.Count==2&&Field<List<Toggle>>(lexicon,"_categoryToggles").Count==10);
        Record("Seventy terms map to thirty five pooled rows",driver.EntryCount==70&&loop.totalCount==35);
        var rows=loop.content.GetComponentsInChildren<LexiconPreviewEntryRowCell>();
        Record("Initial paired row has sequential terms",rows.Length>0&&Field<TextMeshProUGUI>(rows[0],"bodyText").text=="北京烤鸭"&&Field<TextMeshProUGUI>(rows[0],"rightText").text=="炸酱面");
        yield return Capture("lexicon-food-1080x1920");
        var firstRow=rows[0];driver.ProvideData(firstRow.transform,34);
        Record("Virtual binding reaches final two food terms",Field<TextMeshProUGUI>(firstRow,"leftIndex").text=="69"&&Field<TextMeshProUGUI>(firstRow,"rightIndex").text=="70");
        driver.SetLines(new[]{"第一项","第二项","第三项"});yield return null;
        rows=loop.content.GetComponentsInChildren<LexiconPreviewEntryRowCell>();driver.ProvideData(rows[0].transform,1);
        Record("Odd final row hides unused right term",loop.totalCount==2&&!Field<GameObject>(rows[0],"rightItem").activeSelf);
        driver.ProvideData(rows[0].transform,0);Record("Reused row restores right term",Field<GameObject>(rows[0],"rightItem").activeSelf&&Field<TextMeshProUGUI>(rows[0],"rightText").text=="第二项");
        themes[1].isOn=true;yield return null;yield return null;
        Record("Idiom switch rebuilds seven categories and real data",Field<List<Toggle>>(lexicon,"_categoryToggles").Count==7&&driver.EntryCount==LexiconPreviewCatalog.CollectDisplayTexts(2,1).Count);
        Field<List<Toggle>>(lexicon,"_categoryToggles")[1].isOn=true;yield return null;
        Record("Category switch updates count and terms",driver.EntryCount==LexiconPreviewCatalog.CollectDisplayTexts(2,2).Count);
        yield return Capture("lexicon-idioms-1080x1920");
        driver.SetLines(new[]{"这是一个用于验证自动换行并撑高虚拟列表的较长词条名称","短词"});yield return null;
        rows=loop.content.GetComponentsInChildren<LexiconPreviewEntryRowCell>();Record("Long term expands pooled row",rows[0].GetComponent<LayoutElement>().preferredHeight>111);
        lexicon.OnOpen(null);yield return null;
        var lexPresentation=lexicon.GetComponent<HomeLexiconPresentation>();Record("Category header reflects real count",lexPresentation.countLabel.text.Contains("70"));
        profile.Data.achievementProgress=new[]{new AchievementProgressEntry{groupId=1,currentValue=1380,claimedStage=1},new AchievementProgressEntry{groupId=2,currentValue=1,claimedStage=1},new AchievementProgressEntry{groupId=3,currentValue=2,claimedStage=1}};
        AchievementService.Instance.MarkDirty();tabs.SwitchTo("achievement");yield return new WaitForSecondsRealtime(.4f);
        var achievements=FindObjectOfType<AchievementPanel>();var achLoop=Field<LoopVerticalScrollRect>(achievements,"loopScroll");
        var ready=Field<TMP_Text>(achievements,"readyCountText");Record("Achievement banner derives claimable count",ready.text.Contains("2"),ready.text);
        var cells=achLoop.content.GetComponentsInChildren<AchievementCell>();var claim=cells.First(c=>Field<Button>(c,"btnClaim").gameObject.activeSelf);
        int group=Field<int>(claim,"_groupId"),stage=Field<int>(claim,"_stage");
        var stageInfo=AchievementService.Instance.GetAchievementGroups().First(g=>g.groupId==group).stages.First(s=>s.stage==stage);diamond=profile.Diamond;
        yield return Capture("achievements-ready-1080x1920");
        Field<Button>(claim,"btnClaim").onClick.Invoke();yield return null;yield return null;
        Record("Claim awards configured reward and marks stage",profile.Diamond==diamond+stageInfo.rewardCount&&!AchievementService.Instance.CanClaim(group,stage));
        Record("Duplicate claim awards nothing",!AchievementService.Instance.Claim(group,stage).success&&profile.Diamond==diamond+stageInfo.rewardCount);
        Record("Claim refresh updates ready banner",ready.text.Contains("1"),ready.text);
        achLoop.RefillCells(Mathf.Max(0,achLoop.totalCount-4));yield return null;yield return null;
        cells=achLoop.content.GetComponentsInChildren<AchievementCell>();Record("Claimed rows retain checked state",cells.Any(c=>Field<GameObject>(c,"objClaimed").activeSelf));
        yield return Capture("achievements-claimed-1080x1920");
        changeResolution(1080,2400);tabs.SwitchTo("lexicon");yield return Capture("lexicon-long-1080x2400");
        Record("Long screen expands lexicon viewport",loop.viewport.rect.height>785);
        changeResolution(360,640);tabs.SwitchTo("shop");yield return Capture("exchange-phone-360x640");tabs.SwitchTo("lexicon");yield return Capture("lexicon-phone-360x640");tabs.SwitchTo("achievement");yield return Capture("achievements-phone-360x640");
        changeResolution(1080,1440);tabs.SwitchTo("shop");yield return Capture("exchange-short-1080x1440");
        var exchangeScroll=shop.GetComponentInChildren<ScrollRect>();Record("Short screen scrolls all exchange products",exchangeScroll.content.rect.height>exchangeScroll.viewport.rect.height);
        changeResolution(1080,1920);tabs.SwitchTo("battle");yield return null;Record("Return to roadmap retained",map.gameObject.activeInHierarchy);tabs.SwitchTo("character");yield return null;Record("Existing character page retained",FindObjectOfType<CharacterSelectionPanel>()!=null);
    }
}
#endif

