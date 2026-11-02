using UnityEngine;

/// <summary>Repeated prop tiles or legacy directional walls. Collision stays on BossArenaLock's four roots.</summary>
[DisallowMultipleComponent]
public sealed class BossArenaWallVisual : MonoBehaviour
{
    public Sprite tileSprite;
    public float tilePitch = .5f;
    [Min(.01f)] public float tileScale = 1f;
    public Material tileMaterial;
    public Sprite horizontalSprite;
    public Sprite verticalSprite;
    public Sprite postSprite;
    public int sortingOrder = 10;
    public float postSpacing = 5f;

    Mesh tileMesh;
    public int TileCount { get; private set; }

    public void Configure(Vector2 collisionSize)
    {
        if (transform.Find("WallArt") != null) return;
        bool horizontal = collisionSize.x >= collisionSize.y;
        if (tileSprite != null)
        {
            BuildTiles(collisionSize, horizontal);
            return;
        }
        Sprite sprite = horizontal ? horizontalSprite : verticalSprite;
        if (sprite == null) return;

        var body = NewSprite("WallArt", sprite, sortingOrder);
        body.drawMode = SpriteDrawMode.Tiled;
        body.tileMode = SpriteTileMode.Continuous;
        // Use the art's natural narrow dimension; never stretch the painted blocks into a line.
        body.size = horizontal
            ? new Vector2(collisionSize.x, sprite.bounds.size.y)
            : new Vector2(sprite.bounds.size.x, collisionSize.y);
        if (horizontal) body.transform.localPosition = new Vector3(0, .08f, 0);

        if (postSprite == null) return;
        float length = horizontal ? collisionSize.x : collisionSize.y;
        int spans = Mathf.Clamp(Mathf.RoundToInt(length / Mathf.Max(2, postSpacing)), 1, 32);
        // Horizontal walls own all four corners; vertical walls only add intermediate posts.
        int first = horizontal ? 0 : 1, last = horizontal ? spans : spans - 1;
        for (int i = first; i <= last; i++)
        {
            bool corner = horizontal && (i == 0 || i == spans);
            float along = Mathf.Lerp(-length * .5f, length * .5f, (float)i / spans);
            var post = NewSprite(corner ? "CornerPost" : "WallPost", postSprite, sortingOrder + 1);
            post.transform.localPosition = horizontal ? new Vector3(along, 0, -.01f) : new Vector3(0, along, -.01f);
            post.transform.localScale = Vector3.one * (corner ? .86f : .72f);
        }
    }

    void BuildTiles(Vector2 collisionSize, bool horizontal)
    {
        float length = horizontal ? collisionSize.x : collisionSize.y;
        int spans = Mathf.Max(1, Mathf.RoundToInt(length / Mathf.Max(.01f, tilePitch)));
        // Horizontal walls own the four complete corner tiles. Side walls skip both endpoints.
        int first = horizontal ? 0 : 1;
        int last = horizontal ? spans : spans - 1;
        TileCount = Mathf.Max(0, last - first + 1);
        var vertices = new Vector3[TileCount * 4];
        var uv = new Vector2[vertices.Length];
        var colors = new Color32[vertices.Length];
        var triangles = new int[TileCount * 6];
        Vector2 halfSize = (Vector2)tileSprite.bounds.size * (.5f * Mathf.Max(.01f, tileScale));
        Rect textureRect = tileSprite.textureRect;
        float u0 = textureRect.xMin / tileSprite.texture.width;
        float u1 = textureRect.xMax / tileSprite.texture.width;
        float v0 = textureRect.yMin / tileSprite.texture.height;
        float v1 = textureRect.yMax / tileSprite.texture.height;
        var white = new Color32(255, 255, 255, 255);
        for (int i = first; i <= last; i++)
        {
            int tile = i - first, vertex = tile * 4, triangle = tile * 6;
            float along = Mathf.Lerp(-length * .5f, length * .5f, (float)i / spans);
            Vector3 center = horizontal ? new Vector3(along, 0, 0) : new Vector3(0, along, 0);
            vertices[vertex] = center + new Vector3(-halfSize.x, -halfSize.y, 0);
            vertices[vertex + 1] = center + new Vector3(halfSize.x, -halfSize.y, 0);
            vertices[vertex + 2] = center + new Vector3(halfSize.x, halfSize.y, 0);
            vertices[vertex + 3] = center + new Vector3(-halfSize.x, halfSize.y, 0);
            uv[vertex] = new Vector2(u0, v0);
            uv[vertex + 1] = new Vector2(u1, v0);
            uv[vertex + 2] = new Vector2(u1, v1);
            uv[vertex + 3] = new Vector2(u0, v1);
            for (int c = 0; c < 4; c++) colors[vertex + c] = white;
            triangles[triangle] = vertex;
            triangles[triangle + 1] = vertex + 1;
            triangles[triangle + 2] = vertex + 2;
            triangles[triangle + 3] = vertex;
            triangles[triangle + 4] = vertex + 2;
            triangles[triangle + 5] = vertex + 3;
        }

        var child = new GameObject("WallArt");
        child.transform.SetParent(transform, false);
        tileMesh = new Mesh { name = "BossArenaWallTiles", hideFlags = HideFlags.DontSave };
        if (vertices.Length > 65535) tileMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        tileMesh.vertices = vertices;
        tileMesh.uv = uv;
        tileMesh.colors32 = colors;
        tileMesh.triangles = triangles;
        tileMesh.RecalculateBounds();
        child.AddComponent<MeshFilter>().sharedMesh = tileMesh;
        var renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = tileMaterial != null
            ? tileMaterial
            : Resources.GetBuiltinResource<Material>("Sprites-Default.mat");
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        var properties = new MaterialPropertyBlock();
        properties.SetTexture("_MainTex", tileSprite.texture);
        renderer.SetPropertyBlock(properties);
    }

    void OnDestroy()
    {
        if (tileMesh == null) return;
        if (Application.isPlaying) Destroy(tileMesh);
        else DestroyImmediate(tileMesh);
    }

    SpriteRenderer NewSprite(string objectName, Sprite sprite, int order)
    {
        var child = new GameObject(objectName);
        child.transform.SetParent(transform, false);
        var renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return renderer;
    }
}
