#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class BossArenaWallArtBuilder
{
    const string Atlas = "Assets/Res/image/Game/arena/boss_wall_atlas_v1.png";
    const string Prefab = "Assets/Prefab/Battle/boss_arena_wall_v1.prefab";
    const string GameLayer = "Assets/Prefab/Battle/GameLayer.prefab";

    [MenuItem("Tools/Battle Art/Apply Boss Arena Wall V1")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before applying wall art.");
        AssetDatabase.ImportAsset(Atlas, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(Atlas);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 96;
        var textureSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(textureSettings);
        textureSettings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(textureSettings);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 512;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.spritesheet = new[]
        {
            Slice("boss_wall_horizontal", new Rect(0,192,256,64), new Vector2(.5f,.5f)),
            Slice("boss_wall_vertical", new Rect(256,0,64,256), new Vector2(.5f,.5f)),
            Slice("boss_wall_post", new Rect(320,112,96,144), new Vector2(.5f,.17f))
        };
        importer.SaveAndReimport();
        var sprites = AssetDatabase.LoadAllAssetsAtPath(Atlas).OfType<Sprite>().ToDictionary(s=>s.name);
        var root = new GameObject("boss_arena_wall_v1");
        try
        {
            var art = root.AddComponent<BossArenaWallVisual>();
            art.horizontalSprite = sprites["boss_wall_horizontal"];
            art.verticalSprite = sprites["boss_wall_vertical"];
            art.postSprite = sprites["boss_wall_post"];
            var wall = PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            var game = PrefabUtility.LoadPrefabContents(GameLayer);
            try
            {
                var arena = game.GetComponentInChildren<BossArenaLock>(true);
                if (arena == null) throw new InvalidDataException("BossArenaLock is missing from GameLayer.");
                arena.wallPrefab = wall;
                PrefabUtility.SaveAsPrefabAsset(game, GameLayer);
            }
            finally { PrefabUtility.UnloadPrefabContents(game); }
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        Debug.Log("[Boss wall art] Applied directional wall art from " + Atlas);
    }

    static SpriteMetaData Slice(string name, Rect rect, Vector2 pivot)
    {
        return new SpriteMetaData {name=name,rect=rect,pivot=pivot,alignment=(int)SpriteAlignment.Custom};
    }
}
#endif
