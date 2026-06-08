using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 像素图 → Tilemap 生成器。菜单入口：Tools/像素图铺地图
/// 用法：
///   1. 选中一张像素 PNG（TextureType=Sprite, Read/Write Enabled）
///   2. Scene 里选中有 Tilemap 子节点的 Grid
///   3. 给每个像素色值指定对应 TileBase
/// </summary>
public static class PixelMapToTilemap
{
    [MenuItem("Tools/像素图铺地图", false, 220)]
    public static void Run()
    {
        // 1. 获取源贴图路径
        var selectedObj = Selection.activeObject;
        if (selectedObj == null)
        {
            EditorUtility.DisplayDialog("缺少贴图", "请先在 Project 窗口选中一张用作蓝图的 PNG 图片。", "确定");
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(selectedObj);
        if (string.IsNullOrEmpty(assetPath))
        {
            EditorUtility.DisplayDialog("错误", "请选中 Project 窗口中的 PNG 文件。", "确定");
            return;
        }

        // 直接读文件字节，不需要 Read/Write Enabled
        byte[] fileBytes = File.ReadAllBytes(assetPath);
        var src = new Texture2D(2, 2);
        src.LoadImage(fileBytes);

        // 2. 获取目标 Grid → Tilemap
        var grid = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponentInChildren<Grid>()
            : null;
        if (grid == null)
        {
            grid = Object.FindObjectOfType<Grid>();
        }
        if (grid == null)
        {
            EditorUtility.DisplayDialog("缺少 Grid",
                "请在 Scene 中选中一个包含 Grid + Tilemap 的 GameObject。", "确定");
            return;
        }

        var tilemap = grid.GetComponentInChildren<Tilemap>();
        if (tilemap == null)
        {
            EditorUtility.DisplayDialog("缺少 Tilemap",
                "Grid 下未找到 Tilemap 组件。", "确定");
            return;
        }

        // 3. 让用户绑定颜色→Tile
        var colors = CollectUniqueColors(src);
        if (colors == null || colors.Length == 0)
        {
            EditorUtility.DisplayDialog("无有效像素", "贴图无有效像素数据。", "确定");
            return;
        }

        Debug.Log($"[PixelMapToTilemap] 源图={src.name} 尺寸={src.width}x{src.height} 唯一色={colors.Length}");
        foreach (var c in colors)
            Debug.Log($"  色值 #{ColorUtility.ToHtmlStringRGB(c)}");

        // 打开设置窗口
        PixelMapWindow.Open(src, tilemap, colors);
    }

    [MenuItem("Tools/像素图铺地图", true)]
    public static bool Validate() => Selection.activeObject is Texture2D && !EditorApplication.isPlaying;

    private static Color32[] CollectUniqueColors(Texture2D tex)
    {
        if (tex == null) return null;
        Color32[] pixels = tex.GetPixels32();
        var set = new System.Collections.Generic.HashSet<Color32>();
        for (int i = 0; i < pixels.Length; i++)
            if (pixels[i].a > 128) // 忽略透明像素
                set.Add(pixels[i]);
        var arr = new Color32[set.Count];
        set.CopyTo(arr);
        return arr;
    }

    /// <summary>执行铺地图</summary>
    public static void Execute(Texture2D src, Tilemap tilemap, System.Collections.Generic.Dictionary<Color32, TileBase> mapping)
    {
        Undo.RecordObject(tilemap, "Pixel Map To Tilemap");

        Color32[] pixels = src.GetPixels32();
        int w = src.width;
        int h = src.height;

        int placed = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color32 pixel = pixels[y * w + x];
                if (pixel.a <= 128) continue;
                if (!mapping.TryGetValue(pixel, out var tile) || tile == null) continue;

                // Y 翻转（像素原点在左下，Tilemap 原点也在左下）
                tilemap.SetTile(new Vector3Int(x, h - 1 - y, 0), tile);
                placed++;
            }
        }

        EditorUtility.SetDirty(tilemap);
        Debug.Log($"[PixelMapToTilemap] 完成！已放置 {placed} 个瓦片。");
    }

    // ── 设置窗口 ──

    private class PixelMapWindow : EditorWindow
    {
        private Texture2D _src;
        private Tilemap _tilemap;
        private Color32[] _colors;
        private TileBase[] _tileAssignments;
        private Vector2 _scroll;

        public static void Open(Texture2D src, Tilemap tilemap, Color32[] colors)
        {
            var w = GetWindow<PixelMapWindow>(true, "像素颜色 → 瓦片映射");
            w._src = src;
            w._tilemap = tilemap;
            w._colors = colors;
            w._tileAssignments = new TileBase[colors.Length];
            w.minSize = new Vector2(400, 300);
            w.Show();
        }

        private void OnGUI()
        {
            if (_src == null || _tilemap == null)
            {
                Close();
                return;
            }

            EditorGUILayout.LabelField($"蓝图: {_src.name} ({_src.width}×{_src.height})", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"目标: {_tilemap.name} ({_tilemap.transform.parent?.name ?? "无Grid"})");
            EditorGUILayout.Space();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _colors.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                // 色块
                Rect r = GUILayoutUtility.GetRect(24, 24, GUILayout.Width(24));
                EditorGUI.DrawRect(r, _colors[i]);
                EditorGUILayout.LabelField($"#{ColorUtility.ToHtmlStringRGB(_colors[i])}", GUILayout.Width(80));
                _tileAssignments[i] = (TileBase)EditorGUILayout.ObjectField(_tileAssignments[i], typeof(TileBase), false);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            if (GUILayout.Button("铺地图", GUILayout.Height(36)))
            {
                var mapping = new System.Collections.Generic.Dictionary<Color32, TileBase>();
                for (int i = 0; i < _colors.Length; i++)
                {
                    if (_tileAssignments[i] != null)
                        mapping[_colors[i]] = _tileAssignments[i];
                }

                if (mapping.Count == 0)
                {
                    EditorUtility.DisplayDialog("未映射", "请至少为一个颜色指定对应的瓦片。", "确定");
                    return;
                }

                Execute(_src, _tilemap, mapping);
                Close();
            }
        }
    }
}
