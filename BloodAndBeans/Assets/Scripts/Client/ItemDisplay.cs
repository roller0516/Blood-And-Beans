using UnityEngine;

/// 아이템 자리(`IItemHolder`)에 실제 3D 오브젝트를 세운다. 손에 든 원두가 원두로 보이고,
/// 기계 위의 완성품이 컵으로 보이게 하는 것이 전부다.
///
/// 이것이 없을 때 낮의 상태는 전부 글자였다 — HUD의 「손 · Bean」 한 줄과 상호작용
/// 프롬프트. 조리대에 무엇이 올라와 있는지는 그 앞까지 걸어가 F 안내를 읽어야 알았고,
/// 그래서 「조리대 너머로 건네주기」(기획서 5.4-2)가 화면에서 성립하지 않았다.
///
/// **표현만 한다.** 무엇이 어디 있는지는 전부 복제된 값이고(`CarryView`), 이 컴포넌트는
/// 그것이 바뀌었을 때만 다시 그린다. 규칙을 묻지도 바꾸지도 않는다.
[DisallowMultipleComponent]
public class ItemDisplay : MonoBehaviour
{
    [SerializeField] ItemVisualConfig config;

    /// 아이템이 놓일 자리. 배열 순서가 곧 `IItemHolder`의 칸 번호다.
    [SerializeField] Transform[] anchors;

    [Header("강조")]
    [Tooltip("다음에 F가 집을 칸을 얼마나 키울지. 재료 칸에서만 쓴다.")]
    [SerializeField] float highlightScale = 1.4f;
    [SerializeField] Vector3 highlightOffset = new(0f, 0.1f, 0f);

    /// 같은 오브젝트에 붙은 자리 주인. **인터페이스는 Inspector에서 이을 수 없어서**
    /// 여기서만 조회한다 (AGENTS.md 참조와 결합도의 예외). 주기 실행이 아니다.
    IItemHolder holder;

    /// 손에 든 것도 팀 밖에서는 보이지 않아야 한다 (기획서 3.1: 재료는 비공개). 카페는
    /// 통째로 팀 레이어에 있지만(`Cafe.OnNetworkSpawn`) 플레이어는 Default라, 카페끼리
    /// 트인 공간에서 상대가 내 손의 컵을 볼 수 있다. 손 앵커만 팀 레이어로 옮겨 막는다.
    PlayerTeam team;

    /// 자리마다 지금 서 있는 것. 프리팹이 그대로면 다시 세우지 않는다 — 재료를 넣을
    /// 때마다 옆 칸까지 새로 만들면 눈에 보이는 튐이 생긴다.
    GameObject[] standing;
    GameObject[] sources;
    bool[] burnt;

    void Awake()
    {
        holder = GetComponent<IItemHolder>();
        team = GetComponent<PlayerTeam>();

        if (holder == null)
            CDebug.LogError($"{name}: 같은 오브젝트에 IItemHolder가 없다. 이 표시는 아무것도 "
                         + "그릴 수 없다.", this);
        if (config == null)
            CDebug.LogError($"{name}: ItemVisualConfig가 비었다. 아이템이 보이지 않는다.", this);

        var count = anchors != null ? anchors.Length : 0;
        standing = new GameObject[count];
        sources = new GameObject[count];
        burnt = new bool[count];
    }

    void OnEnable()
    {
        if (holder != null) holder.ContentsChanged += Refresh;
        if (team != null)
        {
            team.TeamChanged += ApplyTeamLayer;
            ApplyTeamLayer(team.Team);
        }
        Refresh();
    }

    void OnDisable()
    {
        if (holder != null) holder.ContentsChanged -= Refresh;
        if (team != null) team.TeamChanged -= ApplyTeamLayer;
        Clear();
    }

    /// 자리 하나하나를 복제된 값과 맞춘다. 값이 바뀔 때만 불린다.
    void Refresh()
    {
        if (holder == null || config == null || anchors == null) return;

        var highlight = holder.HighlightSlot;

        for (var slot = 0; slot < anchors.Length; slot++)
        {
            var anchor = anchors[slot];
            if (anchor == null) continue;

            var view = slot < holder.SlotCount ? holder.SlotAt(slot) : CarryView.Nothing;
            var prefab = config.PrefabFor(view);

            if (prefab != sources[slot] || view.Burnt != burnt[slot])
            {
                if (standing[slot] != null) Destroy(standing[slot]);
                standing[slot] = prefab != null ? Build(prefab, anchor, view.Burnt) : null;
                sources[slot] = prefab;
                burnt[slot] = view.Burnt;
            }

            if (standing[slot] == null) continue;

            var lit = slot == highlight;
            standing[slot].transform.localPosition = lit ? highlightOffset : Vector3.zero;
            standing[slot].transform.localScale = Vector3.one * (lit ? highlightScale : 1f);
        }
    }

    GameObject Build(GameObject prefab, Transform anchor, bool isBurnt)
    {
        var made = Instantiate(prefab, anchor);
        made.transform.localPosition = Vector3.zero;
        made.transform.localRotation = Quaternion.identity;

        // 카페는 팀 레이어에 있고 카메라가 그것으로 컬링한다 (`TeamVision`). 런타임에
        // 만든 것은 프리팹의 레이어를 그대로 들고 오므로 여기서 자리에 맞춰 준다 —
        // 안 맞추면 상대 팀 화면에 우리 카페의 아이템만 떠 있는다.
        SetLayer(made, anchor.gameObject.layer);

        if (isBurnt && config.Burnt != null)
            foreach (var r in made.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterial = config.Burnt;

        return made;
    }

    static void SetLayer(GameObject root, int layer)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }

    /// 팀이 정해지면 손 앵커를 그 팀의 레이어로 옮긴다. 이미 서 있는 아이템도 앵커의
    /// 자식이라 같이 따라간다.
    void ApplyTeamLayer(int myTeam)
    {
        if (anchors == null) return;

        foreach (var anchor in anchors)
            if (anchor != null) TeamVision.ApplyTeamLayer(anchor.gameObject, myTeam);
    }

    void Clear()
    {
        if (standing == null) return;

        for (var slot = 0; slot < standing.Length; slot++)
        {
            if (standing[slot] != null) Destroy(standing[slot]);
            standing[slot] = null;
            sources[slot] = null;
            burnt[slot] = false;
        }
    }
}
