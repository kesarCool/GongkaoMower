#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Actual grenade/missile impact checks plus shared-atlas playback and budget proof.</summary>
public sealed class ExplosionLibraryValidationRunner : UIValidationRunnerBase
{
    protected override string OutputFolder { get { string custom = SessionState.GetString("SkillVfxToolkit.OutputFolder", ""); return !string.IsNullOrEmpty(custom) ? custom : SessionState.GetBool("VfxTiming15Validation", false) ? "Timing15/Explosions" : "ExplosionLibraryV2"; } }
    protected override string ValidationNote => "Real Game scene, actual configured skills/projectiles and damaging impacts; isolated memory save. Stills sample specified particle times and include a translated grass fixture. Videos use actual elapsed-time captures. Editor only; not WeChat device or upload-size validation.";
    const string VfxPath = "Assets/Res/Prefabs/Effects/Impact/fx_explosion_";
    readonly string[] variants = { "compact", "chain", "missile", "heavy" };
    readonly SkillId[] ids = { SkillId.ThrowGrenade, SkillId.BouncingGrenade, SkillId.HomingMissileBasic, SkillId.HomingMissile };
    readonly string[] definitions = { "SkillDef_ThrowGrenade", "SkillDef_BouncingGrenade", "SkillDef_HomingMissileBasic", "SkillDef_HomingMissile" };
    readonly List<EnemyBase> targets = new List<EnemyBase>();
    readonly List<Clip> clips = new List<Clip>();
    readonly Vector2 origin = new Vector2(15, 10), impact = new Vector2(15, 15);
    GameObject[] prefabs;
    PlayerSkills playerSkills;
    Transform player;
    UnityEngine.Random.State originalRandom;
    float originalTimeScale;
    bool saved;
    [Serializable] sealed class Clip { public string name; public bool damaged; public int visibleFrames; public List<float> times = new List<float>(); }
    [Serializable] sealed class ClipList { public List<Clip> clips; }
    PooledCombatVfx[] Effects() => FindObjectsOfType<PooledCombatVfx>().Where(v => v.name.StartsWith("fx_explosion_", StringComparison.Ordinal)).ToArray();
    void Release(GameObject go) { CombatVfxSpawner.NotifyReleased(go); GameObjectPool.Release(go); }
    void ResetTargets()
    {
        player.position = origin;
        Camera.main.transform.position = new Vector3(15, 14, -10);
        for (int i = 0; i < targets.Count; i++)
        {
            targets[i].transform.position = impact + new[] { Vector2.zero, new Vector2(0, .95f), new Vector2(2.6f, 1.9f) }[i];
            targets[i].ApplyTableStats(1, 10000, 0);
        }
        Physics2D.SyncTransforms();
    }
    ISkill Equip(int index)
    {
        playerSkills.ClearAllSkills();
        playerSkills.TryAddSkill(ids[index]);
        ISkill result = index < 2 ? (ISkill)playerSkills.GetEquippedSkill<SkillThrowGrenade>(ids[index]) : playerSkills.GetEquippedSkill<SkillHomingMissile>(ids[index]);
        result.OnEquip(new SkillContext { player = player, enemyTag = playerSkills.enemyTag });
        return result;
    }
    float Cooldown(ISkill skill) => skill is SkillThrowGrenade ? ((SkillThrowGrenade)skill).cooldown : ((SkillHomingMissile)skill).cooldown;
    float Damage(ISkill skill) => skill is SkillThrowGrenade ? ((SkillThrowGrenade)skill).damage : ((SkillHomingMissile)skill).damage;
    IEnumerator Sample(string filename, PooledCombatVfx effect, float time, bool grass)
    {
        if (SessionState.GetBool("VfxTiming15Validation", false)) time *= 1.5f;
        Time.timeScale = 0;
        Vector3 shift = grass ? new Vector3(-12f, 3f, 0) : Vector3.zero;
        player.position += shift;
        foreach (var target in targets) target.transform.position += shift;
        Camera.main.transform.position += shift;
        effect.transform.position += shift;
        effect.GetComponent<ParticleSystem>().Simulate(time, true, true, false);
        Canvas.ForceUpdateCanvases();
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(output, filename + ".png"));
        yield return null; yield return null;
        player.position -= shift;
        foreach (var target in targets) target.transform.position -= shift;
        Camera.main.transform.position -= shift;
        effect.transform.position -= shift;
        Time.timeScale = 1;
    }
    protected override IEnumerator RunChecks()
    {
        originalRandom = UnityEngine.Random.state; originalTimeScale = Time.timeScale; saved = true;
        yield return SceneManager.UnloadSceneAsync("Home");
        SelectedLevelContext.Set(1, 101);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/Game.unity", LoadSceneMode.Additive);
        SceneManager.SetActiveScene(SceneManager.GetSceneByName("Game"));
        yield return new WaitForSecondsRealtime(.4f);
        foreach (var spawner in FindObjectsOfType<SpawnerWaves>()) { spawner.StopAllCoroutines(); spawner.enabled = false; }
        foreach (var outcome in FindObjectsOfType<BattleOutcomeCoordinator>()) { outcome.StopAllCoroutines(); outcome.enabled = false; }
        foreach (var follow in FindObjectsOfType<CameraFollow2D>()) follow.enabled = false;
        UIManager.Instance.CloseAllStack();
        foreach (var enemy in FindObjectsOfType<EnemyBase>()) Destroy(enemy.gameObject);
        yield return null; CombatTargetRegistry.ClearAll();
        playerSkills = FindObjectOfType<PlayerSkills>(); player = playerSkills.transform;
        playerSkills.enabled = false; playerSkills.ClearAllSkills();
        var controller = player.GetComponent<PlayerController>(); if (controller != null) controller.enabled = false;
        var body = player.GetComponent<Rigidbody2D>(); if (body != null) { body.velocity = Vector2.zero; body.gravityScale = 0; }
        playerSkills.attackMultiplier = 1; playerSkills.attackRangeMul = 1; playerSkills.critRate = 0; playerSkills.penRate = 0;
        Camera.main.orthographicSize = 11.5f;
        var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy/Normal/enemy001.prefab");
        for (int i = 0; i < 3; i++)
        {
            var go = Instantiate(enemyPrefab, impact, Quaternion.identity);
            foreach (var behavior in go.GetComponentsInChildren<MonoBehaviour>())
                if (!(behavior is EnemyBase) && !(behavior is EnemyWordLabel) && !(behavior is TMP_Text)) behavior.enabled = false;
            var rb = go.GetComponent<Rigidbody2D>(); if (rb != null) { rb.bodyType = RigidbodyType2D.Kinematic; rb.velocity = Vector2.zero; rb.gravityScale = 0; }
            go.GetComponentInChildren<EnemyWordLabel>().SetWord(new[] { "北京烤鸭", "炸酱面", "牡丹燕菜" }[i]);
            targets.Add(go.GetComponent<EnemyBase>());
        }
        prefabs = variants.Select(v => AssetDatabase.LoadAssetAtPath<GameObject>(VfxPath + v + "_v2.prefab")).ToArray();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Res/Textures/VFX/sheet_explosion_library_v2.png");
        Record("Four variants share one 1024x512 texture with no CPU copy or mip chain", texture.width == 1024 && texture.height == 512 && !texture.isReadable && texture.mipmapCount == 1);
        Record("Shared atlas source PNG stays below 512 KiB", new FileInfo(AssetDatabase.GetAssetPath(texture)).Length <= 512 * 1024);
        var materials = prefabs.Select(p => p.GetComponent<ParticleSystemRenderer>().sharedMaterial).ToArray();
        Record("All four effects share one material and one texture", materials.All(m => m == materials[0] && m.mainTexture == texture));
        Record("Explosion variants use only their small shared atlas", Directory.GetFiles("Assets/Res/Textures/VFX", "sheet_explosion*.png").Length == 1 && !File.Exists("Assets/Res/Textures/VFX/sheet_grenade_explosion_v1.png"));
        for (int i = 0; i < 4; i++)
        {
            var ps = prefabs[i].GetComponent<ParticleSystem>(); var sheet = ps.textureSheetAnimation;
            Record(variants[i] + ": eight-frame segment cannot enter another variant", sheet.numTilesX == 8 && sheet.numTilesY == 4 && sheet.cycleCount == 1 &&
                Mathf.Abs(sheet.frameOverTime.curve.Evaluate(0) - i / 4f) < .0001f && sheet.frameOverTime.curve.Evaluate(1) < (i + 1) / 4f);
            Record(variants[i] + ": one deterministic particle and scaled-time animation", ps.main.maxParticles == 1 && !ps.useAutoRandomSeed && !ps.main.useUnscaledTime);
            var def = AssetDatabase.LoadAssetAtPath<SkillDefinitionBase>("Assets/ScriptableObject/Skill/ActiveSkill/" + definitions[i] + ".asset");
            var so = new SerializedObject(def);
            Record(variants[i] + ": real skill binding selects intended prefab", so.FindProperty("explosionFxPrefab").objectReferenceValue == prefabs[i]);
        }
        Time.timeScale = 1; yield return null;
        for (int i = 0; i < 4; i++) yield return CastCase(i);
        yield return ProbeEcho();
        ResetTargets();
        var random = UnityEngine.Random.state;
        CombatVfxSpawner.TryPlayPooled(prefabs[3], impact, Quaternion.identity, 2f);
        var large = Effects().Single();
        Record("Presentation spawn preserves the combat random generator", random.Equals(UnityEngine.Random.state));
        Record("Explosion follows actual radius", Mathf.Abs(large.transform.localScale.x - 2f / 1.2f) < .001f);
        yield return null; Time.timeScale = 0; yield return null;
        var particle = large.GetComponent<ParticleSystem>(); float paused = particle.time;
        yield return new WaitForSecondsRealtime(.16f);
        Record("Pause freezes particle time and pool expiry", large.gameObject.activeSelf && Mathf.Abs(particle.time - paused) < .001f);
        Time.timeScale = 1; int instance = large.GetInstanceID(); Release(large.gameObject);
        CombatVfxSpawner.TryPlayPooled(prefabs[3], impact, Quaternion.identity, .6f);
        var small = Effects().Single();
        Record("Pool reuse resets scale and time without material instances", small.GetInstanceID() == instance && Mathf.Abs(small.transform.localScale.x - .5f) < .001f &&
            small.GetComponent<ParticleSystem>().time < .03f && small.GetComponent<ParticleSystemRenderer>().sharedMaterial == materials[0]);
        Release(small.gameObject); yield return null;
        int accepted = 0;
        for (int i = 0; i < 40; i++) if (CombatVfxSpawner.TryPlayPooled(prefabs[i % 4], impact, Quaternion.identity, 1.2f)) accepted++;
        var limit = SpawnLimiter.Instance.configs.First(c => c.key == CombatVfxSpawner.SpawnLimiterKey);
        Record("Mixed variants obey the existing per-frame and alive caps", accepted > 0 && accepted <= limit.spawnPerFrame && Effects().Length <= limit.maxAlive,
            "Accepted: " + accepted + "; alive: " + Effects().Length);
        yield return new WaitForSeconds(.9f);
        Record("Mixed bursts recycle and unregister", Effects().Length == 0 && limit.currentAlive == 0);
        File.WriteAllText(Path.Combine(output, "clips.json"), JsonUtility.ToJson(new ClipList { clips = clips }, true));
        playerSkills.ClearAllSkills();
        foreach (var target in targets) if (target != null) Destroy(target.gameObject);
        CombatVfxSpawner.TryPlayPooled(prefabs[0], impact, Quaternion.identity, 1.2f);
        var leaving = Effects().First();
        yield return SceneManager.UnloadSceneAsync("Game"); yield return null;
        Record("Leaving battle clears borrowed effects", leaving == null);
        Time.timeScale = originalTimeScale; UnityEngine.Random.state = originalRandom;
    }
    IEnumerator CastCase(int index)
    {
        changeResolution?.Invoke(1080, 1920); yield return null;
        float baselineDamage = -1f;
        if (index >= 2)
        {
            // Existing PlayerBullet applies contact damage before the missile's AOE.
            // Compare against the actual same-config cast with VFX disabled, rather than
            // assuming the primary target must receive only the configured AOE damage.
            ResetTargets();
            var castRandom = UnityEngine.Random.state;
            var baseline = (SkillHomingMissile)Equip(index);
            baseline.explosionFxPrefab = null;
            baseline.Tick(baseline.cooldown + .01f);
            float baselineDeadline = Time.realtimeSinceStartup + 6;
            while (targets[0].Hp == 10000 && Time.realtimeSinceStartup < baselineDeadline) yield return null;
            baselineDamage = 10000 - targets[0].Hp;
            Record(variants[index] + ": presentation-free combat baseline measured", baselineDamage > 0 && Effects().Length == 0, "Observed primary-target damage: " + baselineDamage);
            yield return new WaitForSeconds(.9f);
            playerSkills.ClearAllSkills();
            UnityEngine.Random.state = castRandom;
        }
        ResetTargets(); var skill = Equip(index); float cd = Cooldown(skill), damage = Damage(skill);
        skill.Tick(cd - .02f);
        Record(variants[index] + ": original cooldown prevents early damage/projectiles", targets.All(e => e.Hp == 10000) && FindObjectsOfType<GrenadeProjectile>().Length == 0 && FindObjectsOfType<HomingMissileBullet>().Length == 0);
        skill.Tick(.03f);
        Record(variants[index] + ": actual cast launches before damage", targets.All(e => e.Hp == 10000) && (FindObjectsOfType<GrenadeProjectile>().Length > 0 || FindObjectsOfType<HomingMissileBullet>().Length > 0));
        float deadline = Time.realtimeSinceStartup + 6;
        while (targets[0].Hp == 10000 && Time.realtimeSinceStartup < deadline) yield return null;
        float expectedDamage = baselineDamage >= 0 ? baselineDamage : damage;
        Record(variants[index] + ": real hit retains original combat result", Mathf.Abs(targets[0].Hp - (10000 - expectedDamage)) < .005f,
            "Damage: " + (10000 - targets[0].Hp) + "; baseline: " + expectedDamage + "; configured projectile damage: " + damage);
        Record(variants[index] + ": distant target remains unharmed", targets[2].Hp == 10000);
        var effect = Effects().FirstOrDefault(e => e.name.StartsWith("fx_explosion_" + variants[index], StringComparison.Ordinal));
        Record(variants[index] + ": real landing selects new effect", effect != null);
        if (effect != null)
        {
            float peakTime = new[] { .105f, .075f, .07f, .08f }[index];
            yield return Sample(variants[index] + "-sand", effect, peakTime, false);
            yield return Sample(variants[index] + "-grass", effect, peakTime, true);
        }
        yield return new WaitForSeconds(.9f);
        Record(variants[index] + ": one-shot effect returns automatically", Effects().Length == 0);
        yield return RecordMotion(index);
        playerSkills.ClearAllSkills();
        yield return new WaitForSeconds(.9f);
    }
    IEnumerator RecordMotion(int index)
    {
        changeResolution?.Invoke(540, 960); yield return null; yield return new WaitForSeconds(.8f);
        ResetTargets(); var skill = Equip(index); skill.Tick(Cooldown(skill) - .02f);
        var clip = new Clip { name = variants[index] }; clips.Add(clip);
        string folder = Path.Combine(output, variants[index] + "-frames"); Directory.CreateDirectory(folder);
        float started = Time.realtimeSinceStartup, prepared = Time.time, landed = -1;
        bool launched = false;
        while (clip.times.Count < 80 && Time.realtimeSinceStartup - started < 8)
        {
            if (!launched && Time.time - prepared >= .02f) { skill.Tick(Time.time - prepared); launched = true; }
            if (!clip.damaged && targets[0].Hp < 10000) { clip.damaged = true; landed = Time.time; }
            yield return new WaitForEndOfFrame();
            if (Effects().Any(e => e.name.StartsWith("fx_explosion_" + variants[index], StringComparison.Ordinal))) clip.visibleFrames++;
            clip.times.Add(Time.realtimeSinceStartup - started);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "frame-" + (clip.times.Count - 1).ToString("D4") + ".png"));
            if (clip.damaged && Time.time - landed > .9f) break;
            yield return new WaitForSecondsRealtime(.025f);
        }
        Record(variants[index] + ": motion captures actual damaging impact", launched && clip.damaged && clip.visibleFrames > 0,
            "Frames: " + clip.times.Count + "; visible: " + clip.visibleFrames);
    }
    IEnumerator ProbeEcho()
    {
        ResetTargets();
        var echo = AssetDatabase.LoadAssetAtPath<GameObject>(VfxPath + "echo_v2.prefab");
        var probe = new GameObject("EchoValidationProbe"); probe.transform.position = impact;
        probe.AddComponent<ChainExplosionSpawner>().Init(1, .72f, playerSkills.enemyTag, echo, SkillId.BouncingGrenade);
        float deadline = Time.realtimeSinceStartup + 2;
        PooledCombatVfx effect = null;
        while (effect == null && Time.realtimeSinceStartup < deadline) { effect = Effects().FirstOrDefault(e => e.name.StartsWith("fx_explosion_echo", StringComparison.Ordinal)); yield return null; }
        Record("Delayed secondary explosion uses the shared atlas at its smaller radius", effect != null && Mathf.Abs(effect.transform.localScale.x - .6f) < .001f &&
            effect.GetComponent<ParticleSystemRenderer>().sharedMaterial == prefabs[1].GetComponent<ParticleSystemRenderer>().sharedMaterial);
        yield return new WaitForSeconds(.6f);
    }
    protected override void OnDestroy()
    {
        if (saved) { Time.timeScale = originalTimeScale; UnityEngine.Random.state = originalRandom; }
        base.OnDestroy();
    }
}
#endif
