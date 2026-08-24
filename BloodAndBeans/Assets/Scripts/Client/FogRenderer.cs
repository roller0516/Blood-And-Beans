using UnityEngine;

/// Draws the local team's fog as one texture on a ground-level quad.
/// Separate from FogOfWar on purpose: the state is gameplay and is synced,
/// the look is not, so a nicer shader can replace this without touching logic.
[RequireComponent(typeof(MeshRenderer))]
public class FogRenderer : MonoBehaviour
{
    [SerializeField] Color fogColor = new(0.05f, 0.06f, 0.10f, 0.96f);
    [SerializeField] int texelsPerCell = 3;     // sub-cell resolution, for a round edge
    [SerializeField] float edgeSoftness = 0.8f; // in cells
    [SerializeField] float blurCells = 2.5f;    // smoothing radius, in cells

    FogOfWar fog;
    MatchDirector director;
    Texture2D tex;
    MeshRenderer meshRenderer;
    bool dirty = true;
    int boundTeam = -1;
    System.Action onChanged;

    void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        director = MatchDirector.Find();
    }

    void Update()
    {
        // The client runs this before its player exists, so the first answer is always
        // team 0 — binding once left player 2 staring at team 1's... team 0's fog.
        // Re-check until the real team arrives, and rebind if it ever changes.
        var team = PlayerTeam.Local();
        if (fog == null || team != boundTeam)
        {
            var next = director != null ? director.FogOf(team) : null;
            if (next == null) return;
            if (!ReferenceEquals(next, fog))
            {
                if (fog != null && onChanged != null) fog.Changed -= onChanged;
                fog = next;
                if (tex == null) Build(); else Rebind();
            }
            boundTeam = team;
            dirty = true;
        }

        if (!dirty) return;
        dirty = false;
        Paint();
    }

    void Rebind()
    {
        onChanged = () => dirty = true;
        fog.Changed += onChanged;
        dirty = true;
    }

    int TexSide => fog.Side * texelsPerCell;

    void Build()
    {
        var side = TexSide;
        tex = new Texture2D(side, side, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        // URP needs the whole transparent setup, not just _Surface: without the blend
        // modes and the keyword the alpha channel is ignored, cleared cells render as
        // solid black, and the fog reads inverted.
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetFloat("_Surface", 1f);                       // transparent
        mat.SetFloat("_Blend", 0f);                         // alpha blend
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.mainTexture = tex;
        // Unity's built-in Plane maps UV 180 degrees around from world XZ, so the fog
        // lifted on the opposite side of the map from the player until this flip.
        mat.mainTextureScale = new Vector2(-1f, -1f);
        mat.mainTextureOffset = new Vector2(1f, 1f);
        meshRenderer.sharedMaterial = mat;

        // world size comes from the gameplay grid, not the texture resolution —
        // `side` here is texels, and using it stretched the plane 6x
        var span = fog.Side * fog.CellSize;
        transform.localScale = new Vector3(span / 10f, 1f, span / 10f); // plane is 10 units
        transform.position = new Vector3(0f, 0.2f, 0f);

        onChanged = () => dirty = true;
        fog.Changed += onChanged;
        dirty = true;
    }

    /// The gameplay grid is square cells, so painting it straight gives a staircase
    /// edge. Each cleared cell is stamped as a soft disc instead and the stamps are
    /// max-combined, which leaves the outer boundary of the cleared region round.
    void Paint()
    {
        var side = TexSide;
        var opaque = (Color32)fogColor;
        // keep the fog's own RGB so a partly-cleared edge fades instead of fringing black
        var rgb = new Color(fogColor.r, fogColor.g, fogColor.b);

        var cover = new float[side * side];   // 0 = fogged, 1 = fully cleared

        var radius = 0.5f + edgeSoftness;                  // in cells
        var reach = Mathf.CeilToInt(radius * texelsPerCell);
        var inner = Mathf.Max(0.01f, radius - edgeSoftness);

        var cells = fog.Side;
        for (int c = 0; c < cells * cells; c++)
        {
            if (!fog.IsRevealedCell(c)) continue;

            // cell centre in texel space
            var cx = (c % cells + 0.5f) * texelsPerCell;
            var cy = (c / cells + 0.5f) * texelsPerCell;

            for (int dy = -reach; dy <= reach; dy++)
            for (int dx = -reach; dx <= reach; dx++)
            {
                var px = Mathf.FloorToInt(cx) + dx;
                var py = Mathf.FloorToInt(cy) + dy;
                if (px < 0 || py < 0 || px >= side || py >= side) continue;

                var d = Mathf.Sqrt((px + 0.5f - cx) * (px + 0.5f - cx)
                                 + (py + 0.5f - cy) * (py + 0.5f - cy)) / texelsPerCell;
                var a = Mathf.InverseLerp(radius, inner, d);   // 1 inside, 0 past the rim
                var idx = py * side + px;
                if (a > cover[idx]) cover[idx] = a;
            }
        }

        // The stamps leave scallops where neighbouring discs meet. One separable box
        // blur turns the union into a single round edge, which is what the eye reads
        // as "the fog lifted around me" rather than "seven circles were drawn".
        // blur radius is measured in CELLS, not texels — tying it to texel count made
        // the edge sharpen again the moment the grid got finer
        Blur(cover, side, Mathf.Max(2, Mathf.RoundToInt(blurCells * texelsPerCell)));

        var pixels = new Color32[side * side];
        for (int i = 0; i < pixels.Length; i++)
        {
            // smoothstep, not a hard clamp: a sharp cutoff re-exposes the polygon the
            // blur just smoothed away
            var a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(cover[i]));
            pixels[i] = (Color32)new Color(rgb.r, rgb.g, rgb.b, fogColor.a * (1f - a));
        }

        tex.SetPixels32(pixels);
        tex.Apply(false);
    }

    static void Blur(float[] v, int side, int r)
    {
        var tmp = new float[v.Length];

        for (int y = 0; y < side; y++)
        for (int x = 0; x < side; x++)
        {
            float sum = 0f; int n = 0;
            for (int k = -r; k <= r; k++)
            {
                var sx = x + k;
                if (sx < 0 || sx >= side) continue;
                sum += v[y * side + sx]; n++;
            }
            tmp[y * side + x] = sum / n;
        }

        for (int y = 0; y < side; y++)
        for (int x = 0; x < side; x++)
        {
            float sum = 0f; int n = 0;
            for (int k = -r; k <= r; k++)
            {
                var sy = y + k;
                if (sy < 0 || sy >= side) continue;
                sum += tmp[sy * side + x]; n++;
            }
            v[y * side + x] = sum / n;
        }
    }
}
