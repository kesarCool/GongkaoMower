#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class TutorialDebugTools
{
    [MenuItem("工具/新手引导/重置引导进度（保留角色与货币）")]
    public static void ResetProgress()
    {
        if (TutorialDirector.Current != null) TutorialDirector.Current.Cancel();
        var profile = PlayerProfileService.Instance;
        profile.LoadOrCreate();
        profile.Data.tutorial = new TutorialProgressData();
        profile.MarkDirtyAndSave();
        Debug.Log("[Tutorial] 已重置新手引导，下次进入大厅时开始；角色、关卡、背包与货币保留。");
    }
}
#endif
