using Unity.Netcode;
using UnityEngine;

/// Gives each species a distinct body and colour. Doc 5.5 gives every species a
/// different patience and price, so telling them apart across the room is a
/// gameplay read, not decoration.
/// ponytail: recoloured stand-ins from a CC0 human kit — swap the meshes for real
/// undead models when the art pipeline exists; nothing else has to change.
public class CustomerLook : NetworkBehaviour
{
    [SerializeField] GameObject[] bySpecies = new GameObject[6];

    static readonly Color[] Tint =
    {
        new(0.45f, 0.65f, 0.35f),   // Zombie   — sickly green
        new(0.62f, 0.12f, 0.16f),   // Vampire  — blood red
        new(0.70f, 0.85f, 0.95f),   // Ghost    — pale blue
        new(0.92f, 0.90f, 0.82f),   // Skeleton — bone
        new(0.40f, 0.28f, 0.18f),   // Werewolf — dark fur
        new(0.45f, 0.25f, 0.60f),   // Witch    — violet
    };

    [SerializeField] float bodyHeight = 1.6f;   // world units, so every species matches

    Species? shown;
    GameObject body;

    public override void OnNetworkSpawn()
    {
        var c = GetComponent<Customer>();
        c.SpeciesChanged += Apply;
        Apply(c.Kind);
    }

    public override void OnNetworkDespawn()
    {
        var c = GetComponent<Customer>();
        if (c != null) c.SpeciesChanged -= Apply;
    }

    void Apply(Species s)
    {
        if (shown == s) return;
        var i = (int)s;
        if (i < 0 || i >= bySpecies.Length || bySpecies[i] == null) return;
        shown = s;

        // Destroy is deferred to end of frame, so a name lookup would still find the
        // old body this frame. Keep the reference and drop it explicitly.
        if (body != null) { body.name = "Body(old)"; Destroy(body); }

        // hide the placeholder capsule, show the species body
        var own = GetComponent<MeshRenderer>();
        if (own != null) own.enabled = false;

        body = Instantiate(bySpecies[i], transform, false);
        body.name = "Body";
        body.transform.localPosition = Vector3.zero;   // model pivot is at the feet
        body.transform.localScale = Vector3.one;

        // Kenney's characters are rigged, so their renderers are Skinned, not Mesh —
        // tinting only MeshRenderer left every species the same colour.
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Tint[i] };
        foreach (var r in body.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;

        // Source kits do not agree on scale, so normalise by measured height rather
        // than trusting a magic multiplier per kit.
        var h = MeasuredHeight(body);
        if (h > 0.001f) body.transform.localScale = Vector3.one * (bodyHeight / h);
    }

    static float MeasuredHeight(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return 0f;
        var b = rs[0].bounds;
        for (int k = 1; k < rs.Length; k++) b.Encapsulate(rs[k].bounds);
        return b.size.y;
    }
}
