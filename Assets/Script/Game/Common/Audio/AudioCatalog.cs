using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 音效目录：按 <c>Assets/Res/Audio</c> 目录分区配置，运行时仍按 <see cref="AudioId"/> 索引。
/// </summary>
[CreateAssetMenu(menuName = "Game/Audio/Audio Catalog", fileName = "MainAudioCatalog")]
public sealed class AudioCatalog : ScriptableObject
{
    public const string DefaultResourcesPath = "Audio/MainAudioCatalog";

    [Serializable]
    public sealed class Entry
    {
        public AudioId id = AudioId.None;
        [Tooltip("相对路径，如 Audio/Common/sfx_button_click.mp3（不含 Assets/Res）。")]
        public string relativePath;
        public AudioLoadGroup group = AudioLoadGroup.Common;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("同一 Id 两次播放的最小间隔（秒），0 表示不限制。技能音建议 0，由施放事件节流。")]
        public float minInterval;
        [Tooltip("音高随机范围：0 表示不变，0.1 表示 ±10% 随机。受击/连射音建议 0.08~0.12。")]
        [Range(0f, 0.3f)] public float pitchVariation;
        [Tooltip("音高偏移：-0.1 表示整体低沉10%。死亡音建议 -0.1。")]
        [Range(-0.3f, 0.3f)] public float pitchOffset;
        [Tooltip("音量随机范围：0 表示固定，0.15 表示 ±15% 波动。连射音建议 0.15 打破单调感。")]
        [Range(0f, 0.3f)] public float volumeVariation;
    }

    [Header("Common（首包 UI）")]
    [SerializeField] private CommonSection common = new CommonSection();

    [Header("Battle/Combat（受击 / 死亡 / 主角受伤）")]
    [SerializeField] private CombatSection combat = new CombatSection();

    [Header("Battle/PlayerSkill（技能施放）")]
    [SerializeField] private PlayerSkillSection playerSkill = new PlayerSkillSection();

    private Dictionary<AudioId, Entry> _byId;

    [Serializable]
    public sealed class CommonSection
    {
        public Entry uiClick = new Entry { id = AudioId.UiClick, group = AudioLoadGroup.Common };
        public Entry uiClose = new Entry { id = AudioId.UiClose, group = AudioLoadGroup.Common };
    }

    [Serializable]
    public sealed class CombatSection
    {
        public Entry enemyHit = new Entry { id = AudioId.EnemyHit, group = AudioLoadGroup.Battle, minInterval = 0.08f };
        public Entry enemyDie = new Entry { id = AudioId.EnemyDie, group = AudioLoadGroup.Battle };
        public Entry playerHurt = new Entry { id = AudioId.PlayerHurt, group = AudioLoadGroup.Battle, minInterval = 0.12f };
    }

    [Serializable]
    public sealed class PlayerSkillSection
    {
        public Entry autoProjectile = new Entry { id = AudioId.SkillAutoProjectile, group = AudioLoadGroup.Battle };
        public Entry lineBeam = new Entry { id = AudioId.SkillLineBeam, group = AudioLoadGroup.Battle };
        public Entry orbitingBlades = new Entry { id = AudioId.SkillOrbitingBlades, group = AudioLoadGroup.Battle };
        public Entry throwGrenade = new Entry { id = AudioId.SkillThrowGrenade, group = AudioLoadGroup.Battle };
        public Entry fieldGenerator = new Entry { id = AudioId.SkillFieldGenerator, group = AudioLoadGroup.Battle };
        public Entry lightningStrike = new Entry { id = AudioId.SkillLightningStrike, group = AudioLoadGroup.Battle };
        public Entry autoProjectileTalisman = new Entry { id = AudioId.SkillAutoProjectileTalisman, group = AudioLoadGroup.Battle };
    }

    public CommonSection Common => common;
    public CombatSection Combat => combat;
    public PlayerSkillSection PlayerSkill => playerSkill;

    /// <summary>SkillId → AudioId 映射（原来的 SkillAudioMapping）。</summary>
    public static AudioId ResolveSkillAudioId(SkillId skillId)
    {
        switch (skillId)
        {
            case SkillId.AutoProjectile:  return AudioId.SkillAutoProjectile;
            case SkillId.LineBeam:        return AudioId.SkillLineBeam;
            case SkillId.OrbitingBlades:  return AudioId.SkillOrbitingBlades;
            case SkillId.ThrowGrenade:    return AudioId.SkillThrowGrenade;
            case SkillId.FieldGenerator:  return AudioId.SkillFieldGenerator;
            case SkillId.LightningStrike: return AudioId.SkillLightningStrike;
            case SkillId.AutoProjectilePistol: return AudioId.SkillAutoProjectile;
            case SkillId.AutoProjectileSword: return AudioId.SkillAutoProjectile;
            case SkillId.AutoProjectileTalisman: return AudioId.SkillAutoProjectileTalisman;
            default:                      return AudioId.None;
        }
    }

    public bool TryGet(AudioId id, out Entry entry)
    {
        EnsureCache();
        if (_byId != null && _byId.TryGetValue(id, out entry))
            return true;
        entry = null;
        return false;
    }

    public IEnumerable<Entry> EnumerateGroup(AudioLoadGroup group)
    {
        foreach (Entry e in EnumerateAll())
        {
            if (e != null && e.id != AudioId.None && e.group == group && !string.IsNullOrWhiteSpace(e.relativePath))
                yield return e;
        }
    }

    public void ApplySections(CommonSection commonSection, CombatSection combatSection, PlayerSkillSection skillSection)
    {
        common = commonSection ?? new CommonSection();
        combat = combatSection ?? new CombatSection();
        playerSkill = skillSection ?? new PlayerSkillSection();
        SyncEntryIds();
        _byId = null;
    }

    private void SyncEntryIds()
    {
        if (common != null)
        {
            if (common.uiClick != null) { common.uiClick.id = AudioId.UiClick; common.uiClick.group = AudioLoadGroup.Common; }
            if (common.uiClose != null) { common.uiClose.id = AudioId.UiClose; common.uiClose.group = AudioLoadGroup.Common; }
        }

        if (combat != null)
        {
            if (combat.enemyHit != null) { combat.enemyHit.id = AudioId.EnemyHit; combat.enemyHit.group = AudioLoadGroup.Battle; }
            if (combat.enemyDie != null) { combat.enemyDie.id = AudioId.EnemyDie; combat.enemyDie.group = AudioLoadGroup.Battle; }
            if (combat.playerHurt != null) { combat.playerHurt.id = AudioId.PlayerHurt; combat.playerHurt.group = AudioLoadGroup.Battle; }
        }

        if (playerSkill != null)
        {
            if (playerSkill.autoProjectile != null) { playerSkill.autoProjectile.id = AudioId.SkillAutoProjectile; playerSkill.autoProjectile.group = AudioLoadGroup.Battle; }
            if (playerSkill.lineBeam != null) { playerSkill.lineBeam.id = AudioId.SkillLineBeam; playerSkill.lineBeam.group = AudioLoadGroup.Battle; }
            if (playerSkill.orbitingBlades != null) { playerSkill.orbitingBlades.id = AudioId.SkillOrbitingBlades; playerSkill.orbitingBlades.group = AudioLoadGroup.Battle; }
            if (playerSkill.throwGrenade != null) { playerSkill.throwGrenade.id = AudioId.SkillThrowGrenade; playerSkill.throwGrenade.group = AudioLoadGroup.Battle; }
            if (playerSkill.fieldGenerator != null) { playerSkill.fieldGenerator.id = AudioId.SkillFieldGenerator; playerSkill.fieldGenerator.group = AudioLoadGroup.Battle; }
            if (playerSkill.lightningStrike != null) { playerSkill.lightningStrike.id = AudioId.SkillLightningStrike; playerSkill.lightningStrike.group = AudioLoadGroup.Battle; }
            if (playerSkill.autoProjectileTalisman != null) { playerSkill.autoProjectileTalisman.id = AudioId.SkillAutoProjectileTalisman; playerSkill.autoProjectileTalisman.group = AudioLoadGroup.Battle; }
        }
    }

    private IEnumerable<Entry> EnumerateAll()
    {
        if (common != null)
        {
            if (common.uiClick != null) yield return common.uiClick;
            if (common.uiClose != null) yield return common.uiClose;
        }

        if (combat != null)
        {
            if (combat.enemyHit != null) yield return combat.enemyHit;
            if (combat.enemyDie != null) yield return combat.enemyDie;
            if (combat.playerHurt != null) yield return combat.playerHurt;
        }

        if (playerSkill != null)
        {
            if (playerSkill.autoProjectile != null) yield return playerSkill.autoProjectile;
            if (playerSkill.lineBeam != null) yield return playerSkill.lineBeam;
            if (playerSkill.orbitingBlades != null) yield return playerSkill.orbitingBlades;
            if (playerSkill.throwGrenade != null) yield return playerSkill.throwGrenade;
            if (playerSkill.fieldGenerator != null) yield return playerSkill.fieldGenerator;
            if (playerSkill.lightningStrike != null) yield return playerSkill.lightningStrike;
        }
    }

    private void EnsureCache()
    {
        if (_byId != null) return;
        _byId = new Dictionary<AudioId, Entry>(16);
        foreach (Entry e in EnumerateAll())
        {
            if (e == null || e.id == AudioId.None) continue;
            _byId[e.id] = e;
        }
    }

    private void OnValidate() => SyncEntryIds();

    private void OnEnable()
    {
        SyncEntryIds();
        _byId = null;
    }
}
