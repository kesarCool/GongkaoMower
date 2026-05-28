using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Guest 单存档：关卡星级（历史最高）、最短通关时间、击杀（历史最高）。</summary>
public sealed class PlayerProfileService
{
    public const string SaveKey = "player_save_v1";
    public const string GuestIdKey = "guest_player_id";

    private static PlayerProfileService _instance;
    public static PlayerProfileService Instance => _instance ??= new PlayerProfileService();

    private readonly ISaveStorage _storage;
    private readonly Dictionary<int, LevelProgressEntry> _levels = new Dictionary<int, LevelProgressEntry>();
    private PlayerSaveData _data;
    private bool _loaded;

    public string PlayerId => _data?.playerId ?? string.Empty;
    public bool IsLoaded => _loaded;

    private PlayerProfileService()
    {
        _storage = new PlayerPrefsSaveStorage();
    }

    public void LoadOrCreate()
    {
        if (_loaded) return;

        if (!_storage.TryLoadString(GuestIdKey, out string guestId) || string.IsNullOrEmpty(guestId))
        {
            guestId = "guest_" + Guid.NewGuid().ToString("N");
            _storage.SaveString(GuestIdKey, guestId);
        }

        if (_storage.TryLoad(SaveKey, out string json) && !string.IsNullOrEmpty(json))
        {
            try
            {
                _data = JsonUtility.FromJson<PlayerSaveData>(json);
            }
            catch (Exception e)
            {
                GameErrorPresenter.Show(GameErrorCodes.SaveLoadFailed);
                Debug.LogWarning($"[PlayerProfileService] 存档解析失败，将新建：{e.Message}");
                _data = null;
            }
        }

        if (_data == null)
        {
            _data = new PlayerSaveData
            {
                version = PlayerSaveData.CurrentVersion,
                playerId = guestId,
                levels = Array.Empty<LevelProgressEntry>(),
            };
        }
        else if (string.IsNullOrEmpty(_data.playerId))
        {
            _data.playerId = guestId;
        }

        _levels.Clear();
        if (_data.levels != null)
        {
            for (int i = 0; i < _data.levels.Length; i++)
            {
                var e = _data.levels[i];
                if (e == null || e.levelId <= 0) continue;
                _levels[e.levelId] = e;
            }
        }

        _loaded = true;
        ChapterLevelCatalog.InvalidateCache();
    }

    public bool HasCleared(int levelId)
    {
        return _levels.TryGetValue(levelId, out var e) && e.cleared;
    }

    public bool TryGetProgress(int levelId, out LevelProgressEntry entry)
    {
        return _levels.TryGetValue(levelId, out entry);
    }

    public bool IsLevelUnlocked(int levelId)
    {
        return ChapterLevelUnlockEvaluator.IsLevelUnlocked(levelId, HasCleared);
    }

    /// <summary>胜利结算写入：星级取 max，时长取全局最短，击杀取 max。</summary>
    public void RecordVictory(int levelId, float durationSec, int killCount, int stars)
    {
        if (!_loaded) LoadOrCreate();
        if (levelId <= 0) return;

        stars = Mathf.Clamp(stars, 1, 3);
        durationSec = Mathf.Max(0f, durationSec);
        killCount = Mathf.Max(0, killCount);

        if (!_levels.TryGetValue(levelId, out var entry))
        {
            entry = new LevelProgressEntry { levelId = levelId };
            _levels[levelId] = entry;
        }

        bool wasCleared = entry.cleared;
        entry.cleared = true;
        entry.stars = Mathf.Max(entry.stars, stars);
        if (!wasCleared || entry.bestTimeSec <= 0f)
            entry.bestTimeSec = durationSec;
        else
            entry.bestTimeSec = Mathf.Min(entry.bestTimeSec, durationSec);
        entry.bestKills = Mathf.Max(entry.bestKills, killCount);

        Persist();
    }

    /// <summary>当前上阵角色 ID（持久化到本地存档）。</summary>
    public string EquippedCharacterId
    {
        get
        {
            if (!_loaded) LoadOrCreate();
            return _data?.equippedCharacterId ?? string.Empty;
        }
    }

    /// <summary>设置上阵角色并持久化。</summary>
    public void SetEquippedCharacter(string characterId)
    {
        if (!_loaded) LoadOrCreate();
        if (_data == null) return;
        _data.equippedCharacterId = characterId ?? string.Empty;
        Persist();
    }

    private void Persist()
    {
        var list = new List<LevelProgressEntry>(_levels.Count);
        foreach (var kv in _levels)
            list.Add(kv.Value);
        list.Sort((a, b) => a.levelId.CompareTo(b.levelId));
        _data.levels = list.ToArray();

        string json = JsonUtility.ToJson(_data);
        _storage.Save(SaveKey, json);
    }
}
