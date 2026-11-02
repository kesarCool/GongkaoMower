#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace GongkaoMower.VfxAuthoring
{
    public static class SkillVfxPipeline
    {
        public const string RecipeFolder = "Tools/VFX/Library/Recipes";
        public const string ReportPath = "ArtOutput/VFX/Toolkit/library-audit.json";
        public static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        public static string Full(string relative) => Path.Combine(Root, relative);
        public static string Json(object value) => JsonConvert.SerializeObject(value, Formatting.Indented);
        public static VfxRecipe Read(string path) => JsonConvert.DeserializeObject<VfxRecipe>(File.ReadAllText(path), new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
        public static VfxRecipe[] ReadLibrary() => Directory.Exists(Full(RecipeFolder)) ? Directory.GetFiles(Full(RecipeFolder), "*.json").OrderBy(p => p).Select(Read).ToArray() : new VfxRecipe[0];
        static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
        static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        static bool Close(float a, float b) => Mathf.Abs(a - b) < .0001f;

        public static void Validate(VfxRecipe r)
        {
            Require(r != null && r.schemaVersion == 1, "配方版本必须为 1。");
            Require(!string.IsNullOrEmpty(r.id) && Regex.IsMatch(r.id, @"^[a-z][a-z0-9_]{2,63}$"), "id 使用 3–64 位小写英文、数字、下划线，以字母开头。");
            Require(!string.IsNullOrWhiteSpace(r.displayName), "缺少 displayName。");
            Require(r.width >= 16 && r.height >= 16 && r.width <= 2048 && r.height <= 2048 && Mathf.IsPowerOfTwo(r.width) && Mathf.IsPowerOfTwo(r.height), "正式图集宽高必须是 16–2048 内的 2 次幂。");
            Require(r.columns > 0 && r.rows > 0 && r.columns * r.rows <= 256 && r.width % r.columns == 0 && r.height % r.rows == 0, "分格必须整数等分，最多 256 格。");
            Require(r.pngBudgetKiB > 0 && r.pngBudgetKiB <= 1024 && r.compressionQuality >= 0 && r.compressionQuality <= 100, "PNG 预算或压缩质量无效。");
            Require(Finite(r.poolSeconds) && r.poolSeconds > 0 && r.poolSeconds <= 10 && Finite(r.referenceRadius) && r.referenceRadius >= 0, "回池时间 / 参考半径无效。");
            Require(r.layers != null && r.layers.Length >= 1 && r.layers.Length <= 4, "一次性图集效果支持 1–4 层。");
            var names = new HashSet<string>();
            foreach (var l in r.layers)
            {
                Require(l != null && !string.IsNullOrWhiteSpace(l.name) && !l.name.Contains('/') && names.Add(l.name), "层名不能为空、重复或含斜线。");
                Require(Finite(l.seconds) && l.seconds > 0 && l.seconds + .01f <= r.poolSeconds, "回池时间须比最长粒子寿命至少多 0.01 秒。");
                Require(Finite(l.sizeX) && Finite(l.sizeY) && l.sizeX > 0 && l.sizeY > 0 && (l.size3D || Close(l.sizeX, l.sizeY)), "粒子尺寸无效。");
                Require(Finite(l.offsetX) && Finite(l.offsetY) && Finite(l.startFrame) && l.startFrame >= 0 && l.startFrame < 1 && l.seed > 0, "锚点 / 起始帧 / 随机种子无效。");
                Require(l.frames != null && l.frames.Length >= 2 && Close(l.frames[0].time, 0) && Close(l.frames.Last().time, 1), "帧曲线需覆盖 0–1。");
                float previous = -1;
                foreach (var k in l.frames)
                {
                    Require(k != null && Finite(k.time) && Finite(k.value) && Finite(k.inTangent) && Finite(k.outTangent) && k.time > previous && k.time <= 1 && k.value + l.startFrame >= 0 && k.value + l.startFrame < 1, "帧曲线键值无效或越出图集。"); previous = k.time;
                }
                var curve = new AnimationCurve(l.frames.Select(k => k.Key()).ToArray());
                for (int sample = 0; sample <= 256; sample++) { float frame = curve.Evaluate(sample / 256f) + l.startFrame; Require(frame >= 0 && frame < 1, "帧曲线切线导致采样越界。"); }
                Require(l.alpha != null && l.alpha.Length >= 2 && l.alpha.Length <= 8 && Close(l.alpha[0].time, 0) && Close(l.alpha.Last().time, 1), "透明度键需覆盖 0–1，数量 2–8。");
                previous = -1;
                foreach (var a in l.alpha) { Require(a != null && Finite(a.time) && Finite(a.alpha) && a.time > previous && a.time <= 1 && a.alpha >= 0 && a.alpha <= 1, "透明度键无效。"); previous = a.time; }
            }
            if (r.packing != null)
            {
                var p = r.packing;
                Require(p.sourceColumns > 0 && p.sourceRows > 0 && p.sourceColumns * p.sourceRows <= 256 && p.sourceFrames != null && p.sourceFrames.Length == r.columns * r.rows, "打包映射须与目标格数一致。");
                Require(p.paddingPixels >= 1 && p.paddingPixels * 2 < Math.Min(r.width / r.columns, r.height / r.rows), "打包边距无效。");
                Require(p.sourceFrames.All(f => f >= -1 && f < p.sourceColumns * p.sourceRows) && p.sourceFrames.Any(f => f >= 0), "选帧索引越界或所有格子为空。");
            }
        }

        static Texture2D Decode(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Require(bytes.Length >= 33 && bytes.Length <= 16 * 1024 * 1024 && bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71, "只接受不超过 16 MiB 的 PNG。");
            Func<int, long> be = i => ((long)bytes[i] << 24) | ((long)bytes[i + 1] << 16) | ((long)bytes[i + 2] << 8) | bytes[i + 3];
            Require(be(16) > 0 && be(20) > 0 && be(16) <= 4096 && be(20) <= 4096, "原稿宽高不得超过 4096。");
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try { Require(t.LoadImage(bytes), "PNG 解码失败。"); return t; }
            catch { Object.DestroyImmediate(t); throw; }
        }

        // Resampling and packing only. No background removal, recolouring, or invented in-between frames.
        public static byte[] Prepare(VfxRecipe r, string pngPath)
        {
            Validate(r);
            var source = Decode(pngPath); Texture2D output = null;
            try
            {
                var pixels = source.GetPixels32();
                Require(pixels.Any(c => c.a == 0) && pixels.Any(c => c.a > 16), "必须有真实透明区及可见主体，不能是实底 / 全透明图。");
                if (r.packing == null)
                {
                    Require(source.width == r.width && source.height == r.height, "正式图集尺寸与配方不一致；需原稿选帧时请配置 packing。");
                    byte[] original = File.ReadAllBytes(pngPath);
                    Require(original.Length <= r.pngBudgetKiB * 1024, "正式 PNG 超出配方预算。"); return original;
                }
                var p = r.packing;
                Require(source.width % p.sourceColumns == 0 && source.height % p.sourceRows == 0, "原稿不能按指定行列整数等分。");
                output = new Texture2D(r.width, r.height, TextureFormat.RGBA32, false); output.SetPixels32(new Color32[r.width * r.height]);
                int cw = r.width / r.columns, ch = r.height / r.rows;
                float sw = source.width / p.sourceColumns, sh = source.height / p.sourceRows;
                float scale = Mathf.Min((cw - 2f * p.paddingPixels) / sw, (ch - 2f * p.paddingPixels) / sh);
                int dw = Mathf.Max(1, Mathf.RoundToInt(sw * scale)), dh = Mathf.Max(1, Mathf.RoundToInt(sh * scale));
                for (int cell = 0; cell < p.sourceFrames.Length; cell++)
                {
                    int frame = p.sourceFrames[cell]; if (frame < 0) continue;
                    int dx = cell % r.columns * cw + (cw - dw) / 2, dy = r.height - (cell / r.columns + 1) * ch + (ch - dh) / 2;
                    for (int y = 0; y < dh; y++) for (int x = 0; x < dw; x++)
                    {
                        float u = (frame % p.sourceColumns + (x + .5f) / dw) / p.sourceColumns;
                        float v = (p.sourceRows - 1 - frame / p.sourceColumns + (y + .5f) / dh) / p.sourceRows;
                        output.SetPixel(dx + x, dy + y, source.GetPixelBilinear(u, v));
                    }
                }
                output.Apply(); var bytes = output.EncodeToPNG();
                Require(bytes.Length <= r.pngBudgetKiB * 1024, "打包后的 PNG 超出配方预算。"); return bytes;
            }
            finally { Object.DestroyImmediate(source); if (output != null) Object.DestroyImmediate(output); }
        }

        public static GameObject Import(VfxRecipe input, string sourcePng = null)
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "请先退出运行模式再导入。"); Validate(input);
            var r = JsonConvert.DeserializeObject<VfxRecipe>(Json(input));
            string recipePath = RecipeFolder + "/" + r.id + ".json";
            r.prefabPath = "Assets/Res/Prefabs/Effects/Generated/fx_" + r.id + ".prefab";
            string importReport = "ArtOutput/VFX/Toolkit/Imports/" + r.id + ".json";
            string archivedSource = "ArtOutput/VFX/Toolkit/Sources/" + r.id + "/original.png";
            bool newImage = !string.IsNullOrEmpty(sourcePng);
            if (newImage) { r.texturePath = "Assets/Res/Textures/VFX/Generated/sheet_" + r.id + ".png"; r.materialPath = "Assets/Res/Materials/VFX/Generated/mat_" + r.id + ".mat"; }
            foreach (string p in new[] { recipePath, r.prefabPath, importReport }.Concat(newImage ? new[] { r.texturePath, r.materialPath, archivedSource } : new string[0]))
                Require(!File.Exists(Full(p)) && !File.Exists(Full(p + ".meta")), "目标已存在，使用新的 id：" + p);
            var shader = Shader.Find("Sprites/Default"); Require(shader != null, "缺少 Sprites/Default。");
            byte[] prepared = newImage ? Prepare(input, sourcePng) : null;
            Material material = null;
            if (!newImage)
            {
                Require(!string.IsNullOrEmpty(r.texturePath) && r.texturePath.StartsWith("Assets/Res/Textures/VFX/") && !r.texturePath.Contains(".."), "复用贴图需位于 Assets/Res/Textures/VFX。");
                material = AssetDatabase.LoadAssetAtPath<Material>(r.materialPath);
                Require(material != null && material.shader == shader && AssetDatabase.GetAssetPath(material.mainTexture) == r.texturePath, "复用材质与贴图不匹配。");
                Require(material.mainTexture.width == r.width && material.mainTexture.height == r.height, "复用贴图尺寸不匹配。");
            }
            var assetsCreated = new List<string>(); var filesCreated = new List<string>(); GameObject root = null;
            try
            {
                if (newImage)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Full(r.texturePath))); File.WriteAllBytes(Full(r.texturePath), prepared); assetsCreated.Add(r.texturePath);
                    AssetDatabase.ImportAsset(r.texturePath, ImportAssetOptions.ForceSynchronousImport);
                    var ti = (TextureImporter)AssetImporter.GetAtPath(r.texturePath);
                    ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true; ti.alphaSource = TextureImporterAlphaSource.FromInput; ti.alphaIsTransparency = true;
                    ti.mipmapEnabled = false; ti.isReadable = false; ti.npotScale = TextureImporterNPOTScale.None; ti.maxTextureSize = Math.Max(r.width, r.height);
                    ti.wrapMode = TextureWrapMode.Clamp; ti.filterMode = FilterMode.Bilinear;
                    var web = ti.GetPlatformTextureSettings("WebGL"); web.overridden = true; web.maxTextureSize = ti.maxTextureSize;
                    web.format = TextureImporterFormat.ETC2_RGBA8Crunched; web.crunchedCompression = true; web.compressionQuality = r.compressionQuality;
                    ti.SetPlatformTextureSettings(web); ti.SaveAndReimport();
                    Directory.CreateDirectory(Path.GetDirectoryName(Full(r.materialPath)));
                    material = new Material(shader) { mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(r.texturePath), color = Color.white };
                    assetsCreated.Add(r.materialPath); AssetDatabase.CreateAsset(material, r.materialPath);
                }
                root = BuildObject(r, material);
                Directory.CreateDirectory(Path.GetDirectoryName(Full(r.prefabPath))); assetsCreated.Add(r.prefabPath);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, r.prefabPath); Require(prefab != null, "保存 Prefab 失败。");
                r.status = "draft-needs-combat-validation"; r.validationReport = ""; r.packing = null;
                if (newImage)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Full(archivedSource))); File.Copy(sourcePng, Full(archivedSource)); filesCreated.Add(archivedSource);
                    r.sourceDirectory = Path.GetDirectoryName(archivedSource).Replace('\\', '/');
                }
                Directory.CreateDirectory(Full(RecipeFolder)); File.WriteAllText(Full(recipePath), Json(r)); filesCreated.Add(recipePath);
                Directory.CreateDirectory(Path.GetDirectoryName(Full(importReport)));
                File.WriteAllText(Full(importReport), Json(new { id = r.id, createdUtc = DateTime.UtcNow, reusedAtlas = !newImage, recipePath, r.prefabPath, r.texturePath, r.materialPath, status = r.status, note = "Authoring import only; no skill binding or combat approval." })); filesCreated.Add(importReport);
                AssetDatabase.SaveAssets(); return prefab;
            }
            catch
            {
                foreach (var p in assetsCreated.AsEnumerable().Reverse()) AssetDatabase.DeleteAsset(p);
                foreach (var p in filesCreated) File.Delete(Full(p)); throw;
            }
            finally { if (root != null) Object.DestroyImmediate(root); }
        }

        public static GameObject BuildObject(VfxRecipe r, Material material)
        {
            Validate(r); var root = new GameObject("fx_" + r.id);
            try
            {
                var systems = new List<ParticleSystem>();
                foreach (var l in r.layers)
                {
                    var child = new GameObject(l.name); child.transform.SetParent(root.transform, false); child.transform.localPosition = new Vector3(l.offsetX, l.offsetY, 0);
                    var ps = child.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var m = ps.main; m.duration = l.seconds; m.startLifetime = l.seconds; m.loop = false; m.playOnAwake = false; m.startSpeed = 0;
                    m.startSize3D = l.size3D; if (l.size3D) { m.startSizeX = l.sizeX; m.startSizeY = l.sizeY; m.startSizeZ = 1; } else m.startSize = l.sizeX;
                    m.startColor = Color.white; m.maxParticles = 1; m.simulationSpace = ParticleSystemSimulationSpace.Local; m.scalingMode = ParticleSystemScalingMode.Hierarchy; m.useUnscaledTime = false;
                    ps.useAutoRandomSeed = false; ps.randomSeed = l.seed;
                    var shape = ps.shape; shape.enabled = false;
                    var e = ps.emission; e.rateOverTime = 0; e.rateOverDistance = 0; e.SetBursts(new[] { new ParticleSystem.Burst(0, 1) });
                    var sheet = ps.textureSheetAnimation; sheet.enabled = true; sheet.mode = ParticleSystemAnimationMode.Grid; sheet.numTilesX = r.columns; sheet.numTilesY = r.rows;
                    sheet.animation = ParticleSystemAnimationType.WholeSheet; sheet.timeMode = ParticleSystemAnimationTimeMode.Lifetime; sheet.startFrame = l.startFrame; sheet.cycleCount = 1;
                    sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(l.frames.Select(k => k.Key()).ToArray()));
                    var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, l.alpha.Select(a => new GradientAlphaKey(a.alpha, a.time)).ToArray());
                    var color = ps.colorOverLifetime; color.enabled = true; color.color = gradient;
                    var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material; renderer.sortingOrder = r.sortingOrder; renderer.maxParticleSize = 1;
                    renderer.alignment = ParticleSystemRenderSpace.View; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                    systems.Add(ps);
                }
                var so = new SerializedObject(root.AddComponent<PooledCombatVfx>()); var array = so.FindProperty("_particles"); array.arraySize = systems.Count;
                for (int i = 0; i < systems.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = systems[i];
                so.FindProperty("lifetimeOverride").floatValue = r.poolSeconds; so.FindProperty("sortingOrder").intValue = r.sortingOrder; so.FindProperty("referenceRadius").floatValue = r.referenceRadius;
                so.ApplyModifiedPropertiesWithoutUndo(); return root;
            }
            catch { Object.DestroyImmediate(root); throw; }
        }

        public static VfxRecipe Capture(string prefabPath, string displayName, string usage, int budgetKiB)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath); Require(root != null, "Missing " + prefabPath);
            var systems = root.GetComponentsInChildren<ParticleSystem>(true); var pool = new SerializedObject(root.GetComponent<PooledCombatVfx>());
            var material = systems[0].GetComponent<ParticleSystemRenderer>().sharedMaterial; var texture = material.mainTexture;
            var ti = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
            return new VfxRecipe {
                id = root.name.Substring(3), displayName = displayName, category = "impact", usage = usage, status = "integrated-editor-verified",
                prefabPath = prefabPath, texturePath = AssetDatabase.GetAssetPath(texture), materialPath = AssetDatabase.GetAssetPath(material),
                width = texture.width, height = texture.height, columns = systems[0].textureSheetAnimation.numTilesX, rows = systems[0].textureSheetAnimation.numTilesY,
                pngBudgetKiB = budgetKiB, compressionQuality = ti.GetPlatformTextureSettings("WebGL").compressionQuality,
                poolSeconds = pool.FindProperty("lifetimeOverride").floatValue, referenceRadius = pool.FindProperty("referenceRadius").floatValue, sortingOrder = pool.FindProperty("sortingOrder").intValue,
                layers = systems.Select(ps => new VfxLayer {
                    name = ps.name, seconds = ps.main.startLifetime.constant, size3D = ps.main.startSize3D,
                    sizeX = ps.main.startSize3D ? ps.main.startSizeX.constant : ps.main.startSize.constant, sizeY = ps.main.startSize3D ? ps.main.startSizeY.constant : ps.main.startSize.constant,
                    offsetX = ps.transform == root.transform ? 0 : ps.transform.localPosition.x, offsetY = ps.transform == root.transform ? 0 : ps.transform.localPosition.y,
                    startFrame = ps.textureSheetAnimation.startFrame.constant, seed = ps.randomSeed,
                    frames = ps.textureSheetAnimation.frameOverTime.curve.keys.Select(VfxCurveKey.From).ToArray(),
                    alpha = (ps.colorOverLifetime.enabled ? ps.colorOverLifetime.color.gradient.alphaKeys : new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) }).Select(a => new VfxAlphaKey { time = a.time, alpha = a.alpha }).ToArray()
                }).ToArray()
            };
        }

        public static string AuditLibrary()
        {
            var rows = new List<object>(); var ids = new HashSet<string>(); bool passed = true;
            foreach (var r in ReadLibrary())
            {
                var errors = new List<string>();
                try
                {
                    Validate(r); Require(ids.Add(r.id), "重复 id。");
                    var live = Capture(r.prefabPath, r.displayName, r.usage, r.pngBudgetKiB);
                    Require(live.texturePath == r.texturePath && live.materialPath == r.materialPath, "材质 / 贴图路径漂移。");
                    Require(live.width == r.width && live.height == r.height && live.columns == r.columns && live.rows == r.rows && live.layers.Length == r.layers.Length, "布局 / 层数漂移。");
                    Require(Close(live.poolSeconds, r.poolSeconds) && Close(live.referenceRadius, r.referenceRadius) && live.sortingOrder == r.sortingOrder, "池化参数漂移。");
                    for (int i = 0; i < live.layers.Length; i++)
                    {
                        var a = live.layers[i]; var b = r.layers[i];
                        Require(Close(a.seconds, b.seconds) && Close(a.sizeX, b.sizeX) && Close(a.sizeY, b.sizeY) && Close(a.offsetX, b.offsetX) && Close(a.offsetY, b.offsetY) && Close(a.startFrame, b.startFrame) && a.size3D == b.size3D && a.seed == b.seed, "粒子尺寸 / 时长 / 锚点漂移。");
                        Require(Json(a.frames) == Json(b.frames) && Json(a.alpha) == Json(b.alpha), "帧曲线 / 透明度漂移。");
                    }
                    Require(new FileInfo(Full(r.texturePath)).Length <= r.pngBudgetKiB * 1024, "PNG 超预算。");
                    var ti = (TextureImporter)AssetImporter.GetAtPath(r.texturePath); var web = ti.GetPlatformTextureSettings("WebGL");
                    Require(!ti.isReadable && !ti.mipmapEnabled && ti.wrapMode == TextureWrapMode.Clamp && ti.filterMode == FilterMode.Bilinear && web.overridden && web.format == TextureImporterFormat.ETC2_RGBA8Crunched && web.compressionQuality == r.compressionQuality, "纹理导入设置漂移。");
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(r.prefabPath);
                    foreach (var ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
                        Require(Close(ps.main.duration, ps.main.startLifetime.constant) && !ps.main.loop && !ps.main.playOnAwake && ps.main.maxParticles == 1 && Close(ps.main.simulationSpeed, 1) && Close(ps.main.startDelay.constant, 0) && ps.textureSheetAnimation.timeMode == ParticleSystemAnimationTimeMode.Lifetime, "粒子播放方式漂移。");
                }
                catch (Exception ex) { errors.Add(ex.Message); passed = false; }
                rows.Add(new { r.id, passed = errors.Count == 0, errors, sourcePngBytes = File.Exists(Full(r.texturePath ?? "")) ? new FileInfo(Full(r.texturePath)).Length : 0, etc2BlockBytesWithoutMipmaps = (long)((r.width + 3) / 4) * ((r.height + 3) / 4) * 16 });
            }
            if (rows.Count == 0) passed = false;
            var unique = ReadLibrary().Select(r => r.texturePath).Distinct().ToArray();
            string json = Json(new { passed, checkedUtc = DateTime.UtcNow, recipes = rows, uniqueTextures = unique.Length, uniquePngBytes = unique.Where(p => File.Exists(Full(p))).Sum(p => new FileInfo(Full(p)).Length), note = "Editor authoring audit; not device FPS or final upload-size validation." });
            Directory.CreateDirectory(Path.GetDirectoryName(Full(ReportPath))); File.WriteAllText(Full(ReportPath), json); return json;
        }
    }
}
#endif
