using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 多色文字地图 → Tilemap。字符映射：
/// . = 地面  # = 暗色  @ = 金色  + = 天蓝  * = 橙色  ~ = 青绿
/// </summary>
public static class TextMapToTilemap
{
    [MenuItem("Tools/文字地图铺图", false, 221)]
    public static void Run()
    {
        var txt = Selection.activeObject as TextAsset;
        if (txt == null)
        {
            EditorUtility.DisplayDialog("缺少 txt", "请先在 Project 窗口选中一个文字地图 .txt 文件。", "确定");
            return;
        }

        var grid = Object.FindObjectOfType<Grid>();
        if (grid == null)
        {
            EditorUtility.DisplayDialog("缺少 Grid", "Scene 中未找到 Grid。请先建好 Tilemap。", "确定");
            return;
        }

        var tilemap = grid.GetComponentInChildren<Tilemap>();
        if (tilemap == null)
        {
            EditorUtility.DisplayDialog("缺少 Tilemap", "Grid 下未找到 Tilemap。", "确定");
            return;
        }

        TileBase existing = FindFirstTile(tilemap);
        TextMapWindow.Open(txt, tilemap, existing);
    }

    [MenuItem("Tools/文字地图铺图", true)]
    public static bool Validate() => Selection.activeObject is TextAsset && !EditorApplication.isPlaying;

    private static TileBase FindFirstTile(Tilemap tm)
    {
        BoundsInt bounds = tm.cellBounds;
        for (int y = bounds.yMin; y < bounds.yMax; y++)
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                var t = tm.GetTile(new Vector3Int(x, y, 0));
                if (t != null) return t;
            }
        return null;
    }

    public static void Execute(TextAsset txt, Tilemap tilemap,
        TileBase tileGround, TileBase tileDark, TileBase tileGold,
        TileBase tileBlue, TileBase tileOrange, TileBase tileCyan)
    {
        Undo.RecordObject(tilemap, "Text Map To Tilemap");
        tilemap.ClearAllTiles();

        string[] lines = txt.text.Split('\n');
        int placed = 0;

        for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            string line = lines[lineIdx].TrimEnd('\r', '\n');
            int y = lines.Length - 1 - lineIdx;

            for (int x = 0; x < line.Length; x++)
            {
                char c = line[x];
                TileBase tile = c switch
                {
                    '.' => tileGround,
                    '#' => tileDark,
                    '@' => tileGold,
                    '+' => tileBlue,
                    '*' => tileOrange,
                    '~' => tileCyan,
                    _ => null
                };
                if (tile == null) continue;
                tilemap.SetTile(new Vector3Int(x, y, 0), tile);
                placed++;
            }
        }

        tilemap.CompressBounds();
        EditorUtility.SetDirty(tilemap);
        Debug.Log($"[TextMapToTilemap] {txt.name} → {tilemap.name}, {placed} 瓦片");
        EditorUtility.DisplayDialog("完成", $"已放置 {placed} 个瓦片。", "确定");
    }

    // ── 窗口 ──

    private class TextMapWindow : EditorWindow
    {
        private TextAsset _txt;
        private Tilemap _tilemap;
        private TileBase _tileGround, _tileDark, _tileGold, _tileBlue, _tileOrange, _tileCyan;

        public static void Open(TextAsset txt, Tilemap tilemap, TileBase existing)
        {
            var w = GetWindow<TextMapWindow>(true, "文字地图 → Tilemap (多色)");
            w._txt = txt;
            w._tilemap = tilemap;
            w._tileGround = existing;
            w._tileDark = existing;
            w._tileGold = existing;
            w._tileBlue = existing;
            w._tileOrange = existing;
            w._tileCyan = existing;
            w.minSize = new Vector2(380, 340);
            w.Show();
        }

        private void OnGUI()
        {
            if (_txt == null || _tilemap == null) { Close(); return; }

            EditorGUILayout.LabelField($"蓝图: {_txt.name}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"目标: {_tilemap.name}");
            EditorGUILayout.Space();

            _tileGround = (TileBase)EditorGUILayout.ObjectField(". 地面", _tileGround, typeof(TileBase), false);
            _tileDark   = (TileBase)EditorGUILayout.ObjectField("# 暗色", _tileDark, typeof(TileBase), false);
            _tileGold   = (TileBase)EditorGUILayout.ObjectField("@ 金色", _tileGold, typeof(TileBase), false);
            _tileBlue   = (TileBase)EditorGUILayout.ObjectField("+ 天蓝", _tileBlue, typeof(TileBase), false);
            _tileOrange = (TileBase)EditorGUILayout.ObjectField("* 橙色", _tileOrange, typeof(TileBase), false);
            _tileCyan   = (TileBase)EditorGUILayout.ObjectField("~ 青绿", _tileCyan, typeof(TileBase), false);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("若某色未拖入瓦片，对应字符会被跳过。\n单色地图只需填 . 和 # 两个。", MessageType.Info);

            bool canRun = _tileGround != null;
            EditorGUI.BeginDisabledGroup(!canRun);
            if (GUILayout.Button("铺地图", GUILayout.Height(36)))
            {
                Execute(_txt, _tilemap, _tileGround, _tileDark, _tileGold, _tileBlue, _tileOrange, _tileCyan);
                Close();
            }
            EditorGUI.EndDisabledGroup();
        }
    }
}
