using System;

/// <summary>
/// 看广告复活接口；接入微信/穿山甲时实现本接口并注入 <see cref="GameRevivePanelPayload"/>。
/// </summary>
public interface IReviveAdProvider
{
    void RequestReviveAd(Action<bool> onComplete);
}

/// <summary>占位：不播广告，直接视为成功（便于联调与 Editor）。</summary>
public sealed class DefaultReviveAdProvider : IReviveAdProvider
{
    public static readonly DefaultReviveAdProvider Instance = new DefaultReviveAdProvider();

    public void RequestReviveAd(Action<bool> onComplete)
    {
        onComplete?.Invoke(true);
    }
}
