using UnityEditor;
using UnityEngine;

/// <summary>
/// 开发工具：清除本地存档，模拟新号首次启动。
/// 菜单入口：Tools/清除本地存档（模拟新号）
/// </summary>
public static class ClearPlayerPrefs
{
    [MenuItem("Tools/清除本地存档（模拟新号）", false, 200)]
    public static void ClearAll()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("[ClearPlayerPrefs] 已清除所有 PlayerPrefs（存档 + 教程标记 + 游客ID），重启 Play 即模拟新号。");
    }

    [MenuItem("Tools/仅清除关卡存档（保留教程标记）", false, 201)]
    public static void ClearSaveOnly()
    {
        PlayerPrefs.DeleteKey(PlayerProfileService.SaveKey);
        PlayerPrefs.Save();
        Debug.Log("[ClearPlayerPrefs] 已删除关卡存档 key=" + PlayerProfileService.SaveKey);
    }
}
