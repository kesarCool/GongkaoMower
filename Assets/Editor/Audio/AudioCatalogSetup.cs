using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 扫描 <c>Assets/Res/Audio</c> 按目录写入 <see cref="AudioCatalog"/>，并拷贝到 StreamingAssets / minigame。
/// </summary>
public static class AudioCatalogSetup
{
    private const string CatalogAssetPath = "Assets/Resources/Audio/MainAudioCatalog.asset";
    private const string ResAudioRoot = "Assets/Res/Audio";

    private static readonly string[] AudioExtensions = { ".mp3", ".wav", ".ogg" };

    private static readonly (string fileStem, string combatField)[] CombatFileMap =
    {
        ("sfx_enemy_hit", nameof(AudioCatalog.CombatSection.enemyHit)),
        ("sfx_enemy_die", nameof(AudioCatalog.CombatSection.enemyDie)),
        ("sfx_player_hurt", nameof(AudioCatalog.CombatSection.playerHurt)),
    };

    private static readonly (string fileStem, string skillField)[] SkillFileMap =
    {
        ("sfx_shoot_auto_projectile", nameof(AudioCatalog.PlayerSkillSection.autoProjectile)),
        ("sfx_skill_line_beam", nameof(AudioCatalog.PlayerSkillSection.lineBeam)),
        ("sfx_skill_orbiting_blade", nameof(AudioCatalog.PlayerSkillSection.orbitingBlades)),
        ("sfx_skill_throw_grenade", nameof(AudioCatalog.PlayerSkillSection.throwGrenade)),
        ("sfx_skill_field_generator", nameof(AudioCatalog.PlayerSkillSection.fieldGenerator)),
        ("sfx_skill_lightning", nameof(AudioCatalog.PlayerSkillSection.lightningStrike)),
    };

    [MenuItem("Tools/Audio/创建默认 AudioCatalog", false, 300)]
    public static void CreateDefaultCatalog()
    {
        EnsureDirectory("Assets/Resources/Audio");

        var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(CatalogAssetPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
        }

        var common = new AudioCatalog.CommonSection();
        var combat = new AudioCatalog.CombatSection();
        var skills = new AudioCatalog.PlayerSkillSection();
        var warnings = new List<string>(8);

        string commonDir = ResAudioRoot + "/Common";
        AssignPath(common.uiClick, FindAudioByStem(commonDir, "sfx_button_click"), warnings, "Common/uiClick");
        AssignPath(common.uiClose, FindAudioByStem(commonDir, "sfx_button_close"), warnings, "Common/uiClose");

        string combatDir = ResAudioRoot + "/Battle/Combat";
        foreach ((string stem, string field) in CombatFileMap)
        {
            string path = FindAudioByStem(combatDir, stem);
            if (path == null)
            {
                warnings.Add($"Battle/Combat 未找到 {stem}.*（可稍后补资源）");
                continue;
            }

            AssignPath(GetCombatEntry(combat, field), path, warnings, "Combat/" + field);
        }

        string skillDir = ResAudioRoot + "/Battle/PlayerSkill";
        foreach ((string stem, string field) in SkillFileMap)
        {
            string path = FindAudioByStem(skillDir, stem);
            if (path == null)
            {
                warnings.Add($"Battle/PlayerSkill 未找到 {stem}.*");
                continue;
            }

            AssignPath(GetSkillEntry(skills, field), path, warnings, "PlayerSkill/" + field);
        }

        catalog.ApplySections(common, combat, skills);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        var sb = new StringBuilder();
        sb.AppendLine("[AudioCatalogSetup] 已按目录写入 " + CatalogAssetPath);
        sb.AppendLine("  Common: uiClick=" + common.uiClick.relativePath + ", uiClose=" + common.uiClose.relativePath);
        sb.AppendLine("  Combat: hit=" + NullOrPath(combat.enemyHit) + ", die=" + NullOrPath(combat.enemyDie) +
                      ", hurt=" + NullOrPath(combat.playerHurt));
        sb.AppendLine("  PlayerSkill: 已绑定 " + CountSkillBindings(skills) + " 项");
        if (warnings.Count > 0)
        {
            sb.AppendLine("  警告:");
            for (int i = 0; i < warnings.Count; i++)
                sb.AppendLine("    - " + warnings[i]);
        }

        Debug.Log(sb.ToString());
    }

    [MenuItem("Tools/Audio/拷贝音频到 StreamingAssets 与 Minigame", false, 301)]
    public static void CopyAudioForRuntime()
    {
        AudioBuildCopy.CopyResAudioToStreamingAssets();
        AudioBuildCopy.CopyResAudioToMinigame();
        AssetDatabase.Refresh();
        Debug.Log("[AudioCatalogSetup] 音频已拷贝到 StreamingAssets/Audio 与 Build/minigame/Audio。");
    }

    [MenuItem("Tools/Audio/一键初始化音效", false, 299)]
    public static void SetupAll()
    {
        CreateDefaultCatalog();
        CopyAudioForRuntime();
    }

    private static void AssignPath(AudioCatalog.Entry entry, string relativePath, List<string> warnings, string label)
    {
        if (entry == null) return;
        if (string.IsNullOrEmpty(relativePath))
        {
            entry.relativePath = string.Empty;
            return;
        }

        entry.relativePath = relativePath;
    }

    private static string FindAudioByStem(string folderAssetPath, string fileStem)
    {
        if (string.IsNullOrEmpty(folderAssetPath) || string.IsNullOrEmpty(fileStem))
            return null;

        string folderFull = Path.GetFullPath(folderAssetPath);
        if (!Directory.Exists(folderFull))
            return null;

        for (int i = 0; i < AudioExtensions.Length; i++)
        {
            string fileName = fileStem + AudioExtensions[i];
            string full = Path.Combine(folderFull, fileName);
            if (!File.Exists(full)) continue;

            string assetPath = (folderAssetPath + "/" + fileName).Replace('\\', '/');
            return ToCatalogRelativePath(assetPath);
        }

        return null;
    }

    private static string ToCatalogRelativePath(string assetPath)
    {
        string path = assetPath.Replace('\\', '/');
        const string prefix = "Assets/Res/";
        if (!path.StartsWith(prefix))
            return null;
        return path.Substring(prefix.Length);
    }

    private static AudioCatalog.Entry GetCombatEntry(AudioCatalog.CombatSection section, string fieldName)
    {
        return fieldName switch
        {
            nameof(AudioCatalog.CombatSection.enemyHit) => section.enemyHit,
            nameof(AudioCatalog.CombatSection.enemyDie) => section.enemyDie,
            nameof(AudioCatalog.CombatSection.playerHurt) => section.playerHurt,
            _ => null
        };
    }

    private static AudioCatalog.Entry GetSkillEntry(AudioCatalog.PlayerSkillSection section, string fieldName)
    {
        return fieldName switch
        {
            nameof(AudioCatalog.PlayerSkillSection.autoProjectile) => section.autoProjectile,
            nameof(AudioCatalog.PlayerSkillSection.lineBeam) => section.lineBeam,
            nameof(AudioCatalog.PlayerSkillSection.orbitingBlades) => section.orbitingBlades,
            nameof(AudioCatalog.PlayerSkillSection.throwGrenade) => section.throwGrenade,
            nameof(AudioCatalog.PlayerSkillSection.fieldGenerator) => section.fieldGenerator,
            nameof(AudioCatalog.PlayerSkillSection.lightningStrike) => section.lightningStrike,
            _ => null
        };
    }

    private static int CountSkillBindings(AudioCatalog.PlayerSkillSection s)
    {
        int n = 0;
        if (HasPath(s.autoProjectile)) n++;
        if (HasPath(s.lineBeam)) n++;
        if (HasPath(s.orbitingBlades)) n++;
        if (HasPath(s.throwGrenade)) n++;
        if (HasPath(s.fieldGenerator)) n++;
        if (HasPath(s.lightningStrike)) n++;
        return n;
    }

    private static bool HasPath(AudioCatalog.Entry e) =>
        e != null && !string.IsNullOrWhiteSpace(e.relativePath);

    private static string NullOrPath(AudioCatalog.Entry e) =>
        HasPath(e) ? e.relativePath : "(未配置)";

    private static void EnsureDirectory(string assetDir)
    {
        if (AssetDatabase.IsValidFolder(assetDir)) return;

        string parent = Path.GetDirectoryName(assetDir)?.Replace('\\', '/');
        string name = Path.GetFileName(assetDir);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureDirectory(parent);

        AssetDatabase.CreateFolder(parent, name);
    }
}
