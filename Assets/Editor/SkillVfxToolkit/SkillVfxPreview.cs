#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GongkaoMower.VfxAuthoring
{
    // Owns only a PreviewRenderUtility scene; never spawns effects into the user's scene or pool.
    public sealed class SkillVfxPreview : IDisposable
    {
        readonly PreviewRenderUtility preview;
        readonly List<GameObject> instances = new List<GameObject>();
        public SkillVfxPreview(GameObject prefab, int count, float scale)
        {
            preview = new PreviewRenderUtility();
            try
            {
                preview.camera.orthographic = true; preview.camera.nearClipPlane = .1f; preview.camera.farClipPlane = 40;
                preview.camera.transform.position = new Vector3(0, 1.2f, -10); preview.camera.transform.rotation = Quaternion.identity;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                for (int i = 0; i < count; i++)
                {
                    var go = preview.InstantiatePrefabInScene(prefab); go.hideFlags = HideFlags.HideAndDontSave;
                    foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true)) if (behaviour != null) behaviour.enabled = false;
                    go.transform.position = new Vector3(count == 1 ? 0 : (i % 4 - 1.5f) * .65f, count < 5 ? 0 : i / 4 * .65f, 0);
                    go.transform.localScale *= scale; instances.Add(go);
                }
            }
            catch { Dispose(); throw; }
        }
        public void Sample(float seconds)
        {
            foreach (var go in instances) foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                ps.Simulate(Mathf.Max(.0001f, seconds), false, true, false);
            foreach (var go in instances) foreach (var aura in go.GetComponentsInChildren<FieldGeneratorPresentation>(true)) aura.SamplePreview(seconds);
        }
        public void ConfigureAura(float radius, float intensity, float cycle, bool breakthrough)
        {
            foreach (var go in instances) foreach (var aura in go.GetComponentsInChildren<FieldGeneratorPresentation>(true)) aura.Configure(radius, intensity, cycle, breakthrough, -5, .875f);
        }
        public void Focus(Vector2 centre) { preview.camera.transform.position = new Vector3(centre.x,centre.y,-10); }
        public float AuraTravelSeconds
        {
            get { foreach (var go in instances) { var aura = go.GetComponentInChildren<FieldGeneratorPresentation>(); if (aura != null && aura.UsesFieldSurface) return aura.FieldTravelSeconds; } return 0; }
        }
        public int ParticleCount
        {
            get { int count = 0; foreach (var go in instances) foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) count += ps.particleCount; return count; }
        }
        public Texture Render(Rect rect, Color background, float halfHeight)
        {
            preview.camera.backgroundColor = background; preview.camera.orthographicSize = halfHeight;
            preview.BeginPreview(rect, GUIStyle.none); preview.Render(); return preview.EndPreview();
        }
        public void SavePng(string path, float seconds, Color background, float halfHeight)
        {
            Sample(seconds); var rendered = Render(new Rect(0, 0, 512, 512), background, halfHeight);
            var rt = RenderTexture.GetTemporary(512, 512, 0, RenderTextureFormat.ARGB32); var previous = RenderTexture.active;
            var png = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(rendered, rt); RenderTexture.active = rt; png.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); png.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, png.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(png); }
        }
        public void Dispose()
        {
            foreach (var go in instances) if (go != null)
                foreach (var aura in go.GetComponentsInChildren<FieldGeneratorPresentation>(true)) aura.ReleasePreviewResources();
            preview?.Cleanup(); instances.Clear();
        }
    }
}
#endif
