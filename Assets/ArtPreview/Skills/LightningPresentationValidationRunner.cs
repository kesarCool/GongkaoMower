#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LightningPresentationValidationRunner : UIValidationRunnerBase
{
    const string DefinitionPath = "Assets/ScriptableObject/Skill/ActiveSkill/SkillDef_LightningStrike.asset";
    const string AtlasPath = "Assets/Res/Textures/VFX/sheet_lightning_v1.png";
    bool Baseline => SessionState.GetBool("LightningPresentationBaseline", false);
    bool RedesignSuite => SessionState.GetBool("LightningBreakthroughV2Validation", false);
    bool BreakthroughSuite => RedesignSuite || SessionState.GetBool("LightningBreakthroughValidation", false);
    protected override string OutputFolder { get { string custom = SessionState.GetString("SkillVfxToolkit.OutputFolder", ""); return !string.IsNullOrEmpty(custom) ? custom : SessionState.GetBool("VfxTiming15Validation", false) ? "Timing15/Lightning" : RedesignSuite ? "LightningBreakthroughV2" : BreakthroughSuite ? "LightningBreakthroughV1" : Baseline ? "LightningBefore" : "LightningAfter"; } }
    protected override string ValidationNote => "Real Game scene, equipped skill and original damage/targeting. A single eligible registry target makes AOE edge checks deterministic; other physical enemies remain damageable. Stills sample triggered systems at stated times; videos preserve capture timing. Memory-only save. Editor counters are structural measurements, not device FPS or final WeChat package size.";
    LightningStrikeSkillDefinition definition;
    PlayerSkills skills;
    Transform player;
    readonly List<EnemyBase> enemies = new List<EnemyBase>();
    readonly List<Vector3> casts = new List<Vector3>();
    readonly List<object> clips = new List<object>();
    readonly Vector2 origin = new Vector2(15, 10), center = new Vector2(15, 16);
    UnityEngine.Random.State randomState;
    float originalScale;
    bool saved;
    int talismanCasts, grenadeCasts;
    PooledCombatVfx[] Effects() => FindObjectsOfType<PooledCombatVfx>().Where(v => v.name.StartsWith("fx_lightning", StringComparison.Ordinal)).ToArray();
    void OnCast(SkillCastEvent e)
    {
        if (e.skillId == SkillId.LightningStrike) casts.Add(e.worldPosition);
        if (e.skillId == SkillId.AutoProjectileTalisman) talismanCasts++;
        if (e.skillId == SkillId.ThrowGrenade) grenadeCasts++;
    }
    void ReleaseEffects()
    {
        foreach (var effect in Effects()) { CombatVfxSpawner.NotifyReleased(effect.gameObject); GameObjectPool.Release(effect.gameObject); }
    }
    void Reset(Vector2 shift)
    {
        ReleaseEffects(); casts.Clear(); player.position = origin + shift;
        Camera.main.transform.position = new Vector3(15 + shift.x, 16 + shift.y, -10);
        for (int i = 0; i < enemies.Count; i++)
        {
            enemies[i].transform.position = center + shift + Vector2.right * new[] { 0, .95f, 1.7f }[i];
            enemies[i].ApplyTableStats(1, 10000, 0);
        }
        Physics2D.SyncTransforms();
    }
    SkillLightningStrike Equip(int level)
    {
        skills.ClearAllSkills(); skills.TryAddSkill(SkillId.LightningStrike);
        var skill = skills.GetEquippedSkill<SkillLightningStrike>(SkillId.LightningStrike);
        while (skill.Level < level)
        {
            if (BreakthroughSuite) { if (!skills.TryLevelUp(SkillId.LightningStrike)) throw new InvalidOperationException("Real progression gate rejected lightning level " + level); }
            else skill.OnLevelUp();
        }
        definition.ApplyStatsToSkill(skill, level);
        skill.OnEquip(new SkillContext { player = player, enemyTag = skills.enemyTag }); return skill;
    }
    IEnumerator Sample(string name, PooledCombatVfx effect, float time)
    {
        if (SessionState.GetBool("VfxTiming15Validation", false)) time *= 1.5f;
        Time.timeScale = 0;
        foreach (var p in effect.GetComponentsInChildren<ParticleSystem>()) { p.Simulate(time, false, true, false); p.Pause(false); }
        Canvas.ForceUpdateCanvases(); yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png")); yield return null; yield return null;
        Time.timeScale = 1;
    }
    protected override IEnumerator RunChecks()
    {
        randomState = UnityEngine.Random.state; originalScale = Time.timeScale; saved = true; Application.runInBackground = true;
        EventBus.Subscribe<SkillCastEvent>(OnCast, owner: this);
        yield return SceneManager.UnloadSceneAsync("Home"); SelectedLevelContext.Set(1, 101);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/Game.unity", LoadSceneMode.Additive);
        SceneManager.SetActiveScene(SceneManager.GetSceneByName("Game")); yield return new WaitForSecondsRealtime(.4f);
        foreach (var s in FindObjectsOfType<SpawnerWaves>()) { s.StopAllCoroutines(); s.enabled = false; }
        foreach (var o in FindObjectsOfType<BattleOutcomeCoordinator>()) { o.StopAllCoroutines(); o.enabled = false; }
        foreach (var f in FindObjectsOfType<CameraFollow2D>()) f.enabled = false;
        UIManager.Instance.CloseAllStack(); foreach (var e in FindObjectsOfType<EnemyBase>()) Destroy(e.gameObject);
        yield return null; CombatTargetRegistry.ClearAll();
        skills = FindObjectOfType<PlayerSkills>(); player = skills.transform; skills.enabled = false; skills.ClearAllSkills();
        var controller = player.GetComponent<PlayerController>(); if (controller != null) controller.enabled = false;
        var rb = player.GetComponent<Rigidbody2D>(); if (rb != null) { rb.velocity = Vector2.zero; rb.gravityScale = 0; }
        skills.attackMultiplier = 1; skills.attackRangeMul = 1; skills.critRate = 0; skills.penRate = 0;
        Camera.main.orthographicSize = 11.5f;
        definition = AssetDatabase.LoadAssetAtPath<LightningStrikeSkillDefinition>(DefinitionPath);
        for (int i = 0; i < 3; i++)
        {
            var go = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy/Normal/enemy001.prefab"), center, Quaternion.identity);
            foreach (var b in go.GetComponentsInChildren<MonoBehaviour>()) if (!(b is EnemyBase) && !(b is EnemyWordLabel) && !(b is TMP_Text)) b.enabled = false;
            go.GetComponentInChildren<EnemyWordLabel>().SetWord(new[] { "雷霆万钧", "范围内", "范围外" }[i]); enemies.Add(go.GetComponent<EnemyBase>());
            if (i > 0) CombatTargetRegistry.Unregister(go);
        }
        var duplicate = enemies[0].gameObject.AddComponent<CircleCollider2D>(); duplicate.isTrigger = true; duplicate.radius = .25f;
        Time.timeScale = 1; yield return null;
        string old = File.ReadAllText("Library/VFXBackups/LightningPresentation-20261025/Assets/ScriptableObject/Skill/ActiveSkill/SkillDef_LightningStrike.asset");
        string current = File.ReadAllText(DefinitionPath);
        string visualReferences = @"(?m)^  (strikeFxPrefab|maxLevelPrefab):[^\r\n]*(\r?\n)?";
        Record("Every skill definition field except the VFX references is unchanged", Regex.Replace(old, visualReferences, "").Replace("\r\n", "\n") == Regex.Replace(current, visualReferences, "").Replace("\r\n", "\n"));
        if (BreakthroughSuite) yield return BreakthroughChecks();
        yield return StrikeChecks("normal", 1, Vector2.zero);
        yield return StrikeChecks("max", 5, Vector2.zero);
        yield return StrikeChecks("grass", 5, new Vector2(-12, 3));
        Reset(Vector2.zero); var noTarget = Equip(1); CombatTargetRegistry.Unregister(enemies[0].gameObject); noTarget.Tick(noTarget.interval + .1f);
        Record("No eligible target produces no strike or damage", casts.Count == 0 && enemies.All(e => e.Hp == 10000));
        CombatTargetRegistry.Register(enemies[0].gameObject); enemies[0].transform.position = (Vector2)player.position + Vector2.up * 11; Physics2D.SyncTransforms();
        noTarget.Tick(.1f); Record("Original ten-unit targeting range is preserved", casts.Count == 0);
        yield return Motion("max", false);
        yield return Motion("mixed", true);
        if (!Baseline) yield return PresentationChecks();
        File.WriteAllText(Path.Combine(output, "clips.json"), Newtonsoft.Json.JsonConvert.SerializeObject(clips));
        skills.ClearAllSkills(); yield return SceneManager.UnloadSceneAsync("Game"); yield return null;
        Record("Leaving battle clears all borrowed lightning", Effects().Length == 0);
        Time.timeScale = originalScale; UnityEngine.Random.state = randomState;
    }
    IEnumerator StrikeChecks(string name, int level, Vector2 shift)
    {
        Reset(shift); var skill = Equip(level);
        skill.Tick(skill.interval - .01f);
        Record(name + ": cooldown does not fire early", casts.Count == 0 && enemies[0].Hp == 10000);
        Vector2 expectedVisualCenter = CombatHitPlacement.Center(enemies[0].transform);
        skill.Tick(.02f);
        Record(name + ": strike damage and visual begin on the same tick", casts.Count == 1 && Effects().Length == 1 && Mathf.Abs(10000 - enemies[0].Hp - skill.damage) < .01f);
        var effect = Effects().First();
        Record(name + ": VFX anchor matches the selected word geometry", Vector2.Distance(effect.transform.position, expectedVisualCenter) < .001f);
        if (!Baseline) Record(name + ": VFX uses the actual AOE radius", Mathf.Abs(effect.transform.localScale.x - skill.strikeRadius / 1.1f) < .001f);
        yield return Sample(name + "-flash", effect, .035f); yield return Sample(name + "-residual", effect, .20f);
        for (int i = 1; i < skill.strikeCount; i++)
        {
            int count = casts.Count; skill.Tick(.17f);
            Record(name + ": follow-up " + i + " waits for .18 seconds", casts.Count == count);
            skill.Tick(.011f);
        }
        float total = skill.damage * skill.strikeCount;
        Record(name + ": original strike count and total damage", casts.Count == definition.StrikeCountAt(level) && Mathf.Abs(10000 - enemies[0].Hp - total) < .02f, "Strikes: " + casts.Count + "; damage: " + (10000 - enemies[0].Hp));
        Record(name + ": neighboring enemy inside AOE is hit", Mathf.Abs(10000 - enemies[1].Hp - total) < .02f);
        Record(name + ": enemy outside AOE stays unharmed", enemies[2].Hp == 10000);
        Record(name + ": duplicate colliders do not multiply damage", Mathf.Abs(10000 - enemies[0].Hp - total) < .02f);
        int events = casts.Count; skill.OnUnequip(); skill.Tick(20);
        Record(name + ": unequip cancels future strikes", casts.Count == events);
        yield return new WaitForSeconds(Baseline ? 2.2f : .6f);
        Record(name + ": expired lightning returns to the pool", Effects().Length == 0);
    }
    IEnumerator Motion(string name, bool mixed)
    {
        changeResolution?.Invoke(540, 960); yield return null; yield return new WaitForSecondsRealtime(.6f);
        Reset(Vector2.zero); var skill = Equip(5); skill.Tick(skill.interval - .02f); talismanCasts = grenadeCasts = 0;
        SkillAutoProjectileTalisman talisman = null; SkillThrowGrenade grenade = null;
        if (mixed)
        {
            skills.TryAddSkill(SkillId.AutoProjectileTalisman); talisman = skills.GetEquippedSkill<SkillAutoProjectileTalisman>(SkillId.AutoProjectileTalisman);
            while (talisman.Level < 5) talisman.OnLevelUp();
            AssetDatabase.LoadAssetAtPath<AutoProjectileSkillDefinition>("Assets/ScriptableObject/Skill/ActiveSkill/SkillDef_AutoProjectileTalisman.asset").ApplyStatsToSkill(talisman, 5);
            talisman.OnEquip(new SkillContext { player = player, enemyTag = skills.enemyTag }); talisman.Tick(talisman.interval - .02f);
            skills.TryAddSkill(SkillId.ThrowGrenade); grenade = skills.GetEquippedSkill<SkillThrowGrenade>(SkillId.ThrowGrenade);
            grenade.OnEquip(new SkillContext { player = player, enemyTag = skills.enemyTag }); grenade.Tick(grenade.cooldown - .02f);
        }
        string folder = Path.Combine(output, name + "-frames"); Directory.CreateDirectory(folder);
        var times = new List<float>(); float start = Time.realtimeSinceStartup, lastTick = Time.time; bool damaged = false; int visible = 0, peakSystems = 0, peakParticles = 0;
        while (Time.realtimeSinceStartup - start < 2.3f && times.Count < 100)
        {
            float delta = Time.time - lastTick; lastTick = Time.time; skill.Tick(delta); talisman?.Tick(delta); grenade?.Tick(delta);
            yield return new WaitForEndOfFrame();
            var effects = Effects(); if (effects.Length > 0) visible++;
            var systems = effects.SelectMany(e => e.GetComponentsInChildren<ParticleSystem>()).ToArray();
            peakSystems = Mathf.Max(peakSystems, systems.Length); peakParticles = Mathf.Max(peakParticles, systems.Sum(p => p.particleCount));
            damaged |= enemies[0].Hp < 10000; times.Add(Time.realtimeSinceStartup - start);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "frame-" + (times.Count - 1).ToString("D4") + ".png")); yield return null;
        }
        Record(name + ": video contains real damaging five-strike volley", damaged && casts.Count == 5 && visible > 0, "Casts: " + casts.Count + "; frames: " + times.Count + "; visible: " + visible);
        if (mixed) Record("Mixed replay includes actual grenade and talisman casts", talismanCasts > 0 && grenadeCasts > 0);
        File.WriteAllText(Path.Combine(output, name + "-times.json"), "[" + string.Join(",", times.Select(t => t.ToString(CultureInfo.InvariantCulture))) + "]");
        clips.Add(new { name, damaged, strikes = casts.Count, visibleFrames = visible, frames = times.Count, peakLightningSystems = peakSystems, peakLightningParticles = peakParticles, talismanCasts, grenadeCasts });
        skills.ClearAllSkills(); yield return new WaitForSeconds(2.3f); ReleaseEffects(); changeResolution?.Invoke(1080, 1920); yield return null;
    }
    IEnumerator PresentationChecks()
    {
        Reset(Vector2.zero);
        var template = BreakthroughSuite ? definition.maxLevelPrefab : definition.strikeFxPrefab; var particles = template.GetComponentsInChildren<ParticleSystem>(true);
        var materials = template.GetComponentsInChildren<ParticleSystemRenderer>(true).Select(r => r.sharedMaterial).Distinct().ToArray();
        string texturePath = RedesignSuite ? "Assets/Res/Textures/VFX/sheet_lightning_breakthrough_v2.png" : AtlasPath;
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        Record("Bounded one-particle layers share one authored atlas", particles.Length == (RedesignSuite ? 2 : BreakthroughSuite ? 3 : 2) && particles.All(p => p.main.maxParticles == 1) && materials.Length == (RedesignSuite ? 1 : BreakthroughSuite ? 2 : 1) && materials.All(m => m.mainTexture == texture));
        if (RedesignSuite) Record("Redesign uses dedicated red artwork and the standard alpha material", texturePath != AtlasPath && materials.All(m => m.shader.name == "Sprites/Default"));
        else if (BreakthroughSuite) Record("Breakthrough combines the blue core with a crimson material", materials.Any(m => m.shader.name == "GongkaoMower/LightningRedAccent") && materials.Any(m => m.shader.name == "Sprites/Default"));
        Record("Bounded atlas has no mipmaps or CPU readable copy", texture.width == (RedesignSuite ? 1024 : 512) && texture.height == (RedesignSuite ? 512 : 256) && texture.mipmapCount == 1 && !texture.isReadable && new FileInfo(texturePath).Length <= (RedesignSuite ? 256 : 128) * 1024);
        Record("Lightning is below monster text", template.GetComponentsInChildren<ParticleSystemRenderer>(true).All(r => r.sortingOrder == -5));
        var random = UnityEngine.Random.state; CombatVfxSpawner.TryPlayPooled(template, center, Quaternion.identity, 2.2f);
        Record("Visual playback does not consume combat random state", random.Equals(UnityEngine.Random.state));
        var effect = Effects().Single(); Record("Radius scaling uses the authored reference", Mathf.Abs(effect.transform.localScale.x - 2) < .001f);
        ReleaseEffects(); CombatVfxSpawner.TryPlayPooled(template, center, Quaternion.identity, 1.1f);
        Record("Reused lightning restores its scale", Mathf.Abs(Effects().Single().transform.localScale.x - 1) < .001f);
        yield return null; Time.timeScale = 0; yield return null;
        effect = Effects().Single(); var ps = effect.GetComponentsInChildren<ParticleSystem>(); var times = ps.Select(p => p.time).ToArray();
        yield return new WaitForSecondsRealtime(.2f);
        Record("Pause freezes both layers and pool expiry", effect.gameObject.activeSelf && ps.Select((p, i) => Mathf.Abs(p.time - times[i]) < .001f).All(v => v));
        Time.timeScale = 1; yield return new WaitForSeconds(.6f); Record("Resume finishes and recycles lightning", Effects().Length == 0);
        yield return null; int accepted = 0;
        for (int i = 0; i < 40; i++) if (CombatVfxSpawner.TryPlayPooled(template, center, Quaternion.identity)) accepted++;
        Record("Dense strikes obey existing VFX limits", accepted > 0 && accepted <= 8 && Effects().Length <= 32);
        int count = Effects().Length; var suppressed = Equip(1); suppressed.Tick(suppressed.interval + .01f);
        Record("Visual throttling never suppresses strike damage", Mathf.Abs(10000 - enemies[0].Hp - suppressed.damage) < .02f && Effects().Length == count);
        yield return new WaitForSeconds(.6f); Record("Dense strikes leave no active effects", Effects().Length == 0);
    }
    IEnumerator BreakthroughChecks()
    {
        Reset(Vector2.zero); var skill = Equip(1);
        Record("New level-one lightning starts with the ordinary presentation", !skill.IsBreakthroughVisual && skill.SelectedStrikeFxPrefab == definition.strikeFxPrefab);
        bool upgrades = true; for (int i = 0; i < 3; i++) upgrades &= skills.TryLevelUp(SkillId.LightningStrike);
        Record("Real progression reaches level four without breakthrough", upgrades && skill.Level == 4 && !skill.IsBreakthroughVisual && skill.SelectedStrikeFxPrefab == definition.strikeFxPrefab);
        Record("Missing bonded passive blocks the fifth level", !skills.HasPassiveSkill(definition.bondedPassiveId) && !skills.TryLevelUp(SkillId.LightningStrike) && skill.Level == 4);
        Record("The configured bonded passive can be acquired", skills.TryAddPassive(definition.bondedPassiveId));
        Record("Real fifth-level upgrade selects the breakthrough effect", skills.TryLevelUp(SkillId.LightningStrike) && skill.Level == definition.maxLevel && skill.IsBreakthroughVisual && skill.SelectedStrikeFxPrefab == definition.maxLevelPrefab);
        Record("Breakthrough keeps the original level-five gameplay values", Mathf.Approximately(skill.damage, definition.DamageAt(5)) && Mathf.Approximately(skill.interval, definition.IntervalAt(5)) && Mathf.Approximately(skill.strikeRadius, definition.StrikeRadiusAt(5)) && skill.strikeCount == definition.StrikeCountAt(5));
        // Capture the non-breakthrough level-four state, then a genuine upgraded fifth-level shot.
        var ordinary = Equip(4); ordinary.Tick(ordinary.interval + .01f);
        yield return Sample("level4-flash", Effects().Single(), .035f);
        Reset(Vector2.zero); skill = Equip(5); skill.Tick(skill.interval + .01f);
        Record("A genuine level-five strike spawns the configured breakthrough prefab", Effects().Single().name.StartsWith(definition.maxLevelPrefab.name, StringComparison.Ordinal));
        yield return Sample("breakthrough-flash", Effects().Single(), .035f);
        yield return Sample("breakthrough-residual", Effects().Single(), .20f);
        Reset(Vector2.zero); var legend = Equip(1);
        legend.ApplyLegendBreakthrough(1); Record("Rare stage does not enable the Legend presentation", !legend.IsBreakthroughVisual);
        legend.ApplyLegendBreakthrough(2); definition.ApplyStatsToSkill(legend, 1); legend.OnAfterStatsApplied();
        Record("Legend stage selects breakthrough and survives stats refresh", legend.IsBreakthroughVisual && legend.SelectedStrikeFxPrefab == definition.maxLevelPrefab);
        legend.Tick(legend.interval + .01f);
        Record("Legend presentation preserves level-one damage and strike count", casts.Count == 1 && Mathf.Abs(10000 - enemies[0].Hp - definition.DamageAt(1)) < .02f && Effects().Single().name.StartsWith(definition.maxLevelPrefab.name, StringComparison.Ordinal));
        legend.breakthroughFxPrefab = null;
        Record("A missing breakthrough asset safely falls back to ordinary lightning", legend.SelectedStrikeFxPrefab == definition.strikeFxPrefab);
        definition.ApplyStatsToSkill(legend, 1); legend.ApplyLegendBreakthrough(0);
        Record("Clearing Legend stage restores the ordinary presentation", !legend.IsBreakthroughVisual && legend.SelectedStrikeFxPrefab == definition.strikeFxPrefab);
        var fresh = Equip(1); Record("Newly equipped lightning does not inherit an old breakthrough state", !fresh.IsBreakthroughVisual && fresh.SelectedStrikeFxPrefab == definition.strikeFxPrefab);
        yield return null;
    }
    protected override void OnDestroy()
    {
        EventBus.Unsubscribe<SkillCastEvent>(OnCast);
        if (saved) { Time.timeScale = originalScale; UnityEngine.Random.state = randomState; }
        base.OnDestroy();
    }
}
#endif
