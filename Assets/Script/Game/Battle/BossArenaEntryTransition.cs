using System.Collections;
using UnityEngine;

/// <summary>One lightweight portal sequence; no new textures, materials or prefab dependencies.</summary>
[DisallowMultipleComponent]
public sealed class BossArenaEntryTransition : MonoBehaviour
{
    public const float DepartureDuration = .20f;
    public const float TravelDuration = .52f;
    public const float ArrivalDuration = .24f;
    public bool IsPlaying { get; private set; }
    public bool IsHidden { get; private set; }

    private Coroutine _routine;
    private CameraFollow2D _camera;
    private PlayerController _controller;
    private PlayerHealth _health;
    private Rigidbody2D _body;
    private Renderer[] _renderers;
    private bool[] _hiddenBefore;
    private SpriteRenderer[] _sprites;
    private Color[] _colors;
    private GameObject _portalRoot;
    private Transform _departure, _arrival;
    private LineRenderer[] _departureLines, _arrivalLines;
    private Vector2 _destination;
    private bool _healthProtectedBefore;

    public void Play(Vector2 destination, CameraFollow2D cameraFollow)
    {
        if (IsPlaying || !isActiveAndEnabled) return;
        IsPlaying = true;
        _destination = destination;
        _camera = cameraFollow;
        _controller = GetComponent<PlayerController>();
        _health = GetComponent<PlayerHealth>();
        _body = GetComponent<Rigidbody2D>();
        _renderers = GetComponentsInChildren<Renderer>(true);
        _hiddenBefore = new bool[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++) _hiddenBefore[i] = _renderers[i].forceRenderingOff;
        _sprites = GetComponentsInChildren<SpriteRenderer>(true);
        _colors = new Color[_sprites.Length];
        for (int i = 0; i < _sprites.Length; i++) _colors[i] = _sprites[i].color;
        _controller?.SetArenaEntryLocked(true);
        if (_health != null)
        {
            _healthProtectedBefore = _health.ArenaEntryProtected;
            _health.ArenaEntryProtected = true;
        }
        CreatePortals();
        _camera?.BeginArenaTransition(new Vector3(destination.x, destination.y, transform.position.z), DepartureDuration, TravelDuration);
        _routine = StartCoroutine(Sequence());
    }

    private IEnumerator Sequence()
    {
        float elapsed = 0f;
        while (elapsed < DepartureDuration)
        {
            float t = Mathf.Clamp01(elapsed / DepartureDuration);
            FadePlayer(1f - Mathf.SmoothStep(0, 1, t));
            AnimatePortal(_departure, _departureLines, t, true);
            StopMomentum();
            yield return null;
            elapsed += Time.deltaTime;
        }
        SetHidden(true);
        BattlePlayerPositioning.Teleport(transform, _destination);
        // Camera and portal use the same scaled clock; pausing never reveals a
        // hidden player early or leaves the camera zooming behind a pause panel.
        elapsed = 0f;
        while (elapsed < TravelDuration || (_camera != null && _camera.IsArenaTransitioning))
        {
            float t = Mathf.Clamp01(elapsed / TravelDuration);
            AnimatePortal(_departure, _departureLines, 1f, true);
            AnimatePortal(_arrival, _arrivalLines, t * .5f, false);
            StopMomentum();
            yield return null;
            elapsed += Time.deltaTime;
        }
        FadePlayer(0f);
        SetHidden(false);
        elapsed = 0f;
        while (elapsed < ArrivalDuration)
        {
            float t = Mathf.Clamp01(elapsed / ArrivalDuration);
            FadePlayer(Mathf.SmoothStep(0, 1, t));
            AnimatePortal(_arrival, _arrivalLines, .5f + .5f * t, false);
            StopMomentum();
            yield return null;
            elapsed += Time.deltaTime;
        }
        _routine = null;
        Restore();
    }

    private void StopMomentum()
    {
        if (_body == null) return;
        _body.velocity = Vector2.zero;
        _body.angularVelocity = 0f;
    }

    private void FadePlayer(float alpha)
    {
        for (int i = 0; i < _sprites.Length; i++)
            if (_sprites[i] != null)
            {
                Color color = _colors[i]; color.a *= alpha;
                _sprites[i].color = color;
            }
    }

    private void SetHidden(bool hidden)
    {
        IsHidden = hidden;
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null) _renderers[i].forceRenderingOff = hidden || _hiddenBefore[i];
    }

    public void Cancel()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = null;
        if (IsPlaying) Restore();
    }

    private void Restore()
    {
        FadePlayer(1f);
        SetHidden(false);
        _controller?.SetArenaEntryLocked(false);
        if (_health != null) _health.ArenaEntryProtected = _healthProtectedBefore;
        StopMomentum();
        _camera?.CancelArenaTransition();
        if (_portalRoot != null) Destroy(_portalRoot);
        _portalRoot = null;
        IsPlaying = false;
    }

    private void OnDisable() { Cancel(); }

    private void CreatePortals()
    {
        // Reuse the actual player's sprite material, already included in the build.
        Material material = null;
        for (int i = 0; i < _sprites.Length; i++)
            if (_sprites[i] != null && _sprites[i].sharedMaterial != null) { material = _sprites[i].sharedMaterial; break; }
        if (material == null) return;
        _portalRoot = new GameObject("BossEntryPortals");
        _portalRoot.transform.SetParent(transform.parent, false);
        _departure = CreatePortal("Departure", transform.position, material, out _departureLines);
        _arrival = CreatePortal("Arrival", new Vector3(_destination.x, _destination.y, transform.position.z), material, out _arrivalLines);
        _arrival.gameObject.SetActive(false);
    }

    private Transform CreatePortal(string name, Vector3 position, Material material, out LineRenderer[] lines)
    {
        var root = new GameObject(name).transform;
        root.SetParent(_portalRoot.transform, false);
        root.position = position + Vector3.down * .55f;
        lines = new LineRenderer[3];
        for (int i = 0; i < lines.Length; i++)
        {
            var go = new GameObject(i == 2 ? "LightColumn" : "PortalRing");
            go.transform.SetParent(root, false);
            var line = go.AddComponent<LineRenderer>();
            lines[i] = line;
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.sortingOrder = 30 + i;
            line.startWidth = line.endWidth = i == 1 ? .035f : .07f;
            line.numCapVertices = 3;
            line.loop = i != 2;
            line.positionCount = i == 2 ? 2 : 48;
            if (i == 2)
            {
                line.SetPosition(0, Vector3.zero);
                line.SetPosition(1, Vector3.up * 2.1f);
            }
            else for (int n = 0; n < 48; n++)
            {
                float angle = n * Mathf.PI * 2f / 48;
                float radius = i == 0 ? 1f : 1.3f;
                line.SetPosition(n, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * .4f, 0));
            }
        }
        return root;
    }

    private static void AnimatePortal(Transform root, LineRenderer[] lines, float progress, bool departure)
    {
        if (root == null) return;
        root.gameObject.SetActive(progress < 1f);
        float alpha = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
        float scale = departure ? Mathf.Lerp(1.1f, .45f, progress) : Mathf.Lerp(.65f, 1.55f, progress);
        root.localScale = new Vector3(scale, scale, 1);
        for (int i = 0; i < lines.Length; i++)
        {
            Color color = i == 1 ? new Color(1f, .97f, .82f, alpha) : new Color(.2f, .95f, 1f, alpha * .8f);
            lines[i].startColor = color;
            if (i == 2) color.a = 0f;
            lines[i].endColor = color;
        }
    }
}
