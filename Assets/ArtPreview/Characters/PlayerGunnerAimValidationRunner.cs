#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>真实 Game 场景、真实角色接入；首轮只验证整身瞄准姿态，不冒充步态帧动画。</summary>
public sealed class PlayerGunnerAimValidationRunner : UIValidationRunnerBase
{
    protected override string OutputFolder => "PlayerGunnerAimV1/After";
    protected override string ValidationNote => "Real Game scene and player prefab, actual controller/skill fire, controlled training target, in-memory profile. Existing breathing is temporarily suspended for transform invariants and restored for footage. Five static aim keys mirrored to eight directions; no walk cycle or device-performance claim.";
    readonly Vector3 center = new Vector3(15, 15, 10);
    Transform player;
    PlayerController controller;
    CharacterAppearance appearance;
    SpriteRenderer body;
    GameObject target;
    SkillAutoProjectile skill;
    CharacterDefinition gunner;
    float originalScale;
    bool background;
    int shotCount;
    PlayerWeaponFiredVisualEvent lastShot;

    void Awake() { originalScale = Time.timeScale; background = Application.runInBackground; }
    void OnShot(PlayerWeaponFiredVisualEvent e) { if (e.owner == player) { shotCount++; lastShot = e; } }
    static Vector2 Dir(float angle) { return new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)); }
    void Aim(float angle) { target.transform.position = player.position + (Vector3)(Dir(angle) * 3.2f); }
    IEnumerator Frames() { yield return null; yield return null; yield return new WaitForEndOfFrame(); }
    void ClearBullets() { foreach (var bullet in FindObjectsOfType<PlayerBullet>()) GameObjectPool.Release(bullet.gameObject); }

    protected override IEnumerator RunChecks()
    {
        Time.timeScale = 1;
        Application.runInBackground = true;
        TutorialDirector.Ensure().SkipAll(); // Test fixture uses in-memory storage only.
        gunner = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/ScriptableObject/Character/Role/Character_PistolGuy.asset");
        SelectedCharacterContext.Set(gunner.characterId);
        yield return SceneManager.UnloadSceneAsync("Home");
        SelectedLevelContext.Set(1, 105);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/Game.unity", LoadSceneMode.Additive);
        SceneManager.SetActiveScene(SceneManager.GetSceneByName("Game"));
        yield return new WaitForSecondsRealtime(.4f);
        var spawner = FindObjectOfType<SpawnerWaves>();
        spawner.StopAllCoroutines(); spawner.autoTriggerWavesOnStart = false; spawner.enableNormalSpawn = false;
        foreach (var outcome in FindObjectsOfType<BattleOutcomeCoordinator>()) { outcome.StopAllCoroutines(); outcome.enabled = false; }
        foreach (var cameraFollow in FindObjectsOfType<CameraFollow2D>()) cameraFollow.enabled = false;
        UIManager.Instance.CloseAllStack();
        foreach (var enemy in FindObjectsOfType<EnemyBase>()) { SpawnLimiter.Instance?.Unregister("Enemy", enemy.gameObject); GameObjectPool.Release(enemy.gameObject); }
        yield return null;
        CombatTargetRegistry.ClearAll();
        var skills = FindObjectOfType<PlayerSkills>();
        player = skills.transform;
        skill = skills.GetEquippedSkill<SkillAutoProjectile>(SkillId.AutoProjectilePistol);
        skills.enabled = false;
        controller = player.GetComponent<PlayerController>();
        appearance = player.GetComponent<CharacterAppearance>();
        body = player.Find("Body").GetComponent<SpriteRenderer>();
        player.position = center; controller.ResetMovementAfterTeleport();
        var health = player.GetComponent<PlayerHealth>(); health.SetInvulnerable(true);
        Record("Selected real player receives only the gunner's complete aim frames", appearance.HasDirectionalAimFrames && gunner.aimFrames.IsComplete && skill != null);
        Record("Real player retains a single Body SpriteRenderer and no separate gun", body.GetComponentsInChildren<SpriteRenderer>().Length == 1 && player.Find("Weapon") == null);
        int originalRendererCount = player.GetComponentsInChildren<SpriteRenderer>().Length; // Includes the two existing health-bar sprites.
        var breathing = body.GetComponent<IdleBreathAnim>();
        bool breathingWasEnabled = breathing != null && breathing.enabled;
        if (breathingWasEnabled) breathing.enabled = false;
        var collider = player.GetComponent<Collider2D>();
        string colliderJson = EditorJsonUtility.ToJson(collider);
        var material = body.sharedMaterial;
        Vector3 bodyPosition = body.transform.localPosition, bodyScale = body.transform.localScale;
        target = new GameObject("AimTrainingTarget"); target.tag = "monster";
        var marker = target.AddComponent<SpriteRenderer>();
        marker.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f), 2);
        marker.color = new Color(1f, .45f, .12f); marker.sortingOrder = 20; target.transform.localScale = Vector3.one * .28f;
        CombatTargetRegistry.Register(target);
        Camera.main.orthographicSize = 12;
        Camera.main.transform.position = new Vector3(15, 15, -10);
        EventBus.Subscribe<PlayerWeaponFiredVisualEvent>(OnShot, owner: this);
        for (int sector = 0; sector < 8; sector++)
        {
            Aim(sector * 45); yield return Frames();
            bool mirror; var expected = gunner.aimFrames.GetFrame(sector, out mirror);
            Record("Controller aims toward sector " + sector, appearance.AimSector == sector && body.sprite == expected && body.flipX == mirror);
            ClearBullets(); int before = shotCount;
            skill.Tick(skill.interval + .1f);
            var bullets = FindObjectsOfType<PlayerBullet>();
            Record("Actual primary shot synchronizes sector " + sector, shotCount == before + 1 && lastShot.projectileCount > 0 && Vector2.Dot(lastShot.aimDirection, Dir(sector * 45)) > .999f && appearance.AimSector == sector);
            Record("Shot still originates at player center in sector " + sector, bullets.Length > 0 && bullets.All(b => Vector2.Distance(b.transform.position, player.position) < .001f));
            yield return Capture("direction-" + sector);
        }
        ClearBullets();
        Aim(0); yield return Frames(); Aim(24); yield return Frames();
        Record("Direction boundary jitter stays in the old sector", appearance.AimSector == 0);
        Aim(31); yield return Frames(); Record("Crossing hysteresis changes direction", appearance.AimSector == 1);
        Aim(26); yield return Frames(); Record("Small reverse jitter retains the new sector", appearance.AimSector == 1);
        Aim(16); yield return Frames(); Record("Moving fully back crosses the reverse boundary", appearance.AimSector == 0);
        Aim(24); yield return Frames(); skill.Tick(skill.interval + .1f);
        Record("A successful real shot bypasses visual hysteresis immediately", appearance.AimSector == 1);
        int current = appearance.AimSector;
        EventBus.Publish(new PlayerWeaponFiredVisualEvent { owner = target.transform, skillId = SkillId.AutoProjectilePistol, aimDirection = Vector2.left, projectileCount = 1 });
        Record("Another owner's shot does not change this character", appearance.AimSector == current);
        EventBus.Publish(new PlayerWeaponFiredVisualEvent { owner = player, skillId = SkillId.AutoProjectileSword, aimDirection = Vector2.left, projectileCount = 1 });
        Record("A learned secondary weapon does not change the primary pose", appearance.AimSector == current);
        EventBus.Publish(new PlayerWeaponFiredVisualEvent { owner = player, skillId = SkillId.AutoProjectilePistol, aimDirection = Vector2.left, projectileCount = 0 });
        Record("A zero-projectile notification cannot change the pose", appearance.AimSector == current);
        Time.timeScale = 0; Aim(180); yield return Frames();
        Record("Pause freezes aim", appearance.AimSector == current);
        Time.timeScale = 1; yield return Frames(); Record("Resume restores targeting", appearance.AimSector == 4);
        CombatTargetRegistry.Unregister(target); target.SetActive(false); yield return Frames();
        player.position += Vector3.up * .1f; yield return Frames();
        Record("No target uses actual vertical displacement", appearance.AimSector == 2);
        yield return new WaitForSecondsRealtime(.1f); Record("Standing still retains the last direction", appearance.AimSector == 2);
        player.position = center; controller.ResetMovementAfterTeleport(); yield return Frames();
        Record("Teleport reset does not create a false movement direction", appearance.AimSector == 2);
        target.SetActive(true); CombatTargetRegistry.Register(target); Aim(180);
        var catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>("Assets/ScriptableObject/Character/CharacterCatalog.asset");
        foreach (var role in catalog.characters.Where(c => c != gunner))
        {
            appearance.ApplySkin(role); appearance.ApplyWeapon(role.defaultWeapon); yield return Frames();
            Record("Legacy role retains its single-sprite appearance: " + role.characterId, !appearance.HasDirectionalAimFrames && body.sprite == role.bodySprite && body.flipX);
        }
        appearance.ApplySkin(gunner); appearance.ApplyWeapon(gunner.defaultWeapon); Aim(0); yield return Frames();
        health.SetInvulnerable(false); health.SetDefense(0); health.TakeDamage(1, target.transform); yield return null;
        Record("Existing real damage feedback still tints Body", body.color.r > body.color.g);
        yield return new WaitForSeconds(.3f);
        Record("Damage feedback restores body color, position and scale", body.color == Color.white && body.transform.localPosition == bodyPosition && body.transform.localScale == bodyScale);
        health.SetInvulnerable(true);
        Record("Aim changes never modify player collider data", EditorJsonUtility.ToJson(collider) == colliderJson);
        Record("All aim poses reuse one Body renderer and preserve the health-bar renderers and shared material", player.GetComponentsInChildren<SpriteRenderer>().Length == originalRendererCount && body.GetComponentsInChildren<SpriteRenderer>().Length == 1 && body.sharedMaterial == material && body.transform.localPosition == bodyPosition && body.transform.localScale == bodyScale);
        if (breathingWasEnabled) breathing.enabled = true;
        yield return Motion();
        ClearBullets();
    }

    IEnumerator Motion()
    {
        changeResolution?.Invoke(540, 960);
        Camera.main.orthographicSize = 6;
        yield return new WaitForSecondsRealtime(.2f);
        string folder = Path.Combine(output, "aim-motion"); Directory.CreateDirectory(Path.Combine(folder, "frames"));
        var times = new List<float>(); float start = Time.realtimeSinceStartup, tick = Time.time;
        while (Time.realtimeSinceStartup - start < 9.6f)
        {
            int sector = Mathf.Min(7, (int)((Time.realtimeSinceStartup - start) / 1.2f)); Aim(sector * 45);
            float now = Time.time; skill.Tick(now - tick); tick = now;
            yield return new WaitForEndOfFrame();
            times.Add(Time.realtimeSinceStartup - start);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "frames", "frame-" + (times.Count - 1).ToString("D4") + ".png"));
            yield return new WaitForSecondsRealtime(.065f);
        }
        File.WriteAllText(Path.Combine(folder, "frame-times.json"), "[" + string.Join(",", times.Select(t => t.ToString(CultureInfo.InvariantCulture))) + "]");
        File.WriteAllText(Path.Combine(folder, "motion.json"), Newtonsoft.Json.JsonConvert.SerializeObject(new { frames = times.Count, timeScale = 1, retimed = false, note = "Real Game scene, enlarged camera for inspection; standing aim keys, no locomotion animation." }));
        Record("Real-time direction footage captures all eight poses", times.Count > 30 && appearance.AimSector == 7);
        changeResolution?.Invoke(1080, 1920);
    }

    protected override void OnDestroy()
    {
        EventBus.Unsubscribe<PlayerWeaponFiredVisualEvent>(OnShot);
        if (target != null) CombatTargetRegistry.Unregister(target);
        Time.timeScale = originalScale; Application.runInBackground = background;
        base.OnDestroy();
    }
}
#endif
