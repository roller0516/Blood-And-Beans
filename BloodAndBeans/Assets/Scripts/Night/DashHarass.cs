using System;
using Unity.Netcode;
using UnityEngine;

/// 대시 밀치기 (기획서 6.6). 스킬이 아니라 밤에 존재하는 유일한 공격 행동이다.
/// 소유자는 요청만 한다. 돌진 이동, 대상 선정, 결과 판정은 전부 서버가 한다.
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerMove))]
public class DashHarass : NetworkBehaviour
{
    // ponytail: 기획서 14장 #7/#8에서 미결정이다. 중간값으로 둔 임시값이다.
    [SerializeField] float cooldown = 6f;        // 기획서: 5~8초
    [SerializeField] float reach = 1.6f;
    [SerializeField] float knockback = 3f;       // 약 1.5타일
    [SerializeField] float spillShare = 0.1f;
    [SerializeField] float spillAtLoad = 0.8f;
    [SerializeField] float spawnProtectionSeconds = 15f;

    /// 가방이 이 비율 이상 차면 대시를 쓸 수 없다. 무게를 비우려면 가방을 묻어야 하고,
    /// 그것이 기동성과 수확을 맞바꾸는 선택지다.
    [SerializeField] float dashBlockedAtLoad = 0.6f;

    [Header("돌진")]
    // ponytail: 거리·시간도 기획서 미결정이다. 걷는 속도(5)의 약 4배로 잡은 임시값이다.
    [SerializeField] float dashDistance = 3.5f;
    [SerializeField] float dashSeconds = 0.18f;

    /// 맞힌 쪽도 부딪힌 반작용으로 뒤로 조금 튕긴다. 맞은 쪽만 밀리면 벽을 통과한 것처럼
    /// 보여서 충돌한 느낌이 나지 않는다.
    [SerializeField] float recoil = 0.7f;
    [SerializeField] float recoilSeconds = 0.12f;

    [Header("넘어짐")]
    [SerializeField] float knockdownAngle = 80f;     // 밀린 방향으로 눕는 각도
    [SerializeField] float standUpSeconds = 0.25f;   // 경직이 풀리는 시각에 맞춰 일어서는 데 쓰는 시간

    const float KnockSeconds = 0.15f;

    double nextDash;                             // 서버 측 값. 절대 클라이언트에서 받지 않는다

    /// 다음 대시가 가능해지는 서버 시각. 소유자만 읽고 서버만 쓴다 — 남의 쿨다운을 알
    /// 이유가 없고, 클라이언트가 쓸 수 있으면 쿨다운이 없는 것과 같다.
    readonly NetworkVariable<double> nextDashAt = new(0d,
        NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

    /// 돌진이 시작됐다. 인자는 돌진이 지속되는 시간이다. 위치와 방향은 싣지 않는다 —
    /// NetworkTransform이 이미 보내고 있고, 두 경로가 어긋나면 잔상이 몸과 따로 논다.
    public event Action<float> DashStarted;

    /// 이 플레이어가 상대를 맞혔다. 맞은 자리와, 그 대시로 상대의 재료가 쏟아졌는지.
    public event Action<Vector3, bool> HitLanded;

    /// 이 플레이어가 맞았다. 밀려나는 방향과, 자기 재료가 쏟아졌는지.
    public event Action<Vector3, bool> TookHit;

    CharacterController controller;
    PlayerMove move;
    PlayerInventory inventory;

    Vector3 dashDirection;
    float dashEnd;
    bool dashHitResolved;

    float stunEnd;
    float knockStart;
    Vector3 knockFrom, knockTo;
    Vector3 toppleAxis;                          // 월드 기준 축. 밀린 방향과 수직인 수평축이다
    Quaternion uprightRotation;                  // 넘어지기 직전의 자세. 복구 목표값이다
    float pushSeconds;                           // 이번 밀림이 미끄러지는 시간
    bool pushing;
    bool toppling;                               // 넘어지는 밀림인가. 반동은 서 있는 채로 밀린다

    /// 돌진과 넉백은 둘 다 LateUpdate에서 위치를 직접 민다. 그동안 PlayerMove가 같이
    /// 밀면 두 이동이 더해지므로, 조작 입력은 이 구간 내내 죽인다.
    public bool SuppressesInputServer => IsServer && (Time.time < stunEnd || Time.time < dashEnd);

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        move = GetComponent<PlayerMove>();
        inventory = GetComponent<PlayerInventory>();
    }

    /// 표시 전용. 무게 때문에 대시가 막혔는지 소유자에게 알려 준다. 판정은 서버가 다시 한다.
    public bool BlockedByLoad => inventory != null && inventory.LoadRatio >= dashBlockedAtLoad;

    /// 표시 전용. 남은 쿨다운(초). 소유자 외에는 0이다 — 복제 권한이 소유자뿐이라
    /// 남의 화면에서는 애초에 값이 오지 않는다.
    public float CooldownRemaining
    {
        get
        {
            if (!IsSpawned) return 0f;
            var left = nextDashAt.Value - NetworkManager.ServerTime.Time;
            return left > 0d ? (float)left : 0f;
        }
    }

    /// PlayerMove보다 뒤에 실행되므로 여기서 위치를 밀면 이번 프레임의 입력이 덮인다.
    /// 돌진과 넉백이 같은 자리에서 위치를 소유한다.
    ///
    /// 회전은 NetworkTransform이 SyncRotAngleX/Y/Z로 복제한다(서버 권위). 그래서 서버에서만
    /// 돌리고 클라이언트는 아무것도 계산하지 않는다.
    void LateUpdate()
    {
        if (!IsServer) return;

        if (Time.time < dashEnd) DashStepServer();
        if (pushing) PushStepServer();
    }

    /// 돌진 한 틱. 앞으로 밀면서 경로 위에서 상대를 찾는다. 시전 순간 한 번만 검사하면
    /// 이동 중에 스쳐 지나간 상대를 놓친다.
    void DashStepServer()
    {
        // 대입이 아니라 Move다. 돌진 중에도 벽과 설비를 뚫지 않는다.
        controller.Move(dashDirection * (dashDistance / dashSeconds * Time.deltaTime));

        if (dashHitResolved || !HarassAllowedServer()) return;

        var victim = NearestVictim();
        if (victim == null) return;

        dashHitResolved = true;
        ResolveHitServer(victim);   // 여기서 반동으로 밀리며 돌진이 끝난다
    }

    /// 넉백과 반동이 같은 처리를 쓴다. 차이는 넘어지느냐뿐이다.
    void PushStepServer()
    {
        if (Time.time >= stunEnd)
        {
            // 경직이 풀리는 프레임에 정확히 원래 자세로 돌려놓는다. 보간 잔여로 기울어진 채
            // 남으면 복제된 회전이 그대로 굳는다.
            if (toppling) transform.rotation = uprightRotation;
            pushing = false;
            toppling = false;
            return;
        }

        var slide = Vector3.Lerp(
            knockFrom, knockTo, Mathf.Clamp01((Time.time - knockStart) / pushSeconds));

        // 대입이 아니라 Move다. 밀려나는 도중에도 벽과 설비를 뚫지 않는다.
        controller.Move(slide - transform.position);

        if (toppling)
            transform.rotation = Quaternion.AngleAxis(ToppleAngle(Time.time), toppleAxis) * uprightRotation;
    }

    /// 밀리는 동안 눕고, 누운 채로 경직을 보내다가, 경직이 끝나는 시각에 맞춰 다 일어선다.
    float ToppleAngle(float now)
    {
        var fallEnd = knockStart + KnockSeconds;
        if (now < fallEnd) return knockdownAngle * Mathf.InverseLerp(knockStart, fallEnd, now);

        // 경직이 짧으면 눕는 즉시 일어나기 시작한다. 복구 완료 시점은 항상 stunEnd다.
        var riseStart = Mathf.Max(fallEnd, stunEnd - standUpSeconds);
        if (now <= riseStart) return knockdownAngle;
        return knockdownAngle * (1f - Mathf.InverseLerp(riseStart, stunEnd, now));
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void DashRpc(RpcParams p = default)
    {
        // 이동 자체는 밤이면 된다. 보호 구간 제한은 "피해 효과"에 걸리는 것이라
        // (기획서 4장 Night 0, 6.4) HarassAllowedServer에서 따로 본다.
        var phase = MatchDirector.Instance?.Phase;
        if (phase == null || phase.Current != Phase.Night) return;
        if (Time.time < stunEnd) return;                         // 경직 중에는 돌진하지 않는다

        // 가방이 무거우면 대시가 없다. 서버가 판정한다 — 소유자에게 맡기면 무게 제한이
        // 없는 것과 같다.
        if (inventory != null && inventory.LoadRatio >= dashBlockedAtLoad) return;

        if (NetworkManager.ServerTime.Time < nextDash) return;
        nextDash = NetworkManager.ServerTime.Time + cooldown;
        nextDashAt.Value = nextDash;

        dashDirection = move.FacingServer;
        dashEnd = Time.time + dashSeconds;
        dashHitResolved = false;

        DashStartedRpc(dashSeconds);
    }

    // --- 연출 알림. 판정은 위에서 이미 끝났고, 아래는 그리기 위한 통지뿐이다 ---

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    void DashStartedRpc(float seconds) => DashStarted?.Invoke(seconds);

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    void HitLandedRpc(Vector3 at, bool spilled) => HitLanded?.Invoke(at, spilled);

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    void TookHitRpc(Vector3 direction, bool spilled) => TookHit?.Invoke(direction, spilled);

    /// 첫 밤 전체와 이후 밤의 첫 15초에는 피해 효과가 없다 (기획서 4장 Night 0, 6.4).
    bool HarassAllowedServer()
    {
        var phase = MatchDirector.Instance?.Phase;
        return phase != null && phase.Current == Phase.Night
            && phase.Day > 1 && phase.Elapsed >= spawnProtectionSeconds;
    }

    void ResolveHitServer(NetworkObject victim)
    {
        var inv = victim.GetComponent<PlayerInventory>();
        var load = inv != null ? inv.LoadRatio : 0f;

        // 재료가 쏟아졌는지는 연출이 갈리는 기준이다. 밀리기만 한 것과 수확을 흘린 것은
        // 맞은 사람에게 전혀 다른 사건이다 (기획서 6.6).
        var spilled = inv != null && load >= spillAtLoad;
        if (spilled) inv.DropShareServer(spillShare, victim.transform.position);
        victim.GetComponent<PlayerInteract>()?.InterruptServer();

        var dir = victim.transform.position - transform.position;
        dir.y = 0f;
        dir = dir.sqrMagnitude < 0.0001f ? dashDirection : dir.normalized;

        // 무겁게 들고 있을수록 오래 휘청인다. 상한은 1초다 (기획서 6.6).
        victim.GetComponent<DashHarass>()
             ?.HitServer(dir * knockback, Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(load)), spilled);

        // 부딪힌 반작용. 맞은 방향의 반대로 튕기되 넘어지지는 않는다.
        PushServer(-dir * recoil, recoilSeconds, recoilSeconds, topple: false);

        HitLandedRpc(victim.transform.position, spilled);
    }

    void HitServer(Vector3 push, float stunSeconds, bool spilled)
    {
        PushServer(push, stunSeconds, KnockSeconds, topple: true);
        TookHitRpc(push.sqrMagnitude > 0.0001f ? push.normalized : Vector3.forward, spilled);
    }

    /// 서버가 플레이어를 밀어내는 단 하나의 경로. 미끄러지는 동안 조작은 죽는다.
    void PushServer(Vector3 push, float lockSeconds, float slideSeconds, bool topple)
    {
        if (!IsServer) return;

        // 밀리는 쪽의 돌진은 즉시 끊는다. 남겨 두면 넉백과 돌진이 서로 위치를 밀어낸다.
        dashEnd = 0f;

        knockFrom = transform.position;
        knockTo = transform.position + push;
        knockStart = Time.time;
        pushSeconds = Mathf.Max(0.01f, slideSeconds);
        stunEnd = Time.time + Mathf.Max(lockSeconds, pushSeconds);
        pushing = true;

        if (!topple) return;

        // 밀린 방향으로 머리가 넘어간다. up을 push 방향으로 눕히는 축이 그 수직 수평축이다.
        var flat = new Vector3(push.x, 0f, push.z);
        toppleAxis = flat.sqrMagnitude < 0.0001f
            ? transform.right
            : Vector3.Cross(Vector3.up, flat.normalized);

        // 넘어져 있는 동안 또 맞아도 복구 목표는 처음의 선 자세다. 기울어진 자세를 목표로
        // 잡으면 연속으로 맞을 때마다 누운 각도가 누적된다.
        if (!toppling) uprightRotation = transform.rotation;
        toppling = true;
    }

    NetworkObject NearestVictim()
    {
        NetworkObject best = null;
        var bestDist = reach;

        foreach (var client in NetworkManager.ConnectedClientsList)
        {
            var player = client.PlayerObject;
            if (player == null || player == NetworkObject) continue;
            if (PlayerTeam.Of(player.OwnerClientId) == PlayerTeam.Of(OwnerClientId)) continue;

            var d = Vector3.Distance(transform.position, player.transform.position);
            if (d > bestDist) continue;
            best = player;
            bestDist = d;
        }
        return best;
    }
}
