#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class BossArenaBarrelBuilder
{
    [MenuItem("Tools/Battle Art/Apply Boss Arena Barrels")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
        const string path="Assets/Res/Map/Styles/SharedCartoon/Textures/barrel.png";
        const string prefabPath="Assets/Prefab/Battle/boss_arena_barrel.prefab";
        const string oldPrefabPath="Assets/Prefab/Battle/boss_arena_stone_v3.prefab";
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if(sprite==null)throw new InvalidOperationException("Missing shared barrel sprite: "+path);
        if(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)==null)
        {
            string error=AssetDatabase.MoveAsset(oldPrefabPath,prefabPath);
            if(!string.IsNullOrEmpty(error))throw new InvalidOperationException(error);
        }
        var root=PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            root.name="boss_arena_barrel";
            var view=root.GetComponent<BossArenaWallVisual>();
            view.tileSprite=sprite;view.tilePitch=.5f;
            view.tileScale=.5f/sprite.bounds.size.x;
            view.tileMaterial=AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            view.horizontalSprite=view.verticalSprite=view.postSprite=null;
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
            const string gamePath="Assets/Prefab/Battle/GameLayer.prefab";
            if(AssetDatabase.LoadAssetAtPath<GameObject>(gamePath).GetComponentInChildren<BossArenaLock>(true).wallPrefab!=prefab)
            {
                var game=PrefabUtility.LoadPrefabContents(gamePath);
                try{game.GetComponentInChildren<BossArenaLock>(true).wallPrefab=prefab;PrefabUtility.SaveAsPrefabAsset(game,gamePath);}
                finally{PrefabUtility.UnloadPrefabContents(game);}
            }
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        Debug.Log("[Boss barrels] Shared barrel sprite repeated at 0.5-unit size/pitch; four existing colliders retained. Shared texture importer unchanged.");
    }
}
#endif
