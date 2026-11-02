using System;
using System.Runtime.InteropServices;
using UnityEngine;

public static class WXShareHelper
{
    /// <summary>被动转发：设置分享图 ID 池（微信后台素材 ID），每次转发随机取一张。</summary>
    /// <param name="imageUrlIds">素材 ID 列表，如 new[] {"id1","id2"}</param>
    public static void ConfigureShareImages(string[] imageUrlIds)
    {
        if (imageUrlIds == null || imageUrlIds.Length == 0) return;

        // 构造 JSON 数组：["id1","id2"]
        string json = "[";
        for (int i = 0; i < imageUrlIds.Length; i++)
        {
            if (i > 0) json += ",";
            json += "\"" + imageUrlIds[i].Replace("\"", "\\\"") + "\"";
        }
        json += "]";

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            SetShareImageIds(json);
            GameLog.Info($"[WXShareHelper] 转发图片池已配置：{imageUrlIds.Length} 张，json={json}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WXShareHelper] 转发图片配置异常: {ex.Message}");
        }
#else
        GameLog.Info($"[WXShareHelper] 非 WebGL 环境，跳过转发图片配置：{json}");
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void SetShareImageIds(string idsJson);
#endif

    /// <summary>保留旧入口；需要返回结果的奖励业务应调用 RequestShareReturn。</summary>
    public static void ShareForRevive(string title)
    {
        RequestShareReturn(title, null);
    }

    /// <summary>
    /// 发起主动分享，只有 JS 确认调用并观察到小游戏隐藏后返回才完成。
    /// 微信不提供可靠的送达/取消结果；true 表示返回奖励条件成立，不表示分享已送达。
    /// </summary>
    public static void RequestShareReturn(string title, Action<bool> onComplete)
    {
#if UNITY_EDITOR
        if (EditorRequestOverride != null)
        {
            EditorRequestOverride(title, onComplete);
            return;
        }
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
        WXShareRequestRunner.Instance.Request(title, onComplete);
#else
        // 保留 Editor / Standalone 联调能力，正式 WebGL 绝不使用模拟成功。
        GameLog.Info("[WXShareHelper] 开发环境模拟分享返回奖励");
        onComplete?.Invoke(true);
#endif
    }

#if UNITY_EDITOR
    public static Action<string, Action<bool>> EditorRequestOverride;
#endif
}
