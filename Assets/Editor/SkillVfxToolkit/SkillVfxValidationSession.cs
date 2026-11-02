#if UNITY_EDITOR
using System;
using UnityEditor;

namespace GongkaoMower.VfxAuthoring
{
    [InitializeOnLoad]
    public static class SkillVfxValidationSession
    {
        const string Key = "SkillVfxToolkit.";
        static readonly string[] Flags = { "VfxTiming15Validation", "LightningBreakthroughV2Validation", "LightningPresentationBaseline", "TalismanPresentationBaseline", "FieldGeneratorBaseline", "SwordPresentationBaseline", "BossBladeBurstBaseline", "BossDashKnifeBaseline", "BossCloneBaseline", "BossSummonBaseline", "BossReviveBaseline", "BossZoneBaseline" };
        static SkillVfxValidationSession()
        {
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Restore; };
        }
        [MenuItem("工具/VFX/实战验收/爆炸")]
        public static void Explosions() => Begin("explosion-library");
        [MenuItem("工具/VFX/实战验收/符箓")]
        public static void Talisman() => Begin("talisman");
        [MenuItem("工具/VFX/实战验收/落雷与突破")]
        public static void Lightning() => Begin("lightning");
        [MenuItem("工具/VFX/实战验收/罡气护体")]
        public static void Field() => Begin("field-generator");
        [MenuItem("工具/VFX/实战验收/刀光剑影")]
        public static void Sword() => Begin("sword");
        [MenuItem("工具/VFX/实战验收/Boss刀气爆发")]
        public static void BossBladeBurst() => Begin("boss-blade-burst");
        [MenuItem("工具/VFX/实战验收/Boss冲刺与小刀")]
        public static void BossDashKnife() => Begin("boss-dash-knife");
        [MenuItem("工具/VFX/实战验收/Boss分身")]
        public static void BossClone() => Begin("boss-clone");
        [MenuItem("工具/VFX/实战验收/Boss召唤")]
        public static void BossSummon() => Begin("boss-summon");
        [MenuItem("工具/VFX/实战验收/Boss复活")]
        public static void BossRevive() => Begin("boss-revive");
        [MenuItem("工具/VFX/实战验收/Boss地裂")]
        public static void BossZone() => Begin("boss-zone");
        [MenuItem("工具/VFX/实战验收/结束并恢复")]
        public static void Finish() { HomeUIEditorPreview.Finish(); if (!EditorApplication.isPlayingOrWillChangePlaymode) Restore(); }
        public static string Begin(string phase)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("先退出运行模式；验收会暂存并恢复当前已保存场景。");
            if (phase != "explosion-library" && phase != "talisman" && phase != "lightning" && phase != "field-generator" && phase != "sword" && phase != "boss-blade-burst" && phase != "boss-dash-knife" && phase != "boss-clone" && phase != "boss-summon" && phase != "boss-revive" && phase != "boss-zone") throw new ArgumentException("Unknown VFX suite.");
            Restore();
            foreach (var f in Flags) SessionState.SetBool(Key + "previous." + f, SessionState.GetBool(f, false));
            SessionState.SetString(Key + "previous.OutputFolder", SessionState.GetString(Key + "OutputFolder", ""));
            SessionState.SetBool(Key + "active", true);
            SessionState.SetBool("VfxTiming15Validation", true); SessionState.SetBool("LightningBreakthroughV2Validation", true);
            SessionState.SetBool("LightningPresentationBaseline", false); SessionState.SetBool("TalismanPresentationBaseline", false);
            SessionState.SetBool("FieldGeneratorBaseline", false);
            SessionState.SetBool("SwordPresentationBaseline", false);
            SessionState.SetBool("BossBladeBurstBaseline", false);
            SessionState.SetBool("BossDashKnifeBaseline", false);
            SessionState.SetBool("BossCloneBaseline", false);
            SessionState.SetBool("BossSummonBaseline", false);
            SessionState.SetBool("BossReviveBaseline", false);
            SessionState.SetBool("BossZoneBaseline", false);
            string folder = "Toolkit/" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "/" + phase;
            SessionState.SetString(Key + "OutputFolder", folder);
            try { HomeUIEditorPreview.Begin(phase); return "ArtOutput/UI/" + folder; }
            catch { Restore(); HomeUIEditorPreview.Finish(); throw; }
        }
        static void Restore()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !SessionState.GetBool(Key + "active", false)) return;
            foreach (var f in Flags) SessionState.SetBool(f, SessionState.GetBool(Key + "previous." + f, false));
            SessionState.SetString(Key + "OutputFolder", SessionState.GetString(Key + "previous.OutputFolder", ""));
            SessionState.SetBool(Key + "active", false);
        }
    }
}
#endif
