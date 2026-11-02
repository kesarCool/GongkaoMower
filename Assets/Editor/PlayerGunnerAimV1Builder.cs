#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class PlayerGunnerAimV1Builder
{
    public const string TexturePath = "Assets/Res/image/Game/role/GunnerAimV1/gunner-aim-v1.png";
    public const string FramesPath = "Assets/ScriptableObject/Character/Visual/Character_GunnerAimV1.asset";
    public const string RolePath = "Assets/ScriptableObject/Character/Role/Character_PistolGuy.asset";
    static readonly string[] Names = { "right", "up_right", "up", "down_right", "down" };

    [MenuItem("工具/角色美术/构建弹仔五向整身姿态")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before building assets.");
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 72f;
        var textureSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(textureSettings);
        textureSettings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(textureSettings);
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 512;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.crunchedCompression = false;
        importer.spritesheet = Names.Select((name, i) => new SpriteMetaData {
            name = "gunner_aim_" + name, rect = new Rect(i % 4 * 128, 128 - i / 4 * 128, 128, 128),
            alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f)
        }).ToArray();
        importer.SaveAndReimport();
        var sprites = AssetDatabase.LoadAllAssetsAtPath(TexturePath).OfType<Sprite>().ToDictionary(s => s.name);
        Directory.CreateDirectory(Path.GetDirectoryName(FramesPath));
        AssetDatabase.Refresh();
        var frames = AssetDatabase.LoadAssetAtPath<CharacterAimFrames>(FramesPath);
        if (frames == null) { frames = ScriptableObject.CreateInstance<CharacterAimFrames>(); AssetDatabase.CreateAsset(frames, FramesPath); }
        frames.right = sprites["gunner_aim_right"];
        frames.upRight = sprites["gunner_aim_up_right"];
        frames.up = sprites["gunner_aim_up"];
        frames.downRight = sprites["gunner_aim_down_right"];
        frames.down = sprites["gunner_aim_down"];
        frames.directionHysteresis = 6;
        var role = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(RolePath);
        if (role == null || role.characterId != "character_01") throw new InvalidOperationException("Expected the default gunner role.");
        role.aimFrames = frames;
        role.bodySprite = frames.right;
        role.portrait = frames.right;
        EditorUtility.SetDirty(frames); EditorUtility.SetDirty(role); AssetDatabase.SaveAssets();
        Audit();
    }

    public static string Audit()
    {
        var frames = AssetDatabase.LoadAssetAtPath<CharacterAimFrames>(FramesPath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        var sprites = AssetDatabase.LoadAllAssetsAtPath(TexturePath).OfType<Sprite>().ToArray();
        var role = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(RolePath);
        var catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>("Assets/ScriptableObject/Character/CharacterCatalog.asset");
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        var checks = new[] {
            new { name = "Five complete whole-character frames", passed = frames != null && frames.IsComplete && sprites.Length == 5 },
            new { name = "Only character_01 uses the new aim frames", passed = catalog.characters.All(c => c == role ? c.aimFrames == frames : c.aimFrames == null) },
            new { name = "Body and portrait use the plain-headband short-shotgun image", passed = role.bodySprite == frames.right && role.portrait == frames.right },
            new { name = "One 512x256 runtime texture", passed = texture.width == 512 && texture.height == 256 && sprites.All(s => s.texture == texture) },
            new { name = "Uniform 128px canvas, center pivot and 72 PPU", passed = sprites.All(s => s.rect.width == 128 && s.rect.height == 128 && s.pivot == new Vector2(64, 64) && s.pixelsPerUnit == 72) },
            new { name = "No mipmaps or CPU-readable copy", passed = !importer.mipmapEnabled && !importer.isReadable },
            new { name = "Point filtering and clamp", passed = importer.filterMode == FilterMode.Point && importer.wrapMode == TextureWrapMode.Clamp }
        };
        var report = new { passed = checks.All(c => c.passed), checks, pngBytes = new FileInfo(TexturePath).Length, textureFormatInCurrentEditor = texture.format.ToString(), currentEditorTextureMemoryBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture), note = "Current Editor import check only. No WeChat package or device-memory measurement." };
        Directory.CreateDirectory("ArtOutput/VFX/PlayerGunnerAimV1/Audit");
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText("ArtOutput/VFX/PlayerGunnerAimV1/Audit/config-check.json", json);
        if (!report.passed) throw new InvalidOperationException(json);
        return json;
    }

    [MenuItem("工具/角色美术/验证弹仔八向瞄准")]
    public static void Preview() { HomeUIEditorPreview.Begin("gunner-aim"); }
}
#endif
