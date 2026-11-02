#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class BossArenaFenceV2Builder
{
    [MenuItem("Tools/Battle Art/Apply Boss Arena Fence V2")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode before applying fence art.");
        const string path="Assets/Res/image/Game/arena/boss_fence_atlas_v2.png";
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit=96;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
        importer.isReadable=false;importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;
        importer.maxTextureSize=512;importer.textureCompression=TextureImporterCompression.Compressed;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
        settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
        importer.spritesheet=new[]{
            new SpriteMetaData{name="fence_horizontal",rect=new Rect(0,192,256,64),pivot=new Vector2(.5f,.5f),alignment=9},
            new SpriteMetaData{name="fence_vertical",rect=new Rect(256,0,64,256),pivot=new Vector2(.5f,.5f),alignment=9}};
        importer.SaveAndReimport();
        var sprites=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s=>s.name);
        var root=new GameObject("boss_arena_fence_v2");
        try
        {
            var view=root.AddComponent<BossArenaWallVisual>();
            view.horizontalSprite=sprites["fence_horizontal"];view.verticalSprite=sprites["fence_vertical"];
            view.postSprite=null;
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefab/Battle/boss_arena_fence_v2.prefab");
            const string gamePath="Assets/Prefab/Battle/GameLayer.prefab";
            var game=PrefabUtility.LoadPrefabContents(gamePath);
            try{game.GetComponentInChildren<BossArenaLock>(true).wallPrefab=prefab;PrefabUtility.SaveAsPrefabAsset(game,gamePath);}
            finally{PrefabUtility.UnloadPrefabContents(game);}
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
        Debug.Log("[Boss fence V2] Applied steel mesh with barbed wire, no decorative posts.");
    }
}
#endif
