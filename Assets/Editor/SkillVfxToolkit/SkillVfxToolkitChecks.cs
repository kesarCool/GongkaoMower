#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace GongkaoMower.VfxAuthoring
{
    public static class SkillVfxToolkitChecks
    {
        static string Hash(string path) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(SkillVfxPipeline.Full(path)))); }
        [MenuItem("工具/VFX/检查导入预览工具")]
        public static void Menu() { Debug.Log(Run()); }
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("先退出运行模式。");
            var checks = new List<object>();
            Action<string, bool> check = (name, passed) => { checks.Add(new { name, passed }); if (!passed) throw new InvalidOperationException(name); };
            Action<string, Action> rejects = (name, action) => { bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; } check(name, rejected); };
            var recipes = SkillVfxPipeline.ReadLibrary();
            var originals = recipes.SelectMany(r => new[] { r.prefabPath, r.texturePath, r.materialPath }).Distinct().ToDictionary(p => p, Hash);
            int scenes = SceneManager.sceneCount; string active = SceneManager.GetActiveScene().path; bool dirty = SceneManager.GetActiveScene().isDirty;
            string id = "toolkit_test_" + DateTime.Now.ToString("yyyyMMddHHmmssfff"), reusedId = id + "_reuse";
            string inputPath = SkillVfxPipeline.Full("ArtOutput/VFX/Toolkit/Tests/packing-input.png");
            var cleanup = new List<string>(); var recipeCleanup = new List<string>(); bool passedAll = false; string error = "";
            try
            {
                check("Existing library validates against live assets", SkillVfxPipeline.AuditLibrary().Contains("\"passed\": true") && !SkillVfxPipeline.AuditLibrary().Contains("\"passed\": false"));
                var r = SkillVfxPipeline.Read(SkillVfxPipeline.Full("Tools/VFX/Templates/hit-eight-frame.example.json"));
                SkillVfxPipeline.Validate(r); check("New-effect template validates", true);
                r.id = "../escape"; rejects("Path traversal id rejected", () => SkillVfxPipeline.Validate(r)); r.id = id;
                r.poolSeconds = .1f; rejects("Premature pool return rejected", () => SkillVfxPipeline.Validate(r)); r.poolSeconds = .36f;
                r.layers[0].frames[1].value = 1; rejects("Adjacent-sheet wrap rejected", () => SkillVfxPipeline.Validate(r)); r.layers[0].frames[1].value = .999875f;
                r.width = 500; rejects("Non-power-of-two output rejected", () => SkillVfxPipeline.Validate(r)); r.width = 64;
                r.height = 16; r.columns = 4; r.rows = 1; r.layers[0].frames[1].value = 3.999f / 4;
                r.layers[0].frames[0].outTangent = r.layers[0].frames[1].inTangent = 3.999f / 4;
                r.packing = new VfxPacking { sourceColumns = 2, sourceRows = 2, paddingPixels = 2, sourceFrames = new[] { 3, 1, 0, 2 } };
                var raw = new Texture2D(64, 64, TextureFormat.RGBA32, false); var colors = new[] { Color.red, Color.green, Color.blue, Color.yellow };
                try
                {
                    raw.SetPixels32(new Color32[64 * 64]);
                    for (int f = 0; f < 4; f++) for (int y = 4; y < 28; y++) for (int x = 4; x < 28; x++) raw.SetPixel(f % 2 * 32 + x, (1 - f / 2) * 32 + y, colors[f]);
                    raw.Apply(); Directory.CreateDirectory(Path.GetDirectoryName(inputPath)); File.WriteAllBytes(inputPath, raw.EncodeToPNG());
                    var packed = new Texture2D(2, 2); try {
                        packed.LoadImage(SkillVfxPipeline.Prepare(r, inputPath));
                        check("Repacking produces exact output dimensions", packed.width == 64 && packed.height == 16);
                        check("Row-major source selection and top/bottom orientation", Enumerable.Range(0,4).All(f => Vector4.Distance(packed.GetPixel(f * 16 + 8, 8), colors[r.packing.sourceFrames[f]]) < .01f));
                        check("Per-cell padding remains transparent", Enumerable.Range(0,4).All(f => packed.GetPixel(f * 16, 0).a == 0));
                    } finally { Object.DestroyImmediate(packed); }
                    raw.SetPixels(Enumerable.Repeat(Color.white, 4096).ToArray()); raw.Apply();
                    string opaque = Path.Combine(Path.GetDirectoryName(inputPath), "opaque-input.png"); File.WriteAllBytes(opaque, raw.EncodeToPNG());
                    rejects("Opaque RGB-style matte rejected", () => SkillVfxPipeline.Prepare(r, opaque));
                }
                finally { Object.DestroyImmediate(raw); }
                var prefab = SkillVfxPipeline.Import(r, inputPath);
                var imported = SkillVfxPipeline.Read(SkillVfxPipeline.Full(SkillVfxPipeline.RecipeFolder + "/" + id + ".json"));
                cleanup.Add(imported.prefabPath); cleanup.Add(imported.materialPath); cleanup.Add(imported.texturePath); recipeCleanup.Add(id);
                check("Imported recipe is draft and old approval cleared", imported.status == "draft-needs-combat-validation" && imported.validationReport == "" && imported.packing == null);
                var ps = prefab.GetComponentInChildren<ParticleSystem>(); var pool = new SerializedObject(prefab.GetComponent<PooledCombatVfx>());
                check("Final duration and lifetime are not multiplied again", Mathf.Abs(ps.main.duration - .3f) < .0001f && Mathf.Abs(ps.main.startLifetime.constant - .3f) < .0001f);
                check("Pool override covers full animation", Mathf.Abs(pool.FindProperty("lifetimeOverride").floatValue - .36f) < .0001f);
                var ti = (TextureImporter)AssetImporter.GetAtPath(imported.texturePath); var web = ti.GetPlatformTextureSettings("WebGL");
                check("WebGL compression and no CPU copy/mips", web.overridden && web.format == TextureImporterFormat.ETC2_RGBA8Crunched && !ti.isReadable && !ti.mipmapEnabled);
                string guid = AssetDatabase.AssetPathToGUID(imported.prefabPath), bytes = Hash(imported.prefabPath);
                rejects("Duplicate id cannot overwrite resources", () => SkillVfxPipeline.Import(r, inputPath));
                check("Duplicate attempt preserves GUID and prefab bytes", guid == AssetDatabase.AssetPathToGUID(imported.prefabPath) && bytes == Hash(imported.prefabPath));
                imported.id = reusedId; var reused = SkillVfxPipeline.Import(imported);
                cleanup.Add(AssetDatabase.GetAssetPath(reused)); recipeCleanup.Add(reusedId);
                check("Atlas-reuse variant shares existing material", reused.GetComponentInChildren<ParticleSystemRenderer>().sharedMaterial == ps.GetComponent<ParticleSystemRenderer>().sharedMaterial);
                using (var preview = new SkillVfxPreview(prefab, 8, 1))
                {
                    preview.Sample(.05f); check("Eight-instance preview plays eight particles", preview.ParticleCount == 8);
                    preview.Sample(.4f); check("End-of-lifetime preview is empty", preview.ParticleCount == 0);
                    preview.Sample(.05f); check("Reverse scrubbing resets expired systems", preview.ParticleCount == 8);
                    preview.SavePng(SkillVfxPipeline.Full("ArtOutput/VFX/Toolkit/Tests/packed-preview.png"), .05f, new Color(.94f,.85f,.6f), 2);
                }
                check("Preview leaves loaded scene count/path/dirty state unchanged", scenes == SceneManager.sceneCount && active == SceneManager.GetActiveScene().path && dirty == SceneManager.GetActiveScene().isDirty);
                check("No imported asset is bound to a skill", !Directory.GetFiles(SkillVfxPipeline.Full("Assets/ScriptableObject/Skill"), "*.asset", SearchOption.AllDirectories).Any(p => File.ReadAllText(p).Contains(guid)));
                foreach (var original in originals) check("Production asset unchanged: " + original.Key, Hash(original.Key) == original.Value);
                passedAll = true;
            }
            catch (Exception ex) { error = ex.ToString(); }
            finally
            {
                foreach (var p in cleanup) AssetDatabase.DeleteAsset(p);
                foreach (var testId in recipeCleanup) File.Delete(SkillVfxPipeline.Full(SkillVfxPipeline.RecipeFolder + "/" + testId + ".json"));
                SkillVfxPipeline.AuditLibrary();
            }
            string report = SkillVfxPipeline.Json(new { passed = passedAll, checks, error, fixtureAssetsRemoved = cleanup.All(p => AssetDatabase.LoadMainAssetAtPath(p) == null), note = "Editor import/packing/preview checks, no real skill damage test or WeChat device claim." });
            File.WriteAllText(SkillVfxPipeline.Full("ArtOutput/VFX/Toolkit/Tests/toolkit-report.json"), report); return report;
        }
    }
}
#endif
