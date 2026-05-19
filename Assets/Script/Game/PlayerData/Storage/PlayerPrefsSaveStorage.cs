using UnityEngine;

/// <summary>使用 <see cref="PlayerPrefs"/> 的本地存储实现。</summary>
public sealed class PlayerPrefsSaveStorage : ISaveStorage
{
    public bool TryLoad(string key, out string json)
    {
        json = PlayerPrefs.GetString(key, string.Empty);
        return !string.IsNullOrEmpty(json);
    }

    public void Save(string key, string json)
    {
        PlayerPrefs.SetString(key, json);
        PlayerPrefs.Save();
    }

    public bool TryLoadString(string key, out string value)
    {
        value = PlayerPrefs.GetString(key, string.Empty);
        return !string.IsNullOrEmpty(value);
    }

    public void SaveString(string key, string value)
    {
        PlayerPrefs.SetString(key, value);
        PlayerPrefs.Save();
    }
}
