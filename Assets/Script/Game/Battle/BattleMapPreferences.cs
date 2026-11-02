using System;
using UnityEngine;

/// <summary>Local display preference, shared across battles, like the existing audio settings.</summary>
public static class BattleMapPreferences
{
    public const string ShowDecorationKey = "GongkaoMower.Battle.ShowDecoration";
    public static event Action<bool> DecorationVisibilityChanged;

    public static bool ShowDecoration
    {
        get => PlayerPrefs.GetInt(ShowDecorationKey, 1) != 0;
        set
        {
            if (ShowDecoration == value) return;
            PlayerPrefs.SetInt(ShowDecorationKey, value ? 1 : 0);
            PlayerPrefs.Save();
            DecorationVisibilityChanged?.Invoke(value);
        }
    }
}
