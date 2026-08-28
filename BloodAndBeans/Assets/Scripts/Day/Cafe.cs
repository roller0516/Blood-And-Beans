using Unity.Netcode;
using UnityEngine;

/// 한 팀의 카페. 설비가 찾아야 하는 것이 전부 여기 모여 있어서 낮 시스템이 static
/// 싱글턴을 뒤지지 않아도 된다. 팀 수만큼 런타임에 스폰된다.
///
/// NetworkObject는 이 루트에만 있다. 설비들은 그 아래의 NetworkBehaviour일 뿐이다.
/// NGO 2.13은 동적으로 스폰한 프리팹의 자식 NetworkObject를 복제하지 않기 때문이다
/// (NetworkSpawnManager.cs: "Spawning NetworkObjects with nested NetworkObjects is only
/// supported for scene objects"). 덕분에 팀 은닉도 이 NetworkObject 하나로 끝난다.
[RequireComponent(typeof(NetworkObject))]
public class Cafe : NetworkBehaviour
{
    /// 스폰 페이로드에 실려 모든 피어가 같은 값을 받는다. 예전에는 씬 슬롯 순서가
    /// 팀 번호였는데, 카페가 런타임 생성이 되면서 그 순서가 사라졌다.
    readonly NetworkVariable<int> team = new();
    int pendingServerTeam = -1;

    /// 카페는 키트 모델의 명암을 살려야 해서 옅게만 물들인다. 1로 올리면 찬장·의자·싱크대가
    /// 전부 한 색이 되어 형태가 사라진다.
    [SerializeField, Range(0f, 1f)] float teamTintStrength = 0.35f;

    public int TeamId => team.Value;

    public Dish Dishes { get; private set; }
    public CustomerQueue Queue { get; private set; }
    public TeamStock Stock { get; private set; }

    /// 매출판. 판에 하나뿐이고 카페가 소유하지 않는다 (기획서 3.1: 재료·설비·캐릭터는
    /// 비공개지만 *매출은 공개*다). 카페는 상대 팀에 복제되지 않으므로, 매출판을 카페에
    /// 매달면 남의 매출을 볼 방법이 영영 없다 — 순위표가 자기 팀만 보이고 나머지는 0이 된다.
    public Scoreboard Board => director != null ? director.Board : null;

    /// 이 카페의 게이지 캐시. 모든 카페의 게이지를 한꺼번에 담던 static 리스트를 대체한다.
    /// 그 리스트 때문에 한 팀이 다른 팀의 오븐을 멈출 수 있었다 (아키텍처_v1.0.md §1.2).
    public CompletionGauge[] Gauges { get; private set; } = new CompletionGauge[0];

    MatchDirector director;

    /// 이 카페 밑의 설비들이 조립 루트를 찾는 단 하나의 통로. 설비가 각자 전역을 뒤지면
    /// 카페마다 다른 답을 받을 여지가 생긴다 (아키텍처_v1.0.md §1.4).
    public MatchDirector Director => director;

    /// 서버가 Spawn 직전에 주입한다. 클라이언트에는 복제 경로가 없어서 스폰 때 스스로 푼다.
    public void BindDirectorServer(MatchDirector value) => director = value;

    void Awake()
    {
        Dishes = GetComponentInChildren<Dish>(true);
        Queue = GetComponentInChildren<CustomerQueue>(true);
        Stock = GetComponentInChildren<TeamStock>(true);
        Gauges = GetComponentsInChildren<CompletionGauge>(true);
    }

    /// 서버가 Spawn 직전에 부른다. NetworkBehaviour가 준비되기 전 NetworkVariable을 쓰면
    /// NGO가 경고하므로 값을 보관했다가 OnNetworkSpawn에서 적용한다.
    public void AssignTeamServer(int value) => pendingServerTeam = value;

    public override void OnNetworkSpawn()
    {
        // 서버 OnNetworkSpawn은 관측자에게 스폰 메시지를 보내기 전에 실행된다. 여기서 넣은
        // 값은 나중에 NetworkShow되는 팀 클라이언트의 초기 동기화에 포함된다.
        if (IsServer && pendingServerTeam >= 0) team.Value = pendingServerTeam;

        // 카메라 컬링이 레이어로 팀을 가른다. 프리팹 하나를 여러 팀이 쓰므로 레이어는
        // 씬에 구워 둘 수 없고 스폰 시점에 정해야 한다.
        TeamVision.ApplyCafeLayer(gameObject, team.Value);

        // 색도 같은 이유로 스폰 시점이다. 서버·클라이언트 양쪽에서 실행되므로 보는 쪽에도 걸린다.
        TeamColors.Tint(gameObject, team.Value, teamTintStrength);

        // 서버는 `BindDirectorServer`로 이미 받았다. 클라이언트만 여기서 푼다.
        if (director == null) director = MatchDirector.Instance;
        if (director == null)
        {
            Debug.LogError($"{name}: 씬에 MatchDirector가 없다. 이 카페는 어느 팀 조회에도 "
                         + "잡히지 않는다.", this);
            return;
        }
        director.RegisterCafe(this);
    }

    public override void OnNetworkDespawn()
    {
        if (director != null) director.UnregisterCafe(this);
    }

    /// 모든 설비는 자기 카페 밑에 붙어 있으므로, 소유 판정은 부모를 거슬러 올라가면 끝난다.
    public static Cafe Of(Component c) => c == null ? null : c.GetComponentInParent<Cafe>();

    /// 손이 닿는다고 권한이 있는 것은 아니다. 카페를 나누는 것은 거리뿐이라, 낮 쪽 서버
    /// 진입점은 전부 여기가 누구의 주방인지 물어야 한다. 그러지 않으면 플레이어가 상대
    /// 카페로 걸어 들어가 재고를 비우거나 상대 서빙대에서 판매할 수 있다.
    /// 완성 게이지와 같은 종류의 결함이다 (아키텍처_v1.0.md §1.2).
    public static bool SameTeamServer(Component fixture, ulong clientId)
    {
        var cafe = Of(fixture);
        return cafe != null && cafe.IsSpawned && PlayerTeam.Of(clientId) == cafe.TeamId;
    }
}
