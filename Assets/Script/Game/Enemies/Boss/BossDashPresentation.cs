using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Tracks the existing dash aim, then draws its locked body sweep. Owns no movement or damage.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class BossDashPresentation : MonoBehaviour, IPoolReceiver
{
    [SerializeField] Material solidMaterial;
    [SerializeField] int sortingOrder = 4;
    [SerializeField] float skillDamageRadius = 1.2f;
    [SerializeField] float borderWidth = .055f;
    [SerializeField] float chargeFillAlpha = .10f;
    readonly List<Vector3> vertices = new List<Vector3>(768);
    readonly List<Color32> colors = new List<Color32>(768);
    readonly List<Vector2> uv = new List<Vector2>(768);
    readonly List<int> triangles = new List<int>(1536);
    readonly RaycastHit2D[] contacts = new RaycastHit2D[4];
    Mesh mesh;
    MeshRenderer meshRenderer;
    Rigidbody2D body;
    EnemyBase enemy;
    BossBrain brain;
    EnemyWordLabel wordLabel;
    Transform target;
    TrailRenderer legacyTrail;
    SpriteRenderer[] chargeSprites;
    Color[] chargeColors;
    bool chargeColorsCaptured;
    int phase, chargeToken;
    float phaseAt, chargeSeconds, maxDistance, previewDistance, recoverySeconds;
    float lastTime = float.NegativeInfinity;
    Vector2 direction, lockedDirection, startPosition, endPosition;
    LayerMask wallMask;
    Matrix4x4 worldToLocal, lastMatrix;
    bool dirty = true;

    public int Phase => phase; // 0 idle, 1 tracking charge, 2 locked dash, 3 recovery.
    public int ChargeToken => chargeToken;
    public Vector2 Direction => direction;
    public Vector2 AimDirection => direction;
    public Vector2 LockedDirection => lockedDirection;
    public float PreviewDistance => previewDistance;
    public float VisibleDistance => previewDistance;
    public float PreviewRadius => Mathf.Max(skillDamageRadius, BodyRadius);
    public float DamageRadius => skillDamageRadius;
    public float BodyRadius
    {
        get
        {
            if (body == null) return 0;
            var circle = body.GetComponent<CircleCollider2D>();
            if (circle == null) return 0;
            Vector3 scale = circle.transform.lossyScale;
            return circle.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        }
    }
    public Vector2 StartPosition => startPosition;
    public Vector2 EndPosition => endPosition;
    public Vector2 SpeedLineOrigin { get; private set; }
    public int RendererCount => meshRenderer != null ? 1 : 0;
    public bool HasVisibleGeometry => meshRenderer != null && meshRenderer.enabled && vertices.Count > 0;
    public MeshRenderer Renderer => meshRenderer;
    public int MeshBuildCount { get; private set; }

    void Awake() { EnsureResources(); }
    void EnsureResources()
    {
        if (mesh != null) return;
        body = GetComponentInParent<Rigidbody2D>();
        enemy = GetComponentInParent<EnemyBase>();
        brain = GetComponentInParent<BossBrain>();
        wordLabel = enemy != null ? enemy.GetComponentInChildren<EnemyWordLabel>(true) : null;
        legacyTrail = body != null ? body.GetComponent<TrailRenderer>() : null;
        chargeSprites = brain != null ? brain.GetComponentsInChildren<SpriteRenderer>(true) : new SpriteRenderer[0];
        chargeColors = new Color[chargeSprites.Length];
        mesh = new Mesh { name = "boss_dash_preview", hideFlags = HideFlags.DontSave };
        mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = solidMaterial;
        meshRenderer.sortingOrder = sortingOrder;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        meshRenderer.enabled = false;
    }
    public void BeginCharge(Transform aimTarget, float seconds, float distance, LayerMask mask)
    {
        EnsureResources();
        chargeToken++;
        target = aimTarget;
        chargeSeconds = Mathf.Max(.01f, seconds);
        maxDistance = Mathf.Max(0, distance);
        wallMask = mask;
        lockedDirection = Vector2.zero;
        phase = 1;
        phaseAt = Time.time;
        for (int i = 0; i < chargeSprites.Length; i++) if (chargeSprites[i] != null) chargeColors[i] = chargeSprites[i].color;
        chargeColorsCaptured = true;
        dirty = true;
        RenderNow();
    }
    public void Commit(Vector2 origin, Vector2 aim, float distance, LayerMask mask)
    {
        EnsureResources();
        RestoreChargeColors();
        startPosition = origin;
        lockedDirection = direction = aim;
        wallMask = mask;
        maxDistance = Mathf.Max(0, distance);
        previewDistance = PredictDistance(origin, aim);
        endPosition = origin + aim * previewDistance;
        target = null;
        phase = 2;
        phaseAt = Time.time;
        dirty = true;
        RenderNow();
    }
    public void FinishDash(Vector2 actualEnd, float seconds = .2f)
    {
        EnsureResources();
        endPosition = actualEnd;
        previewDistance = Vector2.Distance(startPosition, actualEnd);
        recoverySeconds = Mathf.Max(.01f, seconds);
        phase = 3;
        phaseAt = Time.time;
        dirty = true;
        RenderNow();
    }
    // The same four-contact query and exact wall filter are used by actual movement.
    float PredictDistance(Vector2 origin, Vector2 aim)
    {
        if (body == null || aim.sqrMagnitude < .000001f) return 0;
        float wallDistance;
        return DashModule.TryFindWallDistance(body, aim, maxDistance, wallMask, contacts, out wallDistance)
            ? wallDistance : maxDistance;
    }
    void RestoreChargeColors()
    {
        if (!chargeColorsCaptured) return;
        for (int i = 0; i < chargeSprites.Length; i++)
            if (chargeSprites[i] != null) chargeSprites[i].color = chargeColors[i];
        chargeColorsCaptured = false;
    }
    public void Cancel()
    {
        ClearState(true);
    }
    public void CompleteRecovery()
    {
        ClearState(false);
    }
    void StopPendingMovement()
    {
        if (body == null) return;
        body.velocity = Vector2.zero;
        // Override a queued dash MovePosition only when cancelling, never on the normal path.
        if (body.gameObject.activeInHierarchy) body.MovePosition(body.position);
    }
    void ClearState(bool stopMovement)
    {
        bool active = phase != 0;
        if (active && stopMovement) StopPendingMovement();
        chargeToken++;
        RestoreChargeColors();
        phase = 0;
        target = null;
        direction = lockedDirection = Vector2.zero;
        previewDistance = 0;
        ClearGeometry();
        if (legacyTrail != null) { legacyTrail.emitting = false; legacyTrail.Clear(); }
        if (active && brain != null) brain.IsBusy = false;
    }
    public void OnPoolGet()
    {
        EnsureResources();
        // Pool.Get has just positioned the Transform. Make that spawn authoritative before
        // overriding a pending physics target; rb.position may still describe the old lease.
        Vector2 spawn = body != null ? (Vector2)body.transform.position : Vector2.zero;
        ClearState(false);
        if (body == null) return;
        body.position = spawn;
        body.velocity = Vector2.zero;
        if (body.gameObject.activeInHierarchy) body.MovePosition(spawn);
    }
    public void OnPoolRelease() { Cancel(); }
    void OnDisable() { Cancel(); }
    void OnDestroy() { if (mesh != null) Destroy(mesh); }
    void ClearGeometry()
    {
        vertices.Clear(); colors.Clear(); uv.Clear(); triangles.Clear();
        if (mesh != null) mesh.Clear();
        if (meshRenderer != null) meshRenderer.enabled = false;
        dirty = true;
    }
    void LateUpdate()
    {
        if (phase == 0) return;
        if (enemy != null && enemy.Hp <= 0f) { Cancel(); return; }
        RenderNow();
    }
    public void RenderNow()
    {
        EnsureResources();
        if (phase == 0 || (Time.timeScale == 0f && !dirty)) return;
        float now = Time.time;
        Matrix4x4 nextMatrix = transform.worldToLocalMatrix;
        if (phase == 1)
        {
            Vector2 origin = body != null ? body.position : (Vector2)transform.parent.position;
            Vector2 aim = target != null ? ((Vector2)target.position - origin).normalized : Vector2.right;
            float distance = PredictDistance(origin, aim);
            dirty |= origin != startPosition || aim != direction || distance != previewDistance;
            startPosition = origin; direction = aim; previewDistance = distance;
            endPosition = origin + aim * distance;
        }
        if (!dirty && now == lastTime && nextMatrix == lastMatrix) return;
        if (phase == 3 && now - phaseAt >= recoverySeconds)
        {
            // The module remains authoritative for the end of the .2 s busy recovery.
            ClearGeometry(); dirty = false; return;
        }
        dirty = false; lastTime = now; lastMatrix = nextMatrix; worldToLocal = nextMatrix;
        vertices.Clear(); colors.Clear(); uv.Clear(); triangles.Clear();
        if (phase == 1) DrawCharge(Mathf.Clamp01((now - phaseAt) / chargeSeconds));
        else if (phase == 2) DrawDash();
        else DrawRecovery(Mathf.Clamp01((now - phaseAt) / recoverySeconds));
        mesh.Clear(false); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); MeshBuildCount++;
        meshRenderer.enabled = vertices.Count > 0 && solidMaterial != null;
    }
    void DrawCharge(float progress)
    {
        float radius = PreviewRadius;
        Capsule(startPosition, endPosition, radius, new Color(.82f, .09f, .035f, chargeFillAlpha * Mathf.Lerp(.7f, 1f, progress)),
            new Color(.46f, .018f, .018f, Mathf.Lerp(.58f, .92f, progress)), borderWidth);
        DrawArrows(new Color(1f, .48f, .16f, Mathf.Lerp(.35f, .86f, progress)));
        Arc(startPosition, radius * .76f, .035f, -90, -90 + 360 * progress, new Color(1f, .45f, .12f, .65f), 32);
    }
    void DrawDash()
    {
        Capsule(startPosition, endPosition, PreviewRadius, new Color(.75f, .055f, .025f, .025f),
            new Color(.65f, .055f, .02f, .34f), borderWidth * .7f);
        DrawArrows(new Color(1f, .62f, .22f, .34f));
        Vector2 current = body != null ? body.position : startPosition;
        Vector2 rear = current - direction * .65f;
        Transform anchor;
        Vector3 localPoint;
        if (wordLabel != null && wordLabel.TryGetHitAnchor(current - direction * 100f, out anchor, out localPoint))
            rear = (Vector2)anchor.TransformPoint(localPoint) - direction * .25f;
        SpeedLineOrigin = rear;
        Vector2 side = new Vector2(-direction.y, direction.x);
        for (int i = -1; i <= 1; i++)
        {
            Vector2 head = rear + side * i * .48f;
            Segment(head - direction * (i == 0 ? 1.25f : .85f), head, i == 0 ? .055f : .035f,
                new Color(1f, i == 0 ? .63f : .25f, .08f, i == 0 ? .65f : .52f));
        }
    }
    void DrawRecovery(float progress)
    {
        float fade = 1 - progress;
        Arc(endPosition, Mathf.Lerp(PreviewRadius * .6f, PreviewRadius, progress), .055f * fade, 0, 360,
            new Color(.86f, .12f, .035f, fade * .75f), 40);
        Arc(endPosition, Mathf.Lerp(PreviewRadius * .42f, PreviewRadius * .8f, progress), .025f * fade, 0, 360,
            new Color(1f, .63f, .24f, fade * .7f), 40);
    }
    void DrawArrows(Color color)
    {
        if (direction.sqrMagnitude < .000001f) return;
        Vector2 side = new Vector2(-direction.y, direction.x);
        for (float distance = .85f; distance < previewDistance - .25f; distance += 1.3f)
        {
            Vector2 tip = startPosition + direction * distance;
            Segment(tip - direction * .23f - side * .23f, tip, .065f, color);
            Segment(tip - direction * .23f + side * .23f, tip, .065f, color);
        }
    }
    void Capsule(Vector2 start, Vector2 end, float radius, Color fill, Color border, float width)
    {
        Vector2 axis = end - start;
        float angle = axis.sqrMagnitude > .000001f ? Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg : 0;
        Vector2 side = new Vector2(-Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad)) * radius;
        if (axis.sqrMagnitude > .000001f)
        {
            Quad(start - side, end - side, end + side, start + side, fill);
            Segment(start - side, end - side, width, border); Segment(start + side, end + side, width, border);
        }
        FilledArc(start, radius, angle + 90, angle + 270, fill, 20);
        FilledArc(end, radius, angle - 90, angle + 90, fill, 20);
        Arc(start, radius, width, angle + 90, angle + 270, border, 20);
        Arc(end, radius, width, angle - 90, angle + 90, border, 20);
    }
    void AddVertex(Vector2 world, Color color)
    { vertices.Add(worldToLocal.MultiplyPoint3x4(new Vector3(world.x, world.y, -.16f))); colors.Add(color); uv.Add(Vector2.zero); }
    void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int index = vertices.Count;
        AddVertex(a, color); AddVertex(b, color); AddVertex(c, color); AddVertex(d, color);
        triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
        triangles.Add(index); triangles.Add(index + 2); triangles.Add(index + 3);
    }
    void Segment(Vector2 a, Vector2 b, float width, Color color)
    {
        Vector2 along = (b - a).normalized, side = new Vector2(-along.y, along.x) * width * .5f;
        Quad(a - side, b - side, b + side, a + side, color);
    }
    void Arc(Vector2 center, float radius, float width, float from, float to, Color color, int segments)
    {
        for (int i = 0; i < segments; i++)
        {
            float a = Mathf.Lerp(from, to, (float)i / segments) * Mathf.Deg2Rad;
            float b = Mathf.Lerp(from, to, (float)(i + 1) / segments) * Mathf.Deg2Rad;
            Segment(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, width, color);
        }
    }
    void FilledArc(Vector2 center, float radius, float from, float to, Color color, int segments)
    {
        for (int i = 0; i < segments; i++)
        {
            int index = vertices.Count;
            float a = Mathf.Lerp(from, to, (float)i / segments) * Mathf.Deg2Rad;
            float b = Mathf.Lerp(from, to, (float)(i + 1) / segments) * Mathf.Deg2Rad;
            AddVertex(center, color); AddVertex(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, color);
            AddVertex(center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, color);
            triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 2);
        }
    }
}
