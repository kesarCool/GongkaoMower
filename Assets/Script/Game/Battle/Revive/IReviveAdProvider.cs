using System;

/// <summary>
/// 复活奖励接口；保留广告接入契约，当前默认实现使用分享返回奖励。
/// </summary>
public interface IReviveAdProvider
{
    void RequestReviveAd(Action<bool> onComplete);
}

/// <summary>兼容旧注入入口，统一进入平台 Provider；开发环境模拟由 WXShareHelper 控制。</summary>
public sealed class DefaultReviveAdProvider : IReviveAdProvider
{
    public static readonly DefaultReviveAdProvider Instance = new DefaultReviveAdProvider();

    public void RequestReviveAd(Action<bool> onComplete)
    {
        WeChatRewardedAdProvider.Instance.RequestReviveAd(onComplete);
    }
}
