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

public sealed class FieldGeneratorValidationRunner : UIValidationRunnerBase
{
    bool Baseline => SessionState.GetBool("FieldGeneratorBaseline", false);
    bool SurfaceStyle { get { var d = definition != null ? definition : AssetDatabase.LoadAssetAtPath<FieldGeneratorSkillDefinition>(DefinitionPath); return !Baseline && d != null && d.fieldVisualPrefab != null && d.fieldVisualPrefab.GetComponent<FieldGeneratorPresentation>().UsesFieldSurface; } }
    bool OrbitStyle { get { var d = definition != null ? definition : AssetDatabase.LoadAssetAtPath<FieldGeneratorSkillDefinition>(DefinitionPath); return SurfaceStyle && d.fieldVisualPrefab.GetComponent<FieldGeneratorPresentation>().UsesFieldOrbit; } }
    protected override string OutputFolder { get { string custom = SessionState.GetString("SkillVfxToolkit.OutputFolder", ""); return !string.IsNullOrEmpty(custom) ? custom : (OrbitStyle ? "FieldGeneratorV3/After" : SurfaceStyle ? "FieldGeneratorV2/After" : "FieldGeneratorV1/" + (Baseline ? "Before" : "After")); } }
    protected override string ValidationNote => "Actual Game scene, configured field skill, real periodic AOE damage and memory-only save. Motion preserves actual elapsed times. Constant aura is not a pooled impact. No WeChat device or upload-size validation.";
    const string DefinitionPath = "Assets/ScriptableObject/Skill/ActiveSkill/SkillDef_FieldGenerator.asset";
    FieldGeneratorSkillDefinition definition;
    PlayerSkills skills;
    Transform player;
    readonly List<EnemyBase> enemies = new List<EnemyBase>();
    UnityEngine.Random.State randomState;
    float originalScale;
    bool saved;
    SkillFieldGenerator Equip(int level)
    {
        skills.ClearAllSkills(); skills.TryAddSkill(SkillId.FieldGenerator);
        var s = skills.GetEquippedSkill<SkillFieldGenerator>(SkillId.FieldGenerator);
        while (s.Level < level) s.OnLevelUp();
        definition.ApplyStatsToSkill(s, level); s.OnEquip(new SkillContext { player = player, enemyTag = skills.enemyTag }); return s;
    }
    void Reset(Vector2 shift, float radius)
    {
        player.position = new Vector2(15, 10) + shift;
        Camera.main.transform.position = new Vector3(15 + shift.x, 12 + shift.y, -10);
        for (int i=0;i<enemies.Count;i++) { enemies[i].ApplyTableStats(1, 10000, 0); enemies[i].transform.position = player.position + new Vector3((i == 0 ? .65f : 1.65f) * radius, .2f, 0); }
        Physics2D.SyncTransforms();
    }
    IEnumerator Snap(string name)
    {
        Time.timeScale = 0; Canvas.ForceUpdateCanvases(); yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png")); yield return null; yield return null; Time.timeScale = 1;
    }
    protected override IEnumerator RunChecks()
    {
        randomState = UnityEngine.Random.state; originalScale = Time.timeScale; saved = true;
        yield return SceneManager.UnloadSceneAsync("Home"); SelectedLevelContext.Set(1, 101);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/Game.unity", LoadSceneMode.Additive);
        SceneManager.SetActiveScene(SceneManager.GetSceneByName("Game")); yield return new WaitForSecondsRealtime(.4f);
        foreach (var spawner in FindObjectsOfType<SpawnerWaves>()) { spawner.StopAllCoroutines(); spawner.enabled = false; }
        foreach (var outcome in FindObjectsOfType<BattleOutcomeCoordinator>()) { outcome.StopAllCoroutines(); outcome.enabled = false; }
        foreach (var follow in FindObjectsOfType<CameraFollow2D>()) follow.enabled = false;
        UIManager.Instance.CloseAllStack(); foreach (var enemy in FindObjectsOfType<EnemyBase>()) Destroy(enemy.gameObject);
        yield return null; CombatTargetRegistry.ClearAll();
        skills = FindObjectOfType<PlayerSkills>(); player = skills.transform; skills.enabled = false; skills.ClearAllSkills();
        var controller = player.GetComponent<PlayerController>(); if (controller != null) controller.enabled = false;
        var body = player.GetComponent<Rigidbody2D>(); body.velocity = Vector2.zero; body.gravityScale = 0;
        skills.attackMultiplier = 1; skills.attackRangeMul = 1; skills.critRate = 0; skills.penRate = 0;
        Camera.main.orthographicSize = 11.5f;
        definition = AssetDatabase.LoadAssetAtPath<FieldGeneratorSkillDefinition>(DefinitionPath);
        for (int i=0;i<2;i++)
        {
            var go = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy/Normal/enemy001.prefab"));
            foreach (var b in go.GetComponentsInChildren<MonoBehaviour>()) if (!(b is EnemyBase) && !(b is EnemyWordLabel) && !(b is TMP_Text)) b.enabled = false;
            var rb = go.GetComponent<Rigidbody2D>(); if (rb != null) { rb.bodyType = RigidbodyType2D.Kinematic; rb.velocity = Vector2.zero; }
            go.GetComponentInChildren<EnemyWordLabel>().SetWord(i == 0 ? "护体罡气" : "范围外"); enemies.Add(go.GetComponent<EnemyBase>());
        }
        Time.timeScale = 1; yield return null;
        yield return Shot("normal", 1, false, Vector2.zero);
        yield return Shot("max", 5, false, Vector2.zero);
        yield return Shot("legend", 1, true, Vector2.zero);
        if (OrbitStyle) yield return Shot("grass-normal",4,false,new Vector2(-12,3));
        yield return Shot("grass-max", 5, false, new Vector2(-12, 3));
        if (SurfaceStyle) { yield return Motion("normal-motion", 4); yield return Motion("breakthrough-motion", 5); }
        else yield return Motion();
        if (!Baseline) { if (SurfaceStyle) yield return SurfacePresentationChecks(); else yield return NewPresentationChecks(); }
        skills.ClearAllSkills(); yield return null;
        Record("Unequip removes all field roots", GameObject.Find("FieldGeneratorRoot") == null);
        yield return SceneManager.UnloadSceneAsync("Game"); yield return null;
        Record("Battle exit leaves no field renderers", GameObject.Find("FieldGeneratorRoot") == null);
        Time.timeScale = originalScale; UnityEngine.Random.state = randomState;
    }
    IEnumerator Shot(string name, int level, bool legend, Vector2 shift)
    {
        Reset(shift, definition.RadiusAt(level)); var s = Equip(level); if (legend) s.ApplyLegendBreakthrough(2);
        yield return null;
        s.Tick(s.damageTickInterval - .01f);
        Record(name + ": no damage before original interval", enemies[0].Hp == 10000);
        s.Tick(.011f);
        Record(name + ": configured damage tick reaches inside target", Mathf.Abs(10000 - enemies[0].Hp - s.damagePerSecond * s.damageTickInterval) < .01f);
        Record(name + ": outside target takes no damage", enemies[1].Hp == 10000);
        Record(name + ": authored radius and pulse cycle match definition", Mathf.Approximately(s.radius, definition.RadiusAt(level)) && Mathf.Approximately(s.pulseCycleDuration, definition.PulseCycleDurationAt(level)));
        var root = player.Find("FieldGeneratorRoot");
        Record(name + ": one visible field follows player", root != null && root.GetComponentsInChildren<Renderer>().Length > 0);
        yield return Snap(name);
        s.OnUnequip(); yield return null;
    }
    IEnumerator Motion(string subfolder = "", int level = 5)
    {
        changeResolution?.Invoke(540, 960); yield return null; yield return new WaitForSecondsRealtime(.5f);
        Reset(Vector2.zero, definition.RadiusAt(level)); var s = Equip(level); yield return null;
        string destination = Path.Combine(output, subfolder);
        var times = new List<float>(); string frames = Path.Combine(destination, "frames"); Directory.CreateDirectory(frames);
        float start = Time.realtimeSinceStartup, last = Time.time; int peakRenderers = 0;
        while (Time.realtimeSinceStartup - start < (OrbitStyle ? 4 : 3) && times.Count < (OrbitStyle ? 125 : 95))
        {
            float dt = Time.time - last; last = Time.time; s.Tick(dt);
            yield return new WaitForEndOfFrame();
            var root = player.Find("FieldGeneratorRoot"); if (root != null) peakRenderers = Mathf.Max(peakRenderers, root.GetComponentsInChildren<Renderer>().Length);
            times.Add(Time.realtimeSinceStartup - start); ScreenCapture.CaptureScreenshot(Path.Combine(frames, "frame-" + (times.Count - 1).ToString("D4") + ".png"));
            yield return new WaitForSecondsRealtime(.025f);
        }
        Record("Motion captures real continuous field damage", enemies[0].Hp < 10000 && times.Count > 10);
        File.WriteAllText(Path.Combine(destination, "motion.json"), Newtonsoft.Json.JsonConvert.SerializeObject(new { baseline = Baseline, level, damaged = enemies[0].Hp < 10000, frames = times.Count, peakRenderers, elapsed = times.Last() }));
        File.WriteAllText(Path.Combine(destination, "frame-times.json"), "[" + string.Join(",", times.Select(t => t.ToString(CultureInfo.InvariantCulture))) + "]");
        s.OnUnequip(); yield return null; changeResolution?.Invoke(1080, 1920); yield return null;
    }
    IEnumerator SurfacePresentationChecks()
    {
        string current = File.ReadAllText(DefinitionPath), previous = File.ReadAllText("ArtOutput/VFX/FieldGeneratorV1/Baseline/SkillDef_FieldGenerator.asset");
        const string visuals = @"(?m)^  (fieldVisualPrefab|visualBaseDiameter|sortingOrder):[^\r\n]*(\r?\n)?";
        Record("Definition gameplay fields remain byte-equivalent", Regex.Replace(current, visuals, "").Replace("\r\n", "\n") == Regex.Replace(previous, visuals, "").Replace("\r\n", "\n"));
        Reset(Vector2.zero, definition.RadiusAt(1)); var s = Equip(1); yield return null;
        var view = player.GetComponentInChildren<FieldGeneratorPresentation>(); var root = view.transform;
        var renderers = root.GetComponentsInChildren<Renderer>(); var mesh = view.RuntimeFieldWaveMesh;
        var sourceMesh = definition.fieldVisualPrefab.transform.Find("HexWaves").GetComponent<MeshFilter>().sharedMesh;
        Record("Skill creates the configured surface field without sprites", view.UsesFieldSurface && view.CurrentSprite == null && root.GetComponentsInChildren<SpriteRenderer>().Length == 0);
        Record("Three layers use one shared material with no authored texture", renderers.Length == 3 && renderers.Select(r => r.sharedMaterial).Distinct().Count() == 1 && renderers.All(r => r.sharedMaterial.mainTexture == null));
        Record("No particles, colliders or impact-pool component are added", root.GetComponentsInChildren<ParticleSystem>().Length == 0 && root.GetComponentsInChildren<Collider2D>().Length == 0 && root.GetComponent<PooledCombatVfx>() == null);
        Record("Every layer stays below word enemies", renderers.All(r => r.sortingOrder == -5));
        Record("Three hex waves share one private dynamic mesh", view.HexWaveCount == 3 && mesh != null && mesh != sourceMesh && mesh.vertexCount == sourceMesh.vertexCount && mesh.vertexCount <= 750);
        Record("Wave traversal preserves the configured slower visual timing", Mathf.Approximately(view.FieldTravelSeconds, s.pulseCycleDuration * 1.6f));
        var block = new MaterialPropertyBlock(); var surface = root.Find("FieldSurface").GetComponent<Renderer>(); surface.GetPropertyBlock(block); Color tint = block.GetColor("_Color");
        Record("Ordinary field is green and translucent", !view.IsBreakthrough && tint.g > tint.r * 3 && tint.g > tint.b * 3 && tint.a > 0 && tint.a < .4f);
        var before = mesh.vertices; var random = UnityEngine.Random.state;
        for (int i=0;i<60;i++) view.TickVisual(.016f);
        Record("Wave geometry animates without consuming combat RNG", !before.SequenceEqual(mesh.vertices) && random.Equals(UnityEngine.Random.state));
        if (view.UsesFieldOrbit)
        {
            before=mesh.vertices; float orbit=view.FieldOrbitAngle; view.TickVisual(.2f); var next=mesh.vertices;
            float inner=Mathf.DeltaAngle(Mathf.Atan2(before[0].y,before[0].x)*Mathf.Rad2Deg,Mathf.Atan2(next[0].y,next[0].x)*Mathf.Rad2Deg);
            float outer=Mathf.DeltaAngle(Mathf.Atan2(before[125].y,before[125].x)*Mathf.Rad2Deg,Mathf.Atan2(next[125].y,next[125].x)*Mathf.Rad2Deg);
            Record("Inner and outer hex waves rotate in opposite directions",inner*outer<0 && Mathf.Abs(inner)>1 && Mathf.Abs(outer)>1 && view.FieldOrbitAngle!=orbit);
            Record("Ordinary state hides breakthrough corner geometry",mesh.colors32.Skip(view.FieldCornerVertexOffset).All(c=>c.a==0));
        }
        Record("Animation reuses the same mesh, renderers and materials", mesh == view.RuntimeFieldWaveMesh && root.GetComponentsInChildren<Renderer>().Length == 3 && root.GetComponentsInChildren<Renderer>().Select(r => r.sharedMaterial).SequenceEqual(renderers.Select(r => r.sharedMaterial)));
        bool contained = true; for (int i=0;i<240;i++) { view.TickVisual(.016f); contained &= mesh.vertices.All(v => v.sqrMagnitude <= 1.0001f); }
        Record("Expanding bands stay inside the real damage radius through multiple cycles", contained);
        var boundary = root.Find("Boundary").GetComponent<MeshRenderer>();
        Record("Boundary matches the actual skill radius", Mathf.Abs(boundary.bounds.extents.x - s.radius) < .01f);
        Vector3 scale = player.localScale; player.localScale = new Vector3(1.4f,.8f,1); player.position += Vector3.up; s.Tick(0);
        Record("Moving and scaling the owner preserve a circular world-space field", Vector3.Distance(root.position,player.position) < .001f && Mathf.Abs(boundary.bounds.extents.x - s.radius) < .01f && Mathf.Abs(boundary.bounds.extents.y - s.radius) < .01f);
        player.localScale = scale; s.Tick(0);
        bool upgraded = true; for (int i=0;i<3;i++) upgraded &= skills.TryLevelUp(SkillId.FieldGenerator);
        Record("Real progression keeps levels one through four green", upgraded && s.Level == 4 && !view.IsBreakthrough);
        Record("Original bonded passive gate still blocks level five", !skills.TryLevelUp(SkillId.FieldGenerator) && s.Level == 4);
        Record("Bonded passive can be acquired", skills.TryAddPassive(definition.bondedPassiveId));
        float ordinarySpeed=view.FieldOrbitDegreesPerSecond,ordinaryAngle=view.FieldOrbitAngle;
        bool reached = skills.TryLevelUp(SkillId.FieldGenerator); surface.GetPropertyBlock(block); tint = block.GetColor("_Color");
        Record("Level five changes to orange in the same mesh and view", reached && s.Level == 5 && view.IsBreakthrough && tint.r > tint.g * 1.8f && tint.g > tint.b * 5 && view.RuntimeFieldWaveMesh == mesh && player.GetComponentInChildren<FieldGeneratorPresentation>() == view);
        Record("Upgrading retains the authored radius, DPS and damage interval", Mathf.Approximately(s.radius,definition.RadiusAt(5)) && Mathf.Approximately(s.damagePerSecond,definition.DamagePerSecondAt(5)) && Mathf.Approximately(s.damageTickInterval,definition.DamageTickIntervalAt(5)));
        if (view.UsesFieldOrbit)
        {
            Record("Breakthrough adds six visible corner markers and gold-white arc cores",mesh.colors32.Skip(view.FieldCornerVertexOffset).Count(c=>c.a>100)==30 && mesh.colors32.Take(view.FieldCornerVertexOffset).Any(c=>c.r>240 && c.g>200 && c.b>100 && c.a>100));
            Record("Breakthrough increases orbit speed without resetting orientation",view.FieldOrbitDegreesPerSecond>ordinarySpeed*1.3f && Mathf.Approximately(view.FieldOrbitAngle,ordinaryAngle));
            bool containedAfterUpgrade=true; for(int i=0;i<180;i++) { view.TickVisual(.016f); containedAfterUpgrade &= mesh.vertices.All(v=>v.sqrMagnitude<=1.0001f && !float.IsNaN(v.x)); }
            Record("Breakthrough arcs and corner tips stay inside the damage boundary",containedAfterUpgrade);
        }
        skills.ClearAllSkills(); yield return null; Record("Unequip disposes the private dynamic mesh", mesh == null);
        skills.attackMultiplier = 1; Reset(Vector2.zero, definition.RadiusAt(1)); s = Equip(1); yield return null; view = player.GetComponentInChildren<FieldGeneratorPresentation>();
        s.ApplyLegendBreakthrough(1); Record("Existing positive Legend-stage rule is retained",view.IsBreakthrough);
        s.ApplyLegendBreakthrough(2); definition.ApplyStatsToSkill(s,1);
        Record("Legend colour survives a stats refresh without changing base DPS",view.IsBreakthrough && Mathf.Approximately(s.damagePerSecond,definition.DamagePerSecondAt(1)));
        s.ApplyLegendBreakthrough(0); Record("Clearing Legend returns to the green field",!view.IsBreakthrough);
        Time.timeScale = 0; yield return null; float phase = view.AnimationPhase, hp = enemies[0].Hp; before = view.RuntimeFieldWaveMesh.vertices;
        for (int i=0;i<3;i++) { s.Tick(Time.deltaTime); yield return new WaitForSecondsRealtime(.05f); }
        Record("Pause freezes field waves and periodic damage",phase == view.AnimationPhase && before.SequenceEqual(view.RuntimeFieldWaveMesh.vertices) && hp == enemies[0].Hp);
        Time.timeScale = 1; s.Tick(s.damageTickInterval+.001f);
        Record("Resume advances the field and original damage",phase != view.AnimationPhase && enemies[0].Hp < hp);
        s.OnUnequip(); hp = enemies[0].Hp; s.Tick(5); yield return null;
        Record("Unequip cancels damage and removes the presentation",hp == enemies[0].Hp && player.GetComponentInChildren<FieldGeneratorPresentation>() == null);
        s=Equip(1); yield return null; view=player.GetComponentInChildren<FieldGeneratorPresentation>();
        Record("Re-equipping creates one green field without stale breakthrough",player.GetComponentsInChildren<FieldGeneratorPresentation>().Length == 1 && !view.IsBreakthrough);
        skills.ClearAllSkills(); yield return null; Reset(Vector2.zero,definition.RadiusAt(1));
        var fallback=new SkillFieldGenerator(); fallback.ApplyRuntimeStats(definition.RadiusAt(1),definition.DamagePerSecondAt(1),definition.DamageTickIntervalAt(1),.7f);
        fallback.OnEquip(new SkillContext {player=player,enemyTag=skills.enemyTag}); fallback.Tick(fallback.damageTickInterval+.001f); view=player.GetComponentInChildren<FieldGeneratorPresentation>();
        Record("Missing art falls back to thin rings with original damage",view != null && !view.UsesFieldSurface && view.GetComponentsInChildren<Renderer>().Length == 2 && enemies[0].Hp < 10000);
        fallback.OnUnequip(); yield return null;
        var owner=new GameObject("FieldOwnerLifecycleFixture"); var ownerSkill=new SkillFieldGenerator {fieldVisualPrefab=definition.fieldVisualPrefab};
        ownerSkill.OnEquip(new SkillContext {player=owner.transform,enemyTag=skills.enemyTag}); var owned=owner.GetComponentInChildren<FieldGeneratorPresentation>(); mesh=owned.RuntimeFieldWaveMesh;
        Destroy(owner); yield return null; ownerSkill.Tick(1); ownerSkill.OnUnequip(); yield return null;
        Record("Owner destruction removes the field and its private mesh",owned == null && mesh == null);
    }
    IEnumerator NewPresentationChecks()
    {
        string current = File.ReadAllText(DefinitionPath), previous = File.ReadAllText("ArtOutput/VFX/FieldGeneratorV1/Baseline/SkillDef_FieldGenerator.asset");
        const string visuals = @"(?m)^  (fieldVisualPrefab|visualBaseDiameter|sortingOrder):[^\r\n]*(\r?\n)?";
        Record("Definition gameplay fields remain byte-equivalent", Regex.Replace(current, visuals, "").Replace("\r\n", "\n") == Regex.Replace(previous, visuals, "").Replace("\r\n", "\n"));
        var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Res/Textures/VFX/sheet_field_generator_v1.png");
        Record("Two authored states share one 512x256 atlas without mips or CPU copy", atlas.width == 512 && atlas.height == 256 && !atlas.isReadable && atlas.mipmapCount == 1);
        Record("Source PNG fits 192 KiB budget", new FileInfo(AssetDatabase.GetAssetPath(atlas)).Length <= 192 * 1024);
        Reset(Vector2.zero, definition.RadiusAt(1)); var s = Equip(1); yield return null;
        var view = player.GetComponentInChildren<FieldGeneratorPresentation>(); var root = view.transform;
        Record("Definition creates the authored persistent presentation", view != null && definition.fieldVisualPrefab.GetComponent<FieldGeneratorPresentation>() != null);
        Record("Normal state uses the amber sprite", !view.IsBreakthrough && view.CurrentSprite.name == "aura_gangqi_normal_v1");
        var renderers = root.GetComponentsInChildren<Renderer>(); var meshes = root.GetComponentsInChildren<MeshFilter>();
        Record("Aura has exactly three renderers and two shared materials", renderers.Length == 3 && renderers.Select(r => r.sharedMaterial).Distinct().Count() == 2);
        Record("Static ring geometry is shared by boundary and pulse", meshes.Length == 2 && meshes[0].sharedMesh == meshes[1].sharedMesh);
        Record("Persistent aura needs no particles or collision geometry", root.GetComponentsInChildren<ParticleSystem>().Length == 0 && root.GetComponentsInChildren<Collider2D>().Length == 0);
        Record("Aura does not use the impact return-to-pool component", root.GetComponent<PooledCombatVfx>() == null);
        Record("Every authored layer stays below word enemies", renderers.All(r => r.sortingOrder == -5));
        var random = UnityEngine.Random.state; for (int i=0;i<60;i++) view.TickVisual(.016f);
        Record("Visual motion does not consume combat RNG", random.Equals(UnityEngine.Random.state));
        Record("Ticking creates no extra renderers or material instances", root.GetComponentsInChildren<Renderer>().Length == 3 && root.GetComponentsInChildren<Renderer>().Select(r => r.sharedMaterial).SequenceEqual(renderers.Select(r => r.sharedMaterial)));
        var boundary = root.Find("Boundary").GetComponent<MeshRenderer>();
        Record("Boundary radius matches actual level-one radius", Mathf.Abs(boundary.bounds.extents.x - s.radius) < .01f);
        Vector3 originalPlayerScale = player.localScale; player.localScale = new Vector3(1.4f,.8f,1); player.position += Vector3.up;
        s.Tick(0);
        Record("Following and player scaling preserve world-space radius", Vector3.Distance(root.position, player.position) < .001f && Mathf.Abs(boundary.bounds.extents.x - s.radius) < .01f && Mathf.Abs(boundary.bounds.extents.y - s.radius) < .01f);
        player.localScale = originalPlayerScale; s.Tick(0);
        bool upgraded = true; for (int i=0;i<3;i++) upgraded &= skills.TryLevelUp(SkillId.FieldGenerator);
        Record("Real progression reaches level four with ordinary art", upgraded && s.Level == 4 && !view.IsBreakthrough);
        Record("Original bonded-passive gate blocks level five", !skills.TryLevelUp(SkillId.FieldGenerator) && s.Level == 4);
        Record("Original bonded passive can be acquired", skills.TryAddPassive(definition.bondedPassiveId));
        Record("Level-five upgrade selects red-gold art in the same instance", skills.TryLevelUp(SkillId.FieldGenerator) && s.Level == 5 && view.IsBreakthrough && player.GetComponentInChildren<FieldGeneratorPresentation>() == view && view.CurrentSprite.name == "aura_gangqi_breakthrough_v1");
        Record("Level-five gameplay values retain their configured values", Mathf.Approximately(s.radius, definition.RadiusAt(5)) && Mathf.Approximately(s.damagePerSecond, definition.DamagePerSecondAt(5)) && Mathf.Approximately(s.damageTickInterval, definition.DamageTickIntervalAt(5)));
        skills.attackMultiplier = 1;
        Reset(Vector2.zero, definition.RadiusAt(1)); s = Equip(1); yield return null; view = player.GetComponentInChildren<FieldGeneratorPresentation>();
        s.ApplyLegendBreakthrough(1); Record("Existing positive Legend-stage rule is retained", view.IsBreakthrough);
        s.ApplyLegendBreakthrough(2); definition.ApplyStatsToSkill(s,1);
        Record("Legend state survives stats refresh without changing base DPS", view.IsBreakthrough && Mathf.Approximately(s.damagePerSecond, definition.DamagePerSecondAt(1)));
        s.ApplyLegendBreakthrough(0); Record("Clearing Legend restores ordinary art", !view.IsBreakthrough);
        Time.timeScale = 0; yield return null; float phase = view.AnimationPhase; var rotation = view.CurrentSprite != null ? view.transform.Find("RotatingQi").localRotation : Quaternion.identity;
        float hp = enemies[0].Hp;
        for (int i=0;i<3;i++) { s.Tick(Time.deltaTime); yield return new WaitForSecondsRealtime(.05f); }
        Record("Pause freezes qi, pulse and periodic damage", Mathf.Abs(view.AnimationPhase-phase) < .0001f && Quaternion.Angle(rotation, view.transform.Find("RotatingQi").localRotation) < .001f && hp == enemies[0].Hp);
        Time.timeScale = 1; s.Tick(s.damageTickInterval + .001f);
        Record("Resume advances animation and original damage", view.AnimationPhase != phase && enemies[0].Hp < hp);
        s.OnUnequip(); hp = enemies[0].Hp; s.Tick(5); yield return null;
        Record("Unequip cancels damage and removes the authored view", hp == enemies[0].Hp && player.GetComponentInChildren<FieldGeneratorPresentation>() == null);
        s = Equip(1); yield return null; view = player.GetComponentInChildren<FieldGeneratorPresentation>();
        Record("Re-equipping creates one ordinary field without stale breakthrough", player.GetComponentsInChildren<FieldGeneratorPresentation>().Length == 1 && !view.IsBreakthrough);
        skills.ClearAllSkills(); yield return null;
        Reset(Vector2.zero, definition.RadiusAt(1));
        var fallback = new SkillFieldGenerator(); fallback.ApplyRuntimeStats(definition.RadiusAt(1), definition.DamagePerSecondAt(1), definition.DamageTickIntervalAt(1), .7f);
        fallback.OnEquip(new SkillContext { player=player, enemyTag=skills.enemyTag }); fallback.Tick(fallback.damageTickInterval + .001f);
        view = player.GetComponentInChildren<FieldGeneratorPresentation>();
        Record("Missing art falls back to two thin rings and still deals original damage", view != null && view.CurrentSprite == null && view.GetComponentsInChildren<Renderer>().Length == 2 && enemies[0].Hp < 10000);
        fallback.OnUnequip(); yield return null;
        var owner = new GameObject("AuraOwnerLifecycleFixture"); var ownerSkill = new SkillFieldGenerator { fieldVisualPrefab = definition.fieldVisualPrefab };
        ownerSkill.OnEquip(new SkillContext { player=owner.transform, enemyTag=skills.enemyTag }); var owned = owner.GetComponentInChildren<FieldGeneratorPresentation>();
        Destroy(owner); yield return null; ownerSkill.Tick(1); ownerSkill.OnUnequip();
        Record("Destroying the owner leaves no orphaned aura", owned == null);
    }
    protected override void OnDestroy() { if (saved) { Time.timeScale = originalScale; UnityEngine.Random.state = randomState; } base.OnDestroy(); }
}
#endif
