using System.Collections.Generic;
using UnityEngine;

/// 아이템 자리(`IItemHolder`)에 실제 3D 오브젝트를 세운다. 손에 든 원두가 원두로 보이고,
/// 기계 위의 완성품이 컵으로 보이게 하는 것이 전부다.
///
/// 이것이 없을 때 낮의 상태는 전부 글자였다 — HUD의 「손 · Bean」 한 줄과 상호작용
/// 프롬프트. 조리대에 무엇이 올라와 있는지는 그 앞까지 걸어가 F 안내를 읽어야 알았고,
/// 그래서 「조리대 너머로 건네주기」(기획서 5.4-2)가 화면에서 성립하지 않았다.
///
/// **표현만 한다.** 무엇이 어디 있는지는 전부 복제된 값이고(`CarryView`), 실제 세우고
/// 치우는 로직은 `ItemSlotPresenter`(일반 C# 객체)가 진다 — 손(`PlayerVisuals`)과 시설이
/// 같은 규칙을 쓴다. 이 컴포넌트에 남은 것은 이 오브젝트의 생애(구독 시작·해제)와
/// 팀 레이어 적용뿐이다.
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

    [Header("개수 제한 없는 자리")]
    [Tooltip("켜면 내용 수만큼 자리를 늘린다 (보관대, 기획서 5.4-13). 잔은 가로로 늘어서고 접시는 위로 쌓인다. "
           + "좌표는 첫 앵커의 부모 기준이다.")]
    [SerializeField] bool unlimited;
    [SerializeField] Vector3 rowStart;
    [SerializeField] Vector3 rowStep = new(0.5f, 0f, 0f);
    [Tooltip("한 줄에 놓을 개수. 차면 wrapStep만큼 옮겨 다음 줄을 시작한다. 0이면 꺾지 않는다.")]
    [SerializeField, Min(0)] int rowWrap;
    [SerializeField] Vector3 wrapStep = new(0f, 0f, 0.5f);
    [SerializeField] Vector3 stackStart;
    [SerializeField] Vector3 stackStep = new(0f, 0.1f, 0f);

    /// 같은 오브젝트에 붙은 자리 주인. **인터페이스는 Inspector에서 이을 수 없어서**
    /// 여기서만 조회한다 (AGENTS.md 참조와 결합도의 예외). 주기 실행이 아니다.
    IItemHolder holder;

    /// 손에 든 것도 팀 밖에서는 보이지 않아야 한다 (기획서 3.1: 재료는 비공개). 카페는
    /// 통째로 팀 레이어에 있지만(`Cafe.OnNetworkSpawn`) 플레이어는 Default라, 카페끼리
    /// 트인 공간에서 상대가 내 손의 컵을 볼 수 있다. 손 앵커만 팀 레이어로 옮겨 막는다.
    PlayerTeam team;

    ItemSlotPresenter presenter;

    /// 실제 칸 목록. `unlimited`면 직렬화된 `anchors` 뒤로 런타임에 늘어난다.
    List<Transform> slots;

    void Awake()
    {
        holder = GetComponent<IItemHolder>();
        team = GetComponent<PlayerTeam>();
        slots = new List<Transform>(anchors ?? System.Array.Empty<Transform>());

        if (holder == null)
            CDebug.LogError($"{name}: 같은 오브젝트에 IItemHolder가 없다. 이 표시는 아무것도 "
                         + "그릴 수 없다.", this);
        if (config == null)
            CDebug.LogError($"{name}: ItemVisualConfig가 비었다. 아이템이 보이지 않는다.", this);
        if (unlimited && (slots.Count == 0 || slots[0] == null))
            CDebug.LogError($"{name}: 개수 제한 없는 자리는 새 칸을 달 첫 앵커가 있어야 한다.", this);

        presenter = new ItemSlotPresenter(config, anchors, highlightScale, highlightOffset);
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
        presenter.Clear();
    }

    void Refresh()
    {
        if (holder == null) return;
        if (unlimited) Arrange();
        presenter.Bind(holder);
    }

    /// 내용 수만큼 칸을 늘리고 놓인 순서대로 자리를 준다. 잔은 가로로, 접시는 위로 쌓는다.
    /// ponytail: 줄 수에는 상한이 없어 상판 깊이를 넘게 놓으면 밖으로 나간다. 식기 수가 그만큼 늘면 층을 올린다.
    void Arrange()
    {
        if (slots.Count == 0 || slots[0] == null) return;

        var row = 0;
        var stack = 0;
        for (var slot = 0; slot < holder.SlotCount; slot++)
        {
            if (slot == slots.Count) AddSlot();
            var position = holder.SlotAt(slot).DishIsPlate
                ? stackStart + stackStep * stack++
                : RowPosition(row++);
            if (slots[slot] != null) slots[slot].localPosition = position;
        }
    }

    Vector3 RowPosition(int index) => rowWrap > 0
        ? rowStart + rowStep * (index % rowWrap) + wrapStep * (index / rowWrap)
        : rowStart + rowStep * index;

    void AddSlot()
    {
        var first = slots[0];
        var anchor = new GameObject($"Slot{slots.Count}").transform;
        anchor.SetParent(first.parent, false);
        anchor.gameObject.layer = first.gameObject.layer;
        slots.Add(anchor);
        presenter.AddSlot(anchor);
    }

    /// 팀이 정해지면 손 앵커를 그 팀의 레이어로 옮긴다. 이미 서 있는 아이템도 앵커의
    /// 자식이라 같이 따라간다.
    void ApplyTeamLayer(int myTeam)
    {
        foreach (var anchor in slots)
            if (anchor != null) TeamVision.ApplyTeamLayer(anchor.gameObject, myTeam);
    }
}
