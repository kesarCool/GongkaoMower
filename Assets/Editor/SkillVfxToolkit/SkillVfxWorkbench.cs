#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GongkaoMower.VfxAuthoring
{
    public sealed class SkillVfxWorkbench : EditorWindow
    {
        VfxRecipe[] recipes = new VfxRecipe[0];
        int selected, background, overlap;
        float time, speed = 1, halfHeight = 4, radius;
        bool playing = true, showWords = true;
        double lastTick;
        string message = "", recipeFile = "", sourceFile = "", search = "";
        Vector2 listScroll, detailScroll;
        Texture2D backgroundImage, ownedBackground;
        SkillVfxPreview preview;
        readonly Color[] backgrounds = { new Color(.94f,.85f,.60f), new Color(.42f,.65f,.32f), new Color(.07f,.09f,.12f) };
        VfxRecipe Selected => recipes.Length == 0 ? null : recipes[Mathf.Clamp(selected, 0, recipes.Length - 1)];

        [MenuItem("工具/VFX/技能特效工作台")]
        public static void Open() { var window = GetWindow<SkillVfxWorkbench>("技能特效工作台"); window.minSize = new Vector2(900, 700); }
        [MenuItem("工具/VFX/检查特效配置库")]
        public static void Audit() { Debug.Log(SkillVfxPipeline.AuditLibrary()); }
        void OnEnable() { lastTick = EditorApplication.timeSinceStartup; Reload(); EditorApplication.update += Tick; }
        void OnDisable() { EditorApplication.update -= Tick; Release(); if (ownedBackground != null) DestroyImmediate(ownedBackground); }
        void Release() { preview?.Dispose(); preview = null; }
        void Reload()
        {
            try { recipes = SkillVfxPipeline.ReadLibrary(); selected = Mathf.Clamp(selected, 0, Math.Max(0, recipes.Length - 1)); ResetPreview(); }
            catch (Exception ex) { message = ex.Message; }
        }
        void ResetPreview()
        {
            Release(); time = .001f;
            if (Selected == null) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Selected.prefabPath);
            if (prefab == null) { message = "Prefab 不存在：" + Selected.prefabPath; return; }
            preview = new SkillVfxPreview(prefab, new[] { 1, 4, 8 }[overlap], Selected.referenceRadius > 0 && radius > 0 ? radius / Selected.referenceRadius : 1);
        }
        void Tick()
        {
            double now = EditorApplication.timeSinceStartup; float dt = Mathf.Min(.1f, (float)(now - lastTick)); lastTick = now;
            if (playing && Selected != null) { time += dt * speed; if (time > Selected.poolSeconds) time = .001f; Repaint(); }
        }
        void Run(Action action) { try { action(); } catch (Exception ex) { message = ex.Message; Debug.LogWarning(ex.Message); } }
        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("刷新配置库", EditorStyles.toolbarButton)) Reload();
                if (GUILayout.Button("检查资源与预算", EditorStyles.toolbarButton)) Run(() => { var report = SkillVfxPipeline.AuditLibrary(); message = report.Contains("\"passed\": false") ? "检查发现问题，详见 ArtOutput/VFX/Toolkit/library-audit.json" : "配置、资源和导入设置检查通过。"; });
                if (GUILayout.Button("打开制作手册", EditorStyles.toolbarButton)) EditorUtility.OpenWithDefaultApp(SkillVfxPipeline.Full("Tools/VFX/README.md"));
                GUILayout.FlexibleSpace(); GUILayout.Label("编辑器工具 · 草稿导入不绑定技能");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(245)))
                {
                    search = EditorGUILayout.TextField("查找", search); listScroll = EditorGUILayout.BeginScrollView(listScroll);
                    for (int i = 0; i < recipes.Length; i++)
                    {
                        if (!string.IsNullOrEmpty(search) && !(recipes[i].id + recipes[i].displayName + recipes[i].usage).Contains(search)) continue;
                        if (GUILayout.Toggle(selected == i, recipes[i].displayName, "Button") && selected != i) { selected = i; radius = 0; ResetPreview(); }
                    }
                    EditorGUILayout.EndScrollView();
                    GUILayout.Label("新增效果", EditorStyles.boldLabel);
                    if (GUILayout.Button("罡气护体（常驻预览）")) PersistentAuraPreviewWindow.Open();
                    if (GUILayout.Button("选择配方 JSON")) { string p = EditorUtility.OpenFilePanel("选择配方", SkillVfxPipeline.Full("Tools/VFX/Library"), "json"); if (p != "") recipeFile = p; }
                    EditorGUILayout.LabelField(Path.GetFileName(recipeFile));
                    if (GUILayout.Button("选择原稿 / 正式图集 PNG")) { string p = EditorUtility.OpenFilePanel("选择透明 PNG", SkillVfxPipeline.Full("ArtOutput/VFX"), "png"); if (p != "") sourceFile = p; }
                    EditorGUILayout.LabelField(Path.GetFileName(sourceFile));
                    if (GUILayout.Button("清空 PNG（复用配方材质）")) sourceFile = "";
                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(recipeFile) || EditorApplication.isPlayingOrWillChangePlaymode))
                    {
                        if (GUILayout.Button("导入为新草稿")) Run(() => { var prefab = SkillVfxPipeline.Import(SkillVfxPipeline.Read(recipeFile), sourceFile); message = "草稿已导入，下一步检查预览并实战验收。"; Reload(); selected = Array.FindIndex(recipes, r => r.prefabPath == AssetDatabase.GetAssetPath(prefab)); ResetPreview(); EditorGUIUtility.PingObject(prefab); });
                    }
                    EditorGUILayout.HelpBox("PNG 留空可复用现有图集和材质。导入要求新 id；不会覆盖已有资源。", MessageType.Info);
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
                    if (Selected != null) DrawSelected(); else EditorGUILayout.HelpBox("配置库尚无配方。", MessageType.Info);
                    if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
                    EditorGUILayout.EndScrollView();
                }
            }
        }
        void DrawSelected()
        {
            var r = Selected;
            GUILayout.Label(r.displayName + "  /  " + r.id, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(r.usage, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("状态", r.status);
            EditorGUILayout.LabelField("图集", r.width + "×" + r.height + "，" + r.columns + "×" + r.rows + " 格，" + r.layers.Length + " 层");
            EditorGUILayout.LabelField("时长", string.Join(" / ", r.layers.Select(l => l.seconds.ToString("0.###") + "s")) + "；回池 " + r.poolSeconds.ToString("0.###") + "s");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("定位 Prefab")) EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(r.prefabPath));
                if (GUILayout.Button("导出变体配方")) Run(() => {
                    string p = EditorUtility.SaveFilePanel("导出后编辑新 id 与参数", SkillVfxPipeline.Full("Tools/VFX/Library"), r.id + "_variant.json", "json");
                    if (p == "") return; var clone = SkillVfxPipeline.Read(SkillVfxPipeline.Full(SkillVfxPipeline.RecipeFolder + "/" + r.id + ".json"));
                    clone.id += "_variant"; clone.displayName += "（变体草稿）"; clone.prefabPath = ""; clone.status = "draft-needs-combat-validation"; clone.validationReport = "";
                    File.WriteAllText(p, SkillVfxPipeline.Json(clone)); recipeFile = p; sourceFile = ""; message = "已导出；可编辑 JSON 后复用图集导入。";
                });
                if (GUILayout.Button("导出当前预览 PNG")) Run(() => { string p = SkillVfxPipeline.Full("ArtOutput/VFX/Toolkit/Previews/" + r.id + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png"); preview.SavePng(p, time, backgrounds[background], halfHeight); message = p; });
            }
            EditorGUI.BeginChangeCheck();
            background = GUILayout.Toolbar(background, new[] { "浅沙", "草地", "深色" });
            int newOverlap = GUILayout.Toolbar(overlap, new[] { "单个", "4 个叠加", "8 个叠加" });
            float newRadius = EditorGUILayout.FloatField("实际技能半径（0=原尺寸）", radius);
            if (newOverlap != overlap || !Mathf.Approximately(newRadius, radius)) { overlap = newOverlap; radius = Mathf.Max(0, newRadius); ResetPreview(); }
            showWords = EditorGUILayout.Toggle("文字可读性对照（示意）", showWords);
            var chosenImage = (Texture2D)EditorGUILayout.ObjectField("地图截图对照（可选）", backgroundImage, typeof(Texture2D), false);
            if (chosenImage != backgroundImage) { if (ownedBackground != null) DestroyImmediate(ownedBackground); ownedBackground = null; backgroundImage = chosenImage; }
            if (GUILayout.Button("从文件载入地图截图（不导入 Assets）")) Run(() => {
                string p = EditorUtility.OpenFilePanel("地图截图", SkillVfxPipeline.Full("ArtOutput/UI"), "png"); if (p == "") return;
                var bytes = File.ReadAllBytes(p); if (bytes.Length > 16 * 1024 * 1024) throw new InvalidOperationException("地图截图不超过 16 MiB。");
                var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!image.LoadImage(bytes)) { DestroyImmediate(image); throw new InvalidOperationException("截图 PNG 无法解码。"); }
                if (ownedBackground != null) DestroyImmediate(ownedBackground); ownedBackground = image; backgroundImage = image;
            });
            halfHeight = EditorGUILayout.Slider("视野半高（世界单位）", halfHeight, 1, 9);
            playing = EditorGUILayout.Toggle("循环播放", playing); speed = EditorGUILayout.Slider("预览速度", speed, .25f, 2);
            time = EditorGUILayout.Slider("采样时间（秒）", time, 0, r.poolSeconds);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("前一帧")) Step(-1);
                if (GUILayout.Button("后一帧")) Step(1);
                if (GUILayout.Button("重播")) { time = .001f; playing = true; }
            }
            var rect = GUILayoutUtility.GetRect(300, Mathf.Min(390, position.height * .48f), GUILayout.ExpandWidth(true));
            if (preview != null && Event.current.type == EventType.Repaint)
            {
                preview.Sample(time);
                if (backgroundImage != null) GUI.DrawTexture(rect, backgroundImage, ScaleMode.ScaleAndCrop);
                GUI.DrawTexture(rect, preview.Render(rect, backgroundImage == null ? backgrounds[background] : Color.clear, halfHeight), ScaleMode.StretchToFill, true);
                if (showWords) GUI.Label(new Rect(rect.center.x - 64, rect.center.y + rect.height * .1f, 220, 50), "词语怪\n伤害 123", new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.black }, fontSize = 20 });
            }
            EditorGUILayout.LabelField("隔离预览：不触发伤害、对象池与限流；实战验收使用菜单「工具/VFX/实战验收」。", EditorStyles.wordWrappedMiniLabel);
            if (EditorGUI.EndChangeCheck()) Repaint();
        }
        void Step(int direction)
        {
            playing = false; var r = Selected;
            var curves = r.layers.Select(l => new AnimationCurve(l.frames.Select(k => k.Key()).ToArray())).ToArray();
            Func<float, string> signature = t => string.Join(",", r.layers.Select((l, i) => t >= l.seconds ? -1 : Mathf.FloorToInt((curves[i].Evaluate(t / l.seconds) + l.startFrame) * r.columns * r.rows)));
            string current = signature(time); float increment = r.layers.Min(l => l.seconds) / 2048;
            for (int i = 0; i < 65536; i++) { float next = Mathf.Clamp(time + direction * increment, 0, r.poolSeconds); if (next == time) break; time = next; if (signature(time) != current) break; }
        }
    }
}
#endif
