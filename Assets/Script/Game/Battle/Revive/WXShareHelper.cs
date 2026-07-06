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
    [DllImport("__Internal__")]
    private static extern void SetShareImageIds(string idsJson);
#endif

    /// <summary>主动分享（已知不可用：SDK 缺少 WX_OneWayNoFunction_vt，待 SDK 升级后恢复）。</summary>
    public static void ShareForRevive(string title)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            var option = new WeChatWASM.ShareAppMessageOption
            {
                title = title ?? "救我复活！",
            };
            WeChatWASM.WX.ShareAppMessage(option);
            GameLog.Info("[WXShareHelper] ShareAppMessage 已调用");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WXShareHelper] 分享调用异常: {ex.Message}");
        }
#else
        GameLog.Info("[WXShareHelper] 非 WebGL 环境，跳过分享");
#endif
    }
}
