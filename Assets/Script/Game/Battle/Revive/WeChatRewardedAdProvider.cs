using System;

/// <summary>
/// 奖励入口：未开通流量主时发起分享，观察到返回后才发奖。
/// 返回不等于确认送达；后续真实激励广告仍可替换此 Provider。
/// </summary>
public sealed class WeChatRewardedAdProvider : IReviveAdProvider
{
    public static readonly WeChatRewardedAdProvider Instance = new WeChatRewardedAdProvider();

    public void RequestReviveAd(Action<bool> onComplete)
    {
        RequestShareReward("快来一起玩文字割草机！", onComplete);
    }

    public void RequestShareReward(string title, Action<bool> onComplete) =>
        WXShareHelper.RequestShareReturn(title, onComplete);
}
