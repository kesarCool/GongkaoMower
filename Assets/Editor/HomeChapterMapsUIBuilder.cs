#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using U = HomeUIBuilderUtility;

public static class HomeChapterMapsUIBuilder
{
    private const string Art = "Assets/Res/image/Home/roadmap/";
    private const string PrefabPath = "Assets/Prefab/Home/RoadmapView.prefab";

    [MenuItem("工具/Home UI/Apply Seasonal Chapter Maps")]
    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save edited scenes before updating chapter maps.");
        U.Prepare("home-chapter-map");
        foreach (string name in new[] { "map_autumn_academy_720x1280.jpg", "map_winter_town_720x1280.jpg" })
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(Art + name);
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = true; importer.compressionQuality = 70;
            var web = importer.GetPlatformTextureSettings("WebGL");
            web.name = "WebGL"; web.overridden = true; web.maxTextureSize = 2048;
            web.format = TextureImporterFormat.DXT1Crunched; web.crunchedCompression = true; web.compressionQuality = 70;
            importer.SetPlatformTextureSettings(web); importer.SaveAndReimport();
        }
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var map = root.GetComponent<HomeChapterMapView>();
            map.mapBackground = map.world.Find("MapBackground").GetComponent<Image>();
            var foodPoints = new[] { new Vector2(288,1473), new Vector2(635,1342), new Vector2(598,1082), new Vector2(437,960), new Vector2(660,793), new Vector2(567,651), new Vector2(713,490), new Vector2(695,265) };
            var layout = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("Tools/UI/home-chapter-map-assets.json"));
            map.chapterThemes = new[]
            {
                new HomeChapterMapView.ChapterMapTheme { chapterId = 1, background = U.Sprite("map_food_background_1080x1920.jpg", Art), points = foodPoints },
                new HomeChapterMapView.ChapterMapTheme { chapterId = 2, background = U.Sprite("map_autumn_academy_720x1280.jpg", Art), points = Points(layout, 2) },
                new HomeChapterMapView.ChapterMapTheme { chapterId = 3, background = U.Sprite("map_winter_town_720x1280.jpg", Art), points = Points(layout, 3) }
            };
            map.mapBackground.sprite = map.chapterThemes[0].background;
            EditorUtility.SetDirty(map); PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return "Seasonal Home maps configured: chapter 2 autumn, chapter 3 winter. Existing scene references inherit the prefab update.";
    }

    private static Vector2[] Points(Newtonsoft.Json.Linq.JObject layout, int chapter)
    {
        var entry = layout["chapters"].First(c => (int)c["id"] == chapter);
        return entry["points"].Select(p => new Vector2((float)p[0], (float)p[1])).ToArray();
    }
}
#endif
