#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Real reward transactions and character feedback, with isolated profile and gift-code storage.</summary>
public sealed class ProgressionFeedbackValidationRunner : UIValidationRunnerBase
{
    protected override string OutputFolder => "ProgressionFeedback";
    protected override string ValidationNote => "Actual Home tabs, business services and UI callbacks; isolated in-memory profile, gift-code claim storage and controllable Editor ad callbacks. No real ad request, persistent preference write, WeChat device or package-size validation. Intermediate screenshots render the actual running animation.";
    const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    const string GiftHashes="gift_code_claimed_hashes",GiftDate="gift_code_last_claim_date";
    ISaveStorage previousGiftStorage;
    Action<Action<bool>> previousAdRequest;
    string giftHashesBefore,giftDateBefore;
    bool giftHashesHad,giftDateHad,seamsCaptured;
    string receiptPrefix;
    ShopView shop;
    CharacterSelectionPanel character;
    bool previousBackground;
    void Awake() { previousBackground=Application.runInBackground;Application.runInBackground=true; }
    PlayerProfileService Profile => PlayerProfileService.Instance;
    RewardRevealQueue Queue => RewardRevealQueue.Instance;
    static T Field<T>(object value,string name) => (T)value.GetType().GetField(name,Fields).GetValue(value);
    static void Call(object value,string method,params object[] args) => value.GetType().GetMethod(method,Fields).Invoke(value,args);
    static string Plain(string text) => Regex.Replace(text??"","<[^>]+>","");
    List<ShopCell> ShopCells => Field<List<ShopCell>>(shop,"_cells");
    void Buy(ShopCell cell) => Field<Button>(cell,"btnBuy").onClick.Invoke();
    string Wallet() => JsonUtility.ToJson(Profile.Data);
    string Receipt(string suffix) => receiptPrefix+suffix;
    bool CurrentItem(int item,int count,string source) => Queue.IsShowing&&Queue.Current.Request.source==source
        &&Queue.Current.Request.items.Count==1&&Queue.Current.Request.items[0].itemId==item&&Queue.Current.Request.items[0].count==count;

    protected override IEnumerator RunChecks()
    {
        // Flatten our nested checks so exceptions still reach UIValidationRunnerBase's report writer.
        var stack=new Stack<IEnumerator>();stack.Push(Checks());float deadline=Time.realtimeSinceStartup+180;
        while(stack.Count>0)
        {
            if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("Progression feedback validation exceeded 180 seconds.");
            var flow=stack.Peek();
            if(!flow.MoveNext()){(flow as IDisposable)?.Dispose();stack.Pop();continue;}
            if(flow.Current is IEnumerator nested){stack.Push(nested);continue;}
            yield return flow.Current;
        }
    }
    IEnumerator Checks()
    {
        previousGiftStorage=GiftCodeService.EditorStorageOverride;previousAdRequest=ShopView.EditorAdRequestOverride;
        giftHashesHad=PlayerPrefs.HasKey(GiftHashes);giftDateHad=PlayerPrefs.HasKey(GiftDate);
        giftHashesBefore=PlayerPrefs.GetString(GiftHashes,"");giftDateBefore=PlayerPrefs.GetString(GiftDate,"");
        seamsCaptured=true;GiftCodeService.EditorStorageOverride=new MemoryClaims();
        receiptPrefix="feedback-preview:"+Guid.NewGuid().ToString("N")+":";
        fixture.SetProgress(21);AchievementService.Instance.MarkDirty();hub.RefreshCurrencyHud();
        yield return CheckQueue();
        yield return CheckShop();
        yield return CheckAchievement();
        yield return CheckGiftCode();
        yield return CheckGrowth();
        yield return DrainRewards();UIManager.Instance.CloseAllStack();changeResolution(1080,1920);
        Record("Real gift-code preference history remains untouched",GiftPrefsUnchanged());
        RestoreSeams();
    }
    IEnumerator Until(Func<bool> condition,string description,float seconds=5)
    {
        float deadline=Time.realtimeSinceStartup+seconds;
        while(!condition()&&Time.realtimeSinceStartup<deadline)yield return null;
        if(!condition())throw new TimeoutException(description);
    }
    IEnumerator RewardShown(string source)
    {
        yield return Until(()=>Queue.IsShowing&&Queue.Current.Request!=null&&Queue.Current.Request.source==source,"Reward popup did not open: "+source);
    }
    IEnumerator DrainRewards()
    {
        float deadline=Time.realtimeSinceStartup+8;
        while((Queue.IsShowing||Queue.PendingCount>0)&&Time.realtimeSinceStartup<deadline)
        {
            if(Queue.IsShowing){Queue.SkipCurrent();Queue.CloseCurrent();}
            yield return null;
        }
        if(Queue.IsShowing||Queue.PendingCount>0)throw new TimeoutException("Reward queue did not drain.");
        yield return null;
    }
    IEnumerator CaptureNow(string filename)
    {
        var toast=FindObjectOfType<UiToastPanel>();if(toast!=null)toast.HideImmediate();
        Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(output,filename+".png"));yield return null;yield return null;
    }
    IEnumerator CheckQueue()
    {
        string before=Wallet();var catalog=Field<CharacterCatalog>(hub,"characterCatalog");
        var locked=catalog.characters.First(c=>c!=null&&!CharacterUnlockEvaluator.IsUnlocked(c));
        var items=new List<RewardItemEntry>{RewardRevealRequest.Item(1,25),RewardRevealRequest.Item(2,8),RewardRevealRequest.Item(locked.fragmentItemId,3)};
        UIManager.Instance.ShowConfirm("验证队列等待","此确认框关闭后展示已到账奖励。",_=>{});
        bool first=RewardRevealQueue.ShowItem(Receipt("queue-a"),"validation",1,37);
        bool duplicate=RewardRevealQueue.ShowItem(Receipt("queue-a"),"validation",1,37);
        bool second=RewardRevealQueue.ShowItems(Receipt("queue-b"),"validation",items,"补给到账");
        bool third=RewardRevealQueue.ShowCharacter(Receipt("queue-c"),"validation",locked.characterId);
        items[0].count=999;items.Clear();yield return null;
        Record("Reward queue defers to confirmation and deduplicates receipts",first&&!duplicate&&second&&third&&!Queue.IsShowing&&Queue.PendingCount==3);
        UIManager.Instance.CloseConfirm();yield return RewardShown("validation");
        Record("Queued receipts preserve order without granting rewards",Queue.Current.Request.receiptId==Receipt("queue-a")&&CurrentItem(1,37,"validation")&&Wallet()==before);
        Queue.SkipCurrent();
        Record("Skip reveals the saved receipt and cannot grant it again",!Queue.Current.IsRevealing&&Wallet()==before);
        Queue.CloseCurrent();yield return Until(()=>Queue.IsShowing&&Queue.Current.Request.receiptId==Receipt("queue-b"),"Second receipt did not follow the first.");
        Queue.SkipCurrent();
        Record("Multi-item reward keeps an independent copy of real item metadata",Queue.Current.VisibleItemCount==3&&Queue.Current.Request.items.Count==3
            &&Queue.Current.Request.items[0].count==25&&Queue.Current.Request.items[2].itemId==locked.fragmentItemId&&Wallet()==before);
        yield return Capture("reward-items-1080x1920");
        changeResolution(1080,2400);yield return Capture("reward-items-long-1080x2400");
        var popup=Queue.Current;
        Record("Long-screen reward surface stays within its canvas",popup.panel.rect.height<=((RectTransform)popup.transform).rect.height&&popup.panel.rect.width<=((RectTransform)popup.transform).rect.width);
        Queue.CloseCurrent();yield return Until(()=>Queue.IsShowing&&Queue.Current.Request.receiptId==Receipt("queue-c"),"Character receipt did not follow items.");
        Queue.SkipCurrent();
        Record("Character reveal does not unlock a character or alter the wallet",Queue.Current.Request.characterId==locked.characterId&&!CharacterUnlockEvaluator.IsUnlocked(locked)&&Wallet()==before);
        yield return Capture("reward-character-long-1080x2400");
        Queue.CloseCurrent();yield return DrainRewards();changeResolution(1080,1920);
        Record("Closed receipts stay deduplicated and close never grants rewards",!RewardRevealQueue.ShowItem(Receipt("queue-a"),"validation",1,37)&&Wallet()==before);
    }
    IEnumerator CheckShop()
    {
        tabs.SwitchTo("shop");yield return Until(()=>FindObjectOfType<ShopView>()!=null,"Shop page missing.");shop=FindObjectOfType<ShopView>();
        var free=ShopCells.First(c=>c.ShopRow.PriceType==0);var row=free.ShopRow;
        int have=Profile.GetItemCount(row.ItemID),purchases=Profile.GetShopPurchaseCount(row.ID);
        Buy(free);Buy(free);yield return RewardShown("shop");
        Record("Free claim delivers the configured item once despite rapid clicks",Profile.GetItemCount(row.ItemID)==have+Mathf.Max(1,row.ItemNumber)
            &&Profile.GetShopPurchaseCount(row.ID)==purchases+1&&CurrentItem(row.ItemID,Mathf.Max(1,row.ItemNumber),"shop"));
        string granted=Wallet();yield return Capture("shop-free-reward-1080x1920");yield return DrainRewards();
        Record("Closing a purchased reward leaves the already-saved result unchanged",Wallet()==granted);
        var ad=ShopCells.First(c=>c.ShopRow.PriceType==3);var adRow=ad.ShopRow;
        int requests=0;Action<bool> callback=null;
        ShopView.EditorAdRequestOverride=complete=>{requests++;callback=complete;};
        have=Profile.GetItemCount(adRow.ItemID);purchases=Profile.GetShopPurchaseCount(adRow.ID);
        Buy(ad);Buy(ad);
        Record("Pending ad request is gated before any grant",requests==1&&shop.IsAdRewardInFlight&&Profile.GetItemCount(adRow.ItemID)==have&&!Queue.IsShowing);
        callback(false);callback(true);yield return null;
        Record("Failed ad and its duplicate callback grant no reward",!shop.IsAdRewardInFlight&&Profile.GetItemCount(adRow.ItemID)==have&&Profile.GetShopPurchaseCount(adRow.ID)==purchases&&!Queue.IsShowing&&Queue.PendingCount==0);
        Buy(ShopCells.First(c=>c.ShopRow.ID==adRow.ID));
        shop.OnTabRefresh();tabs.SwitchTo("battle");yield return null;callback(true);callback(true);callback(false);
        yield return RewardShown("shop");
        Record("Delayed ad success survives destroyed cells, tab changes and duplicate callbacks",requests==2&&!shop.IsAdRewardInFlight
            &&Profile.GetItemCount(adRow.ItemID)==have+Mathf.Max(1,adRow.ItemNumber)&&Profile.GetShopPurchaseCount(adRow.ID)==purchases+1
            &&CurrentItem(adRow.ItemID,Mathf.Max(1,adRow.ItemNumber),"shop"));
        yield return DrainRewards();tabs.SwitchTo("shop");yield return null;
        var paid=ShopCells.First(c=>c.ShopRow.PriceType==2&&!ShopService.IsSoldOut(c.ShopRow));var paidRow=paid.ShopRow;
        int diamonds=Profile.Diamond,itemCount=Profile.GetItemCount(paidRow.ItemID);
        Buy(paid);yield return Until(()=>FindObjectOfType<UiConfirmDialog>()!=null,"Purchase confirmation missing.");
        FindObjectOfType<UiConfirmDialog>().cancelButton.onClick.Invoke();yield return null;
        Record("Canceling a purchase produces no reward and no spending",Profile.Diamond==diamonds&&Profile.GetItemCount(paidRow.ItemID)==itemCount&&Queue.PendingCount==0&&!Queue.IsShowing);
        Buy(ShopCells.First(c=>c.ShopRow.ID==paidRow.ID));yield return Until(()=>FindObjectOfType<UiConfirmDialog>()!=null,"Purchase confirmation missing on retry.");
        var confirm=FindObjectOfType<UiConfirmDialog>();confirm.okButton.onClick.Invoke();confirm.okButton.onClick.Invoke();
        yield return RewardShown("shop");
        Record("Confirmed exchange spends once and reveals its real item",Profile.Diamond==diamonds-ShopService.GetPrice(paidRow)
            &&Profile.GetItemCount(paidRow.ItemID)==itemCount+Mathf.Max(1,paidRow.ItemNumber)&&CurrentItem(paidRow.ItemID,Mathf.Max(1,paidRow.ItemNumber),"shop"));
        yield return DrainRewards();
    }
    IEnumerator CheckAchievement()
    {
        var groups=AchievementService.Instance.GetAchievementGroups();
        var group=groups.FirstOrDefault(g=>g.stages.Any(s=>s.rewardId!=2));if(group.stages==null)group=groups.First();
        var stage=group.stages.Any(s=>s.rewardId!=2)?group.stages.First(s=>s.rewardId!=2):group.stages.OrderBy(s=>s.stage).First();
        Profile.Data.achievementProgress=new[]{new AchievementProgressEntry{groupId=group.groupId,currentValue=stage.targetValue,claimedStage=stage.stage-1}};
        Profile.MarkDirtyAndSave();AchievementService.Instance.MarkDirty();tabs.SwitchTo("achievement");yield return null;yield return null;
        var page=FindObjectOfType<AchievementPanel>();int have=Profile.GetItemCount(stage.rewardId);
        Call(page,"OnCellClaimClicked",group.groupId,stage.stage);yield return RewardShown("achievement");
        Record("Achievement reveal uses the configured item ID and exact saved count",Profile.GetItemCount(stage.rewardId)==have+stage.rewardCount
            &&CurrentItem(stage.rewardId,stage.rewardCount,"achievement")&&!AchievementService.Instance.CanClaim(group.groupId,stage.stage),"Actual configured item ID: "+stage.rewardId);
        var failed=AchievementService.Instance.Claim(group.groupId,stage.stage);
        Record("Repeated achievement claim cannot issue another reward",!failed.success&&Profile.GetItemCount(stage.rewardId)==have+stage.rewardCount&&Queue.PendingCount==0);
        yield return Capture("achievement-reward-1080x1920");yield return DrainRewards();
    }
    IEnumerator CheckGiftCode()
    {
        var settings=UIManager.Instance.Open<SettingsPanel>(null,UiOpenOptions.NonPausingModal);
        yield return Until(()=>settings.gameObject.activeInHierarchy,"Settings did not open.");
        var input=Field<InputField>(settings,"giftCodeInput");var claim=Field<Button>(settings,"claimButton");int diamonds=Profile.Diamond;
        input.text="feedback-preview-invalid";claim.onClick.Invoke();yield return null;
        Record("Invalid gift code grants nothing and enqueues nothing",Profile.Diamond==diamonds&&!Queue.IsShowing&&Queue.PendingCount==0&&GiftCodeService.CanClaimToday);
        input.text=" wenzigecao666 ";claim.onClick.Invoke();claim.onClick.Invoke();yield return RewardShown("gift_code");
        Record("Gift redemption commits once and reveals the actual gift reward",Profile.Diamond==diamonds+100&&CurrentItem(2,100,"gift_code")&&!GiftCodeService.CanClaimToday&&!claim.interactable);
        string granted=Wallet();Queue.SkipCurrent();Queue.CloseCurrent();yield return DrainRewards();
        Record("Gift popup dismissal cannot redeem the code again",Wallet()==granted&&GiftCodeService.TryRedeem("WENZIGECAO666")!=null&&GiftPrefsUnchanged());
        UIManager.Instance.CloseAllStack();yield return null;
    }
    IEnumerator CheckGrowth()
    {
        fixture.SetProgress(21);Profile.AddGold(200000);AchievementService.Instance.MarkDirty();hub.RefreshCurrencyHud();
        tabs.SwitchTo("character");yield return Until(()=>FindObjectOfType<CharacterSelectionPanel>()!=null,"Character page missing.");character=FindObjectOfType<CharacterSelectionPanel>();
        var catalog=Field<CharacterCatalog>(character,"characterCatalog");
        var knife=catalog.characters.First(c=>c.startingSkill==SkillId.AutoProjectileSword);
        var pistol=catalog.characters.First(c=>c.startingSkill==SkillId.AutoProjectilePistol);
        character.SelectCharacter(knife.characterId);character.SetStatsView(true);yield return null;
        var preview=character.AttributeRows.Select(r=>Plain(Field<TMP_Text>(r,"nextText").text)).ToArray();
        var beforeLabels=character.AttributeRows.Select(r=>r.DisplayedText).ToArray();
        int level=Profile.GetHeroLevel(knife.characterId),gold=Profile.Gold,cost=knife.upgradeData.GetCostForLevel(level+1);
        float began=Time.realtimeSinceStartup;
        Field<Button>(character,"upgradeButton").onClick.Invoke();Field<Button>(character,"upgradeButton").onClick.Invoke();
        var feedback=character.GrowthFeedback;
        Record("Upgrade button commits exactly one real level and one cost",Profile.GetHeroLevel(knife.characterId)==level+1&&Profile.Gold==gold-cost&&character.IsGrowthFeedbackPlaying);
        Record("Growth keeps a complete immutable before and after snapshot",feedback.Before.CharacterId==knife.characterId&&feedback.Before.Level==level
            &&feedback.After.CharacterId==knife.characterId&&feedback.After.Level==level+1&&feedback.Before.Stats.Length==9&&feedback.After.Stats.Length==9
            &&feedback.Before.Stats.Select(s=>s.FormattedValue).SequenceEqual(beforeLabels)&&feedback.After.Stats.Select(s=>s.FormattedValue).SequenceEqual(preview));
        yield return Until(()=>Time.realtimeSinceStartup-began>=.2f,"Upgrade did not advance.");
        bool rolling=character.AttributeRows.Where((r,i)=>Mathf.Abs(feedback.After.Stats[i].Value-feedback.Before.Stats[i].Value)>.0001f)
            .Any(r=>r.IsAnimating&&Mathf.Abs(r.DisplayedValue-r.TargetValue)>.0001f);
        bool unchangedQuiet=character.AttributeRows.Select((r,i)=>Mathf.Abs(feedback.After.Stats[i].Value-feedback.Before.Stats[i].Value)<=.0001f?string.IsNullOrEmpty(r.DeltaText):true).All(v=>v);
        Record("Real upgrade rolls changing values while unchanged rows stay quiet",rolling&&unchangedQuiet);
        yield return Until(()=>Time.realtimeSinceStartup-began>=.5f,"Upgrade capture time not reached.");
        File.WriteAllText(Path.Combine(output,"upgrade-middle-frame.txt"),"Actual elapsed before capture: "+(Time.realtimeSinceStartup-began).ToString("0.000")+" seconds; phase="+feedback.Phase);
        yield return CaptureNow("upgrade-middle-approximately-0_5s-1080x1920");
        character.SkipGrowthFeedback();
        Record("Skip leaves precise final values and cannot charge another upgrade",RowsMatch(feedback.After)&&!character.IsGrowthFeedbackPlaying&&Profile.Gold==gold-cost&&Profile.GetHeroLevel(knife.characterId)==level+1);
        yield return Until(()=>!Field<bool>(character,"_upgradeCooldown"),"Upgrade click gate did not settle.");
        int knifeBefore=Profile.GetHeroLevel(knife.characterId),pistolBefore=Profile.GetHeroLevel(pistol.characterId);
        bool next=character.TryUpgradeSelected();character.SelectCharacter(pistol.characterId);yield return null;
        Record("Switching characters settles the old feedback without cross-playing",next&&Profile.GetHeroLevel(knife.characterId)==knifeBefore+1
            &&Profile.GetHeroLevel(pistol.characterId)==pistolBefore&&character.SelectedCharacterId==pistol.characterId&&!character.IsGrowthFeedbackPlaying
            &&RowsMatch(CharacterGrowthSnapshot.Capture(pistol)));
        yield return Until(()=>!Field<bool>(character,"_upgradeCooldown"),"Character switch left the click gate locked.");
        bool viewUpgrade=character.TryUpgradeSelected();character.SetStatsView(false);yield return null;
        Record("Switching to the skill view settles numbers without leaking upgrade effects",viewUpgrade&&character.IsSkillView
            &&!character.IsGrowthFeedbackPlaying&&Profile.GetHeroLevel(pistol.characterId)==pistolBefore+1&&RowsMatch(CharacterGrowthSnapshot.Capture(pistol)));
        character.SetStatsView(true);
        bool pageUpgrade=character.TryUpgradeSelected();tabs.SwitchTo("battle");yield return null;tabs.SwitchTo("character");yield return null;
        Record("Leaving and reopening the tab shows saved final values without replay",pageUpgrade&&Profile.GetHeroLevel(pistol.characterId)==pistolBefore+2
            &&character.SelectedCharacterId==pistol.characterId&&!character.IsGrowthFeedbackPlaying&&RowsMatch(CharacterGrowthSnapshot.Capture(pistol)));
        character.SetStatsView(false);int fragments=Profile.GetFragmentCount(pistol.characterId),stage=Profile.GetHeroStage(pistol.characterId);
        bool denied=character.TryPromoteSelected();
        Record("Unmet promotion requirements cannot trigger an unlock animation",!denied&&!character.IsGrowthFeedbackPlaying&&Profile.GetHeroStage(pistol.characterId)==stage&&Profile.GetFragmentCount(pistol.characterId)==fragments);
        SetHero(knife,knife.upgradeData.rareRequiredLevel,0,knife.upgradeData.rareFragmentCost+7);
        character.SelectCharacter(knife.characterId);character.SetStatsView(false);yield return null;
        var presentation=character.GetComponent<CharacterPanelPresentation>();
        bool promoted=character.TryPromoteSelected(),duplicatePromotion=character.TryPromoteSelected();feedback=character.GrowthFeedback;
        Record("Promotion saves one stage and consumes fragments exactly once",promoted&&!duplicatePromotion&&Profile.GetHeroStage(knife.characterId)==1&&Profile.GetFragmentCount(knife.characterId)==7
            &&feedback.Before.Stage==0&&feedback.After.Stage==1);
        Record("Charge preserves the old lock before the reveal",feedback.Phase=="Charge"&&presentation.tierStates[0].text!="已解锁");
        yield return Until(()=>feedback.Phase=="Reveal"||!feedback.IsPlaying,"Promotion did not reach reveal.");
        Record("Reveal unlocks the correct tier and configured ability",feedback.Phase=="Reveal"&&feedback.RevealProgress<1
            &&presentation.tierStates[0].text=="已解锁"&&feedback.After.UnlockedAbility==knife.upgradeData.rareTraitDescription);
        yield return CaptureNow("promotion-reveal-1080x1920");
        character.SkipGrowthFeedback();
        Record("Promotion skip keeps the final unlock and cannot consume more fragments",!character.IsGrowthFeedbackPlaying&&Profile.GetHeroStage(knife.characterId)==1
            &&Profile.GetFragmentCount(knife.characterId)==7&&presentation.tierStates[0].text=="已解锁");
        SetHero(knife,knife.upgradeData.legendRequiredLevel,1,knife.upgradeData.legendFragmentCost+9);
        character.SelectCharacter(knife.characterId);character.SetStatsView(false);bool legend=character.TryPromoteSelected();
        character.SelectCharacter(pistol.characterId);yield return null;
        Record("Switching during promotion cannot reveal another character's skill",legend&&!character.IsGrowthFeedbackPlaying&&character.SelectedCharacterId==pistol.characterId
            &&presentation.tierStates.All(t=>t.text!="已解锁")&&Profile.GetHeroStage(knife.characterId)==2&&Profile.GetFragmentCount(knife.characterId)==9);
        tabs.SwitchTo("battle");yield return null;tabs.SwitchTo("character");yield return null;character.SelectCharacter(knife.characterId);character.SetStatsView(false);
        Record("Reopening a promoted hero renders the saved unlocks without replay",!character.IsGrowthFeedbackPlaying&&presentation.tierStates.All(t=>t.text=="已解锁"));
        changeResolution(1080,2400);yield return Capture("promotion-final-long-1080x2400");
        SetHero(knife,knife.upgradeData.maxLevel,2,9);character.SelectCharacter(knife.characterId);character.SetStatsView(true);
        gold=Profile.Gold;bool maxed=character.TryUpgradeSelected();
        Record("Maximum-level upgrade is a no-op without celebratory feedback",!maxed&&Profile.Gold==gold&&!character.IsGrowthFeedbackPlaying);
        Profile.SpendGold(Profile.Gold);character.SelectCharacter(pistol.characterId);character.SetStatsView(true);
        int pistolLevel=Profile.GetHeroLevel(pistol.characterId);bool poor=character.TryUpgradeSelected();
        Record("Insufficient gold does not start an upgrade or change the saved level",!poor&&!character.IsGrowthFeedbackPlaying&&Profile.Gold==0&&Profile.GetHeroLevel(pistol.characterId)==pistolLevel);
        var storage=Field<ISaveStorage>(Profile,"_storage");bool saved=storage.TryLoad(PlayerProfileService.SaveKey,out string json);
        var reloaded=saved?JsonUtility.FromJson<PlayerSaveData>(json):null;
        Record("Final progression is present in the isolated serialized save",reloaded!=null&&reloaded.heroUpgrades.Any(h=>h.characterId==knife.characterId&&h.stage==2&&h.level==knife.upgradeData.maxLevel));
        changeResolution(1080,1920);yield return Capture("growth-final-1080x1920");
    }
    bool RowsMatch(CharacterGrowthSnapshot snapshot) => character.AttributeRows.Count==snapshot.Stats.Length
        &&character.AttributeRows.Select((r,i)=>!r.IsAnimating&&Mathf.Abs(r.DisplayedValue-snapshot.Stats[i].Value)<.0001f
            &&Mathf.Abs(r.TargetValue-snapshot.Stats[i].Value)<.0001f&&r.DisplayedText==snapshot.Stats[i].FormattedValue).All(v=>v);
    void SetHero(CharacterDefinition def,int level,int stage,int fragments)
    {
        character.SkipGrowthFeedback();
        var entries=Profile.Data.heroUpgrades.ToList();var entry=entries.FirstOrDefault(e=>e.characterId==def.characterId);
        if(entry==null){entry=new HeroUpgradeEntry{characterId=def.characterId};entries.Add(entry);}
        entry.level=level;entry.stage=stage;Profile.Data.heroUpgrades=entries.ToArray();
        var keys=(Profile.Data.characterFragmentKeys??Array.Empty<string>()).ToList();var values=(Profile.Data.characterFragmentValues??Array.Empty<int>()).ToList();
        int index=keys.IndexOf(def.characterId);if(index<0){keys.Add(def.characterId);values.Add(fragments);}else values[index]=fragments;
        Profile.Data.characterFragmentKeys=keys.ToArray();Profile.Data.characterFragmentValues=values.ToArray();
        var ids=Profile.Data.itemIds.ToList();var counts=Profile.Data.itemCounts.ToList();index=ids.IndexOf(def.fragmentItemId);
        if(index<0){ids.Add(def.fragmentItemId);counts.Add(fragments);}else counts[index]=fragments;
        Profile.Data.itemIds=ids.ToArray();Profile.Data.itemCounts=counts.ToArray();Profile.MarkDirtyAndSave();character.OnOpen(null);
    }
    bool GiftPrefsUnchanged() => PlayerPrefs.HasKey(GiftHashes)==giftHashesHad&&PlayerPrefs.HasKey(GiftDate)==giftDateHad
        &&PlayerPrefs.GetString(GiftHashes,"")==giftHashesBefore&&PlayerPrefs.GetString(GiftDate,"")==giftDateBefore;
    void RestoreSeams()
    {
        if(!seamsCaptured)return;GiftCodeService.EditorStorageOverride=previousGiftStorage;ShopView.EditorAdRequestOverride=previousAdRequest;seamsCaptured=false;
    }
    protected override void OnDestroy(){RestoreSeams();base.OnDestroy();Application.runInBackground=previousBackground;}
    sealed class MemoryClaims : ISaveStorage
    {
        readonly Dictionary<string,string> values=new Dictionary<string,string>();
        public bool TryLoad(string key,out string value)=>values.TryGetValue(key,out value);
        public void Save(string key,string value)=>values[key]=value;
        public bool TryLoadString(string key,out string value)=>values.TryGetValue(key,out value);
        public void SaveString(string key,string value)=>values[key]=value;
    }
}
#endif
