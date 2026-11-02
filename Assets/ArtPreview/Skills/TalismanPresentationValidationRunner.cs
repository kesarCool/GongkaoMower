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

public sealed class TalismanPresentationValidationRunner : UIValidationRunnerBase
{
    bool Baseline => SessionState.GetBool("TalismanPresentationBaseline", false);
    protected override string OutputFolder { get { string custom = SessionState.GetString("SkillVfxToolkit.OutputFolder", ""); return !string.IsNullOrEmpty(custom) ? custom : SessionState.GetBool("VfxTiming15Validation", false) ? "Timing15/Talisman" : Baseline ? "TalismanBefore" : "TalismanAfter"; } }
    protected override string ValidationNote => "Real Game scene and configured talisman projectiles, actual collision damage, memory-only save. Flight stills freeze a real shot; hit stills sample the triggered particle at " +
        (SessionState.GetBool("VfxTiming15Validation", false) ? ".0825s" : ".055s") +
        ". Motion uses actual elapsed time. No WeChat device/upload measurement.";
    AutoProjectileSkillDefinition definition;
    PlayerSkills skills;
    Transform player;
    EnemyBase enemy;
    Vector2 offset;
    readonly Vector2 origin = new Vector2(15, 10), target = new Vector2(15, 16);
    UnityEngine.Random.State originalRandom;
    float originalScale;
    bool saved;
    PooledCombatVfx[] Hits() => FindObjectsOfType<PooledCombatVfx>().Where(v => v.name.StartsWith("fx_talisman_hit", StringComparison.Ordinal)).ToArray();
    PlayerBullet[] Bullets() => FindObjectsOfType<PlayerBullet>().Where(b => b.skillSource == SkillId.AutoProjectileTalisman).ToArray();
    void ResetTarget(Vector2 shift)
    {
        offset = shift; player.position = origin + shift; enemy.transform.position = target + shift; enemy.ApplyTableStats(1, 10000, 0);
        Camera.main.transform.position = new Vector3(15 + shift.x, 14 + shift.y, -10); Physics2D.SyncTransforms();
    }
    SkillAutoProjectileTalisman Equip(int level)
    {
        skills.ClearAllSkills(); skills.TryAddSkill(SkillId.AutoProjectileTalisman);
        var skill = skills.GetEquippedSkill<SkillAutoProjectileTalisman>(SkillId.AutoProjectileTalisman);
        while (skill.Level < level) skill.OnLevelUp(); definition.ApplyStatsToSkill(skill, level);
        skill.OnEquip(new SkillContext { player = player, enemyTag = skills.enemyTag }); return skill;
    }
    IEnumerator Snap(string name, PooledCombatVfx hit = null)
    {
        Time.timeScale = 0;
        if (hit != null) hit.GetComponent<ParticleSystem>().Simulate(.055f * (SessionState.GetBool("VfxTiming15Validation", false) ? 1.5f : 1), true, true, false);
        Canvas.ForceUpdateCanvases(); yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png")); yield return null; yield return null;
        Time.timeScale = 1;
    }
    protected override IEnumerator RunChecks()
    {
        originalRandom = UnityEngine.Random.state; originalScale = Time.timeScale; saved = true;
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
        skills.attackMultiplier = 1; skills.attackRangeMul = 1; skills.critRate = 0; skills.penRate = 0; skills.pierceCount = 0; skills.pierceRate = 0;
        Camera.main.orthographicSize = 11.5f;
        definition = AssetDatabase.LoadAssetAtPath<AutoProjectileSkillDefinition>("Assets/ScriptableObject/Skill/ActiveSkill/SkillDef_AutoProjectileTalisman.asset");
        var go = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy/Normal/enemy001.prefab"), target, Quaternion.identity);
        foreach (var b in go.GetComponentsInChildren<MonoBehaviour>()) if (!(b is EnemyBase) && !(b is EnemyWordLabel) && !(b is TMP_Text)) b.enabled = false;
        var body = go.GetComponent<Rigidbody2D>(); if (body == null) body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic; body.velocity = Vector2.zero; body.gravityScale = 0;
        go.GetComponentInChildren<EnemyWordLabel>().SetWord("北京烤鸭"); enemy = go.GetComponent<EnemyBase>();
        Time.timeScale = 1; yield return null;
        Record("Skill definition bytes remain identical to the pre-task backup", File.ReadAllText("Assets/ScriptableObject/Skill/ActiveSkill/SkillDef_AutoProjectileTalisman.asset") ==
            File.ReadAllText("Library/VFXBackups/TalismanPresentation-20261025/Assets/ScriptableObject/Skill/ActiveSkill/SkillDef_AutoProjectileTalisman.asset"));
        Record("Normal and maximum collision radii remain .30 and .36", Mathf.Approximately(definition.bulletPrefab.GetComponent<CircleCollider2D>().radius, .3f) &&
            Mathf.Approximately(definition.maxLevelPrefab.GetComponent<CircleCollider2D>().radius, .36f));
        yield return Shot("normal", 1, false, Vector2.zero);
        yield return Shot("max", 5, false, Vector2.zero);
        yield return Shot("burst", 5, true, Vector2.zero);
        yield return Shot("grass-max", 5, false, new Vector2(-12, 3));
        yield return Motion();
        if (!Baseline) yield return NewPresentationChecks();
        skills.ClearAllSkills();
        yield return SceneManager.UnloadSceneAsync("Game"); yield return null;
        Record("Leaving battle removes borrowed talisman effects", Hits().Length == 0);
        Time.timeScale = originalScale; UnityEngine.Random.state = originalRandom;
    }
    IEnumerator Shot(string name, int level, bool burst, Vector2 shift)
    {
        ResetTarget(shift); var skill = Equip(level); float expectedDamage; Time.timeScale = .25f;
        if (burst)
        {
            var bullet = GameObjectPool.Get(definition.burstBulletPrefab, origin + shift, Quaternion.identity).GetComponent<AutoProjectileBurstBullet>();
            bullet.LaunchBurst(Vector2.up, definition.bulletSpeed, 3, 5, SkillId.AutoProjectileTalisman, 0, false, 0, false, 0, 0, 0, player);
            expectedDamage = 3;
        }
        else
        {
            skill.Tick(skill.interval - .02f);
            Record(name + ": original cooldown prevents early launch", Bullets().Length == 0 && enemy.Hp == 10000);
            skill.Tick(.03f); expectedDamage = definition.DamageAt(level) * definition.ProjectileCountAt(level);
            Record(name + ": configured number of projectiles launches", Bullets().Length == definition.ProjectileCountAt(level));
        }
        yield return new WaitForSeconds(.12f);
        Record(name + ": flight has not caused early damage", enemy.Hp == 10000);
        if (!Baseline && level == 5) Record(name + ": smooth trails contain real flight positions", Bullets().SelectMany(b => b.GetComponentsInChildren<TrailRenderer>()).Any(t => t.positionCount > 1));
        yield return Snap(name + "-flight");
        float deadline = Time.realtimeSinceStartup + 5;
        while (enemy.Hp == 10000 && Time.realtimeSinceStartup < deadline) yield return null;
        Record(name + ": a real collision resolves damage", enemy.Hp < 10000);
        Record(name + ": hit feedback appears only in the new presentation", Baseline ? Hits().Length == 0 : Hits().Length > 0);
        yield return Snap(name + "-hit", Hits().FirstOrDefault());
        yield return new WaitForSeconds(.55f);
        Record(name + ": complete volley retains original total damage", Mathf.Abs(10000 - enemy.Hp - expectedDamage) < .02f,
            "Actual: " + (10000 - enemy.Hp) + "; expected: " + expectedDamage);
        Record(name + ": projectile and hit effect return independently", Bullets().Length == 0 && Hits().Length == 0);
        skills.ClearAllSkills(); yield return new WaitForSeconds(.35f);
    }
    IEnumerator Motion()
    {
        changeResolution?.Invoke(540, 960); yield return null; yield return new WaitForSeconds(.8f);
        ResetTarget(Vector2.zero); var skill = Equip(5); skill.Tick(skill.interval - .02f);
        string folder = Path.Combine(output, "frames"); Directory.CreateDirectory(folder);
        var times = new List<float>(); float started = Time.realtimeSinceStartup, prepared = Time.time, landed = -1; bool fired = false, damaged = false; int visible = 0;
        while (times.Count < 90 && Time.realtimeSinceStartup - started < 8)
        {
            if (!fired && Time.time - prepared >= .02f) { skill.Tick(Time.time - prepared); fired = true; }
            if (!damaged && enemy.Hp < 10000) { damaged = true; landed = Time.time; }
            yield return new WaitForEndOfFrame(); if (Hits().Length > 0) visible++;
            times.Add(Time.realtimeSinceStartup - started); ScreenCapture.CaptureScreenshot(Path.Combine(folder, "frame-" + (times.Count - 1).ToString("D4") + ".png"));
            if (damaged && Time.time - landed > .55f) break; yield return new WaitForSecondsRealtime(.025f);
        }
        Record("Motion records a real damaging maximum-level volley", fired && damaged && (Baseline || visible > 0), "Frames: " + times.Count + "; hit frames: " + visible);
        File.WriteAllText(Path.Combine(output, "frame-times.json"), "[" + string.Join(",", times.Select(t => t.ToString(CultureInfo.InvariantCulture))) + "]");
        File.WriteAllText(Path.Combine(output, "motion.json"), Newtonsoft.Json.JsonConvert.SerializeObject(new { fired, damaged, visible, frames = times.Count, baseline = Baseline }));
        yield return new WaitForSeconds(.4f); changeResolution?.Invoke(1080, 1920); yield return null;
    }
    IEnumerator NewPresentationChecks()
    {
        var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Res/Textures/VFX/sheet_talisman_presentation_v1.png");
        Record("Paper and eight-frame hit share a small 512x256 atlas", atlas.width == 512 && atlas.height == 256 && !atlas.isReadable && atlas.mipmapCount == 1);
        Record("Atlas PNG stays under 96 KiB source budget", new FileInfo(AssetDatabase.GetAssetPath(atlas)).Length <= 96 * 1024);
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Res/Materials/VFX/mat_talisman_trail_v1.mat");
        var templates = new[] { definition.bulletPrefab, definition.maxLevelPrefab, definition.burstBulletPrefab };
        Record("All configured talisman projectile variants opt into hit feedback", templates.All(p => p.GetComponent<ProjectileHitFeedback>() != null));
        Record("All paper sprites reuse the hit atlas", templates.All(p => p.GetComponentInChildren<SpriteRenderer>().sprite.texture == atlas));
        var pistol = AssetDatabase.LoadAssetAtPath<AutoProjectileSkillDefinition>("Assets/ScriptableObject/Skill/ActiveSkill/SkillDef_AutoProjectilePistol.asset");
        Record("Other projectile types do not gain talisman feedback", pistol.bulletPrefab.GetComponent<ProjectileHitFeedback>() == null);
        ResetTarget(Vector2.zero);
        var probe = GameObjectPool.Get(definition.maxLevelPrefab, origin, Quaternion.identity);
        var trails = probe.GetComponentsInChildren<TrailRenderer>();
        Record("Maximum talisman uses two short tapered soft-edge trails", trails.Length == 2 && trails.All(t => t.sharedMaterial == material && t.time <= .12f && t.numCapVertices == 3 && t.numCornerVertices == 2 && t.widthMultiplier <= .1151f && t.widthCurve.Evaluate(1) == 0));
        var random = UnityEngine.Random.state; probe.GetComponent<ProjectileHitFeedback>().Play(enemy.GetComponent<Collider2D>());
        Record("Hit presentation does not consume combat random state", random.Equals(UnityEngine.Random.state));
        GameObjectPool.Release(probe);
        Record("Pool release disables and clears every trail", trails.All(t => !t.emitting && t.positionCount == 0));
        var reused = GameObjectPool.Get(definition.maxLevelPrefab, origin + Vector2.right * 7, Quaternion.identity);
        Record("Teleporting a reused talisman does not bridge the old position", reused == probe && trails.All(t => t.positionCount <= 1 && t.emitting));
        GameObjectPool.Release(reused);
        yield return new WaitForSeconds(.35f);
        var hitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Res/Prefabs/Effects/Impact/fx_talisman_hit_v1.prefab");
        CombatVfxSpawner.TryPlayPooled(hitPrefab, target, Quaternion.identity, .45f); yield return null;
        var hit = Hits().First(); Time.timeScale = 0; yield return null; float time = hit.GetComponent<ParticleSystem>().time;
        yield return new WaitForSecondsRealtime(.15f);
        Record("Pause freezes hit animation and pool expiry", hit.gameObject.activeSelf && Mathf.Abs(hit.GetComponent<ParticleSystem>().time - time) < .001f);
        Time.timeScale = 1; yield return new WaitForSeconds(.4f);
        Record("Resume finishes the short hit effect", Hits().Length == 0);
        yield return null; int accepted = 0;
        for (int i = 0; i < 40; i++) if (CombatVfxSpawner.TryPlayPooled(hitPrefab, target, Quaternion.identity)) accepted++;
        Record("Dense talisman hits retain existing per-frame limit", accepted > 0 && accepted <= 8 && Hits().Length <= 32);
        yield return new WaitForSeconds(.4f);
        Record("Dense hit stamps leave no active effects", Hits().Length == 0);
        ResetTarget(Vector2.zero);
        var ownerObject = new GameObject("OrbitContactFixture");
        var owner = ownerObject.AddComponent<TraitTalismanOrbit>(); owner.enabled = false;
        var orbitTemplate = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Traits/TalismanOrbit.prefab");
        var orbitObject = Instantiate(orbitTemplate, target, Quaternion.identity);
        var orbiter = orbitObject.AddComponent<TalismanOrbiter>(); orbiter.Init(owner, 8, 1.5f);
        orbiter.FlyToward(target, 0); Physics2D.SyncTransforms();
        bool contacted = orbiter.CheckEnemyContact();
        Record("Orbit contact keeps its damage and uses the shared hit stamp", contacted && Mathf.Abs(10000 - enemy.Hp - 10) < .02f && Hits().Length == 1);
        Destroy(ownerObject); yield return null; yield return new WaitForSeconds(.4f);
        Record("Orbit contact leaves no talisman or active hit stamp", orbitObject == null && Hits().Length == 0);
    }
    protected override void OnDestroy()
    {
        if (saved) { Time.timeScale = originalScale; UnityEngine.Random.state = originalRandom; }
        base.OnDestroy();
    }
}
#endif
