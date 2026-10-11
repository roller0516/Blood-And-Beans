using Unity.Netcode;
using UnityEngine;

/// 남이 건 스킬로 이 플레이어에게 붙은 상태 (기획서 9.1.2 · 9.2). **기절은 없다** — 길어야 3초다.
///
/// 거는 쪽(능력 객체)은 판정만 하고, 걸린 뒤의 효과는 전부 여기서 기존 축으로 흘려보낸다:
/// 이동 배수는 `PlayerCharacter`가, 손 잠금은 `PlayerCarry.Reserved`가, 밀림·넘어짐은
/// `DashHarass`가 맡는다. 그래서 상태가 늘어도 이동·상호작용 코드는 그대로다.
///
/// 종료 시각을 복제한다. 이동 배수는 소유자 예측에 들어가고(`PlayerController.speedScale`),
/// 나머지는 남의 화면에도 그려져야 한다.
[RequireComponent(typeof(PlayerTeam))]
public class PlayerStatus : NetworkBehaviour
{
    readonly NetworkVariable<double> zombieUntil = new();
    readonly NetworkVariable<double> frogUntil = new();
    readonly NetworkVariable<double> rootUntil = new();
    readonly NetworkVariable<double> blindUntil = new();
    readonly NetworkVariable<double> hasteUntil = new();

    /// 면역은 판정에만 쓰여 서버만 든다.
    double zombieImmuneUntil;
    double fallImmuneUntil;

    PlayerTeam team;
    PlayerController move;
    DashHarass dash;
    MatchDirector director;

    /// 상태가 걸린 순간. 붙는 연출을 그 시간만큼 그린다 (`EffectManager.PlayAttached`).
    public event System.Action<EffectId, float> Applied;

    double Now => NetworkManager != null ? NetworkManager.ServerTime.Time : 0d;
    bool On(NetworkVariable<double> until) => IsSpawned && until.Value > Now;

    public bool Zombie => On(zombieUntil);
    public bool Frog => On(frogUntil);
    public bool Rooted => On(rootUntil);
    public bool Blind => On(blindUntil);
    public bool Hasted => On(hasteUntil);

    /// 이동 배수. 발이 묶이면 0이다 — 다른 행동은 된다 (9.2 팔 던지기).
    public float MoveSpeedScale =>
        (Rooted ? 0f : 1f) * (Zombie ? DaySkills.BiteSpeed : 1f) *
        (Frog ? DaySkills.FrogSpeed : 1f) * (Hasted ? DaySkills.HowlAllySpeed : 1f);

    public int TeamId => team != null ? team.Team : -1;

    /// 카페 안은 안전지대다 — 스킬이 걸리지 않는다 (9.1).
    public bool InSafeZone => director != null && director.InsideAnyCafe(transform.position);

    void Awake()
    {
        team = GetComponent<PlayerTeam>();
        move = GetComponent<PlayerController>();
        dash = GetComponent<DashHarass>();
    }

    public override void OnNetworkSpawn() => MatchDirector.Bind(BindDirector);
    public override void OnNetworkDespawn() => MatchDirector.Unbind(BindDirector);
    void BindDirector(MatchDirector next) => director = next;

    // --- 거는 쪽. 안전지대·면역이면 false를 돌려준다 ---

    /// 물기와 전염 (9.1.2). 옮은 뒤 면역이라 둘이 서로 주고받으며 영원히 걸지 않는다.
    public bool BiteServer()
    {
        if (!IsServer || InSafeZone || Now < zombieImmuneUntil) return false;
        zombieImmuneUntil = Now + DaySkills.BiteImmuneSeconds;
        return ApplyServer(zombieUntil, DaySkills.BiteSeconds, EffectId.ZombieWalk);
    }

    /// 개구리 저주 (9.1.2). 손 잠금은 `PlayerCarry.Reserved`가 이 값을 읽어 건다.
    public bool FrogServer()
    {
        if (!IsServer || InSafeZone) return false;
        SharedFacility.KickServer(OwnerClientId);
        return ApplyServer(frogUntil, DaySkills.FrogSeconds, EffectId.FrogCurse);
    }

    public bool RootServer()
    {
        if (!IsServer || InSafeZone) return false;
        return ApplyServer(rootUntil, NightSkills.ArmThrowSeconds, EffectId.ArmGrab);
    }

    /// 시야 축소 (9.2 박쥐 떼). 반경은 `FogOfWar`가 이 값을 읽어 줄인다.
    public bool BlindServer()
    {
        if (!IsServer || InSafeZone) return false;
        return ApplyServer(blindUntil, NightSkills.BatSwarmSeconds, EffectId.BatSwarm);
    }

    /// 하울링의 같은 팀 가속. 해로운 효과가 아니라 안전지대를 보지 않는다.
    public bool HasteServer()
    {
        if (!IsServer) return false;
        hasteUntil.Value = Now + DaySkills.HowlAllySeconds;
        return true;
    }

    /// 하울링 도망 (9.1.2). 조작이 죽은 채로 늑대인간 반대쪽으로 밀려난다.
    /// 거리는 자기 걸음으로 그 시간만큼이다 — 기획서가 시간만 정했다.
    public bool FleeServer(Vector3 from)
    {
        if (!IsServer || InSafeZone || dash == null || move == null) return false;
        var away = transform.position - from;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f) away = transform.forward;
        var seconds = DaySkills.HowlFleeSeconds;
        dash.PushServer(away.normalized * (move.WalkSpeed * seconds), seconds, seconds, topple: false);
        return true;
    }

    /// 미끄러운 물약 엉덩방아 (9.2). 제자리에서 넘어진다.
    public bool FallServer()
    {
        if (!IsServer || InSafeZone || dash == null || Now < fallImmuneUntil) return false;
        var seconds = NightSkills.PotionFallSeconds;
        fallImmuneUntil = Now + seconds + NightSkills.PotionImmuneSeconds;
        dash.PushServer(Vector3.zero, seconds, seconds, topple: true);
        StatusCueRpc(EffectId.Dizzy, seconds);
        return true;
    }

    bool ApplyServer(NetworkVariable<double> until, float seconds, EffectId cue)
    {
        until.Value = Now + seconds;
        StatusCueRpc(cue, seconds);
        return true;
    }

    /// 좀비걸음인 사람과 부딪히면 옮는다 (9.1.2). 같은 팀도 옮는다.
    /// 서버의 `CharacterController.Move`가 실제로 부딪힌 순간에만 불린다.
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!IsServer) return;
        var other = hit.collider.GetComponentInParent<PlayerStatus>();
        if (other == null || other == this) return;
        if (Zombie && !other.Zombie) other.BiteServer();
        else if (other.Zombie && !Zombie) BiteServer();
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    void StatusCueRpc(EffectId id, float seconds) => Applied?.Invoke(id, seconds);
}
