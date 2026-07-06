using System;
using UnityEngine;

/// <summary>
/// 广告 Provider：当前阶段直接回调成功（点击即发放奖励）。
/// 待开通流量主后，接入微信激励视频广告。
/// </summary>
public sealed class WeChatRewardedAdProvider : IReviveAdProvider
{
    public static readonly WeChatRewardedAdProvider Instance = new WeChatRewardedAdProvider();

    public void RequestReviveAd(Action<bool> onComplete)
    {
        GameLog.Info("[WeChatAd] 未接入广告（待开通流量主），模拟完成");
        onComplete?.Invoke(true);
    }
}
