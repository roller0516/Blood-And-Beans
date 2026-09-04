using Unity.Netcode;
using UnityEngine;

/// 그릇 재고 전체를 오브젝트 네 개가 아니라 컴포넌트 하나로 다룬다 (기획서 5.3).
/// 규칙에 필요한 것은 개수뿐이다. 그릇은 깨끗하거나, 사용 중이거나, 더럽다.
///
/// 총 개수를 미는 요인이 둘이다 — 임대료 3단계는 하나를 깨뜨리고(3.3), 「그릇 추가」
/// 업그레이드는 둘을 더한다(8장). 둘을 각자 `total`을 건드리게 두면 순서에 따라 결과가
/// 달라지므로, 요인은 플래그로만 들고 총 개수는 항상 한 곳에서 다시 계산한다.
public class Dish : NetworkBehaviour
{
    // ponytail: 4는 기획서 기본값이다. 실제 개수는 기획서 14장 #5에서 미결정이다.
    [SerializeField] int total = 4;

    /// 「그릇 추가」가 늘려 주는 개수 (기획서 8장: 4개 → 6개).
    [SerializeField] int extraDishes = 2;

    /// 「식기세척기」가 더러운 그릇 하나를 씻는 데 걸리는 시간.
    /// ponytail: 기획서 8장은 "느리게"라고만 하고 수치를 주지 않았다. 손 세척
    /// (`Sink.washSeconds` 1.5초)보다 확실히 느려야 한다는 것만 확정이다. 표가 생기면 옮긴다.
    [SerializeField] float dishwasherSeconds = 6f;

    int baseTotal;

    /// 임대료 3단계로 하나가 깨져 있는가 (기획서 3.3).
    bool broken;

    readonly NetworkVariable<int> clean = new();
    readonly NetworkVariable<int> dirty = new();

    /// 소속 카페. 업그레이드 상태의 출처다. 설비는 전역이 아니라 자기 카페에서 받는다
    /// (아키텍처_v1.0.md §1.4).
    Cafe cafe;

    /// 식기세척기가 다음 그릇을 꺼내는 서버 시각. 더러운 그릇이 처음 생길 때 시작한다.
    double nextAutoWash;

    public int Clean => clean.Value;
    public int Dirty => dirty.Value;
    public int InUse => total - clean.Value - dirty.Value;

    public override void OnNetworkSpawn()
    {
        baseTotal = total;
        cafe = Cafe.Of(this);
        if (cafe != null) cafe.UpgradesChanged += OnUpgradesChanged;

        if (IsServer) clean.Value = total;
    }

    public override void OnNetworkDespawn()
    {
        if (cafe != null) cafe.UpgradesChanged -= OnUpgradesChanged;
    }

    void OnUpgradesChanged()
    {
        if (IsServer) ApplyTotalServer();
    }

    /// 주문이 시작될 때 Station이 호출한다. false면 깨끗한 그릇이 없어 새 주문을 받을 수 없다.
    public bool ClaimServer()
    {
        if (!IsServer || clean.Value <= 0) return false;
        clean.Value--;
        return true;
    }

    /// 서빙했든 버렸든 그릇은 더러운 상태로 돌아온다.
    public void SoilServer()
    {
        if (!IsServer || InUse <= 0) return;   // 실제로 나가 있는 그릇만 돌아올 수 있다

        // 더러운 더미가 비어 있다가 처음 생겼다. 식기세척기 시계는 여기서 시작한다 —
        // 0에서 출발시키면 설치 직후 첫 그릇이 대기 없이 즉시 씻긴다.
        if (dirty.Value == 0) nextAutoWash = NetworkManager.ServerTime.Time + dishwasherSeconds;
        dirty.Value++;
    }

    /// 임대료 페널티 3단계는 그릇 하나를 깨뜨린다. 그날 하루만이다 (기획서 3.3: 모든
    /// 페널티는 하루 유지되고 임대료를 내면 풀린다). 정산 때마다 현재 단계로 호출되므로
    /// 깨지는 만큼 쉽게 복구된다.
    public void SetBreakageServer(bool value)
    {
        if (!IsServer || broken == value) return;
        broken = value;
        ApplyTotalServer();
    }

    /// 지금 있어야 할 총 개수. 최소 하나는 남긴다 — 0이 되면 그 팀은 그날 아무것도
    /// 만들 수 없고, 기획서 3.3이 "매출을 직접 깎는 페널티는 두지 않는다"고 했다.
    int WantedTotal => Mathf.Max(1,
        baseTotal + (HasDishwasherBonus ? extraDishes : 0) - (broken ? 1 : 0));

    bool HasDishwasherBonus => cafe != null && cafe.HasUpgrade(UpgradeId.ExtraDishes);

    /// 총 개수를 목표치에 맞춘다. 나가 있는 그릇은 건드리지 않는다 — 만들던 주문에서
    /// 그릇을 빼앗으면 그 주문이 그릇 없이 완성된다.
    void ApplyTotalServer()
    {
        if (!IsServer) return;

        var want = WantedTotal;
        var delta = want - total;
        if (delta == 0) return;
        total = want;

        if (delta > 0) { clean.Value += delta; return; }

        for (var i = 0; i < -delta; i++)
        {
            if (clean.Value > 0) clean.Value--;
            else if (dirty.Value > 0) dirty.Value--;
        }
    }

    public void WashServer()
    {
        if (!IsServer || dirty.Value <= 0) return;
        dirty.Value--;
        clean.Value++;
    }

    /// 「식기세척기」는 더러운 그릇을 사람 없이 씻는다 (기획서 8장). 낮에만 돈다 —
    /// 밤에는 그릇을 쓰는 사람도 더럽히는 사람도 없다.
    void Update()
    {
        if (!IsServer || dirty.Value <= 0) return;
        if (cafe == null || !cafe.HasUpgrade(UpgradeId.Dishwasher)) return;

        var director = cafe.Director;
        if (director == null || director.Phase == null ||
            director.Phase.Current != Phase.Day) return;

        var now = NetworkManager.ServerTime.Time;
        if (now < nextAutoWash) return;

        nextAutoWash = now + dishwasherSeconds;
        WashServer();
    }
}
