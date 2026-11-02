#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace GongkaoMower.VfxAuthoring
{
    public sealed class PersistentAuraPreviewWindow : EditorWindow
    {
        SkillVfxPreview preview;
        float seconds, radius = 1.75f, cycle = 1, intensity = 1;
        bool breakthrough, playing = true;
        int background;
        bool surfaceStyle, orbitStyle;
        float samplingLimit = 2;
        double last;
        readonly Color[] colors = { new Color(.94f,.85f,.6f), new Color(.42f,.65f,.32f), new Color(.07f,.09f,.12f) };
        [MenuItem("工具/VFX/常驻特效/罡气护体预览")]
        public static void Open() { var w = GetWindow<PersistentAuraPreviewWindow>("罡气护体预览"); w.minSize = new Vector2(540,680); }
        void OnEnable()
        {
            var def=AssetDatabase.LoadAssetAtPath<FieldGeneratorSkillDefinition>(FieldGeneratorPresentationV1Builder.DefinitionPath);
            var prefab=def!=null?def.fieldVisualPrefab:null;
            if (prefab != null) { preview = new SkillVfxPreview(prefab, 1, 1); preview.Focus(Vector2.zero); var view=prefab.GetComponent<FieldGeneratorPresentation>(); surfaceStyle=view.UsesFieldSurface; orbitStyle=view.UsesFieldOrbit; }
            last = EditorApplication.timeSinceStartup; EditorApplication.update += Tick;
        }
        void OnDisable() { EditorApplication.update -= Tick; preview?.Dispose(); preview = null; }
        void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (playing) { seconds += Mathf.Min(.1f,(float)(now-last)); Repaint(); }
            last = now;
        }
        void OnGUI()
        {
            GUILayout.Label("常驻护体场 · 独立于一次性命中配方", EditorStyles.boldLabel);
            breakthrough = EditorGUILayout.Toggle(surfaceStyle?"橙金突破":"朱红金突破", breakthrough);
            radius = EditorGUILayout.Slider("实际半径", radius, .3f, 3);
            cycle = EditorGUILayout.Slider("脉冲周期", cycle, .3f, 2);
            intensity = EditorGUILayout.Slider("表现强度", intensity, .5f, 1.5f);
            background = GUILayout.Toolbar(background, new[] { "浅沙", "草地", "深色" });
            playing = EditorGUILayout.Toggle("播放", playing);
            if (preview == null) { EditorGUILayout.HelpBox("请先构建并绑定当前版本的罡气护体表现。", MessageType.Info); return; }
            preview.ConfigureAura(radius, intensity, cycle, breakthrough);
            samplingLimit = (surfaceStyle ? preview.AuraTravelSeconds : cycle) * 2;
            seconds = EditorGUILayout.Slider("采样秒数", seconds, 0, Mathf.Max(samplingLimit,Mathf.Ceil(seconds/samplingLimit)*samplingLimit));
            if (GUILayout.Button("回到起点")) seconds=0;
            var rect = GUILayoutUtility.GetRect(300, position.height-285, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint) { preview.Sample(seconds); GUI.DrawTexture(rect, preview.Render(rect, colors[background], 3.2f), ScaleMode.StretchToFill); }
            if (GUILayout.Button("导出当前状态 PNG")) preview.SavePng(SkillVfxPipeline.Full("ArtOutput/VFX/"+(orbitStyle?"FieldGeneratorV3":surfaceStyle?"FieldGeneratorV2":"FieldGeneratorV1")+"/Previews/" + (breakthrough ? "breakthrough" : "normal") + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png"), seconds, colors[background], 3.2f);
            EditorGUILayout.LabelField(orbitStyle?"内外反向旋转；突破增加长弧、金白核心与六枚能量棱角。":surfaceStyle?"半透明覆盖 + 三层六边形扩散；真实伤害与暂停、退场使用实战验收菜单。":"旋转 / 脉冲只做表现；真实伤害与暂停、退场使用实战验收菜单。", EditorStyles.wordWrappedMiniLabel);
        }
    }
}
#endif
