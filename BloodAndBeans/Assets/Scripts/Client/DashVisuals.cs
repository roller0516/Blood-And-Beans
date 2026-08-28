using DG.Tweening;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

/// 대시의 연출만 맡는다. 판정·이동·쿨다운은 전부 서버의 `DashHarass`에 있고, 여기서는
/// 그쪽이 던지는 이벤트를 받아 그리기만 한다. 한 클래스에 섞으면 연출을 고치다 판정을
/// 건드리게 된다 — 대시는 밤에 존재하는 유일한 공격 행동이라 그 사고가 곧 밸런스 사고다.
[RequireComponent(typeof(DashHarass))]
public class DashVisuals : MonoBehaviour
{
    [Header("돌진")]
    /// 돌진하는 동안만 켜지는 잔상. 비워 두면 나머지 연출만 돈다.
    [SerializeField] TrailRenderer trail;

    [Header("피격 번쩍임")]
    /// 팀 색을 소유한 쪽. 번쩍인 뒤 되돌리는 것은 이쪽이 한다.
    [SerializeField] PlayerLook look;
    [SerializeField] Color hitFlash = Color.white;

    /// 재료가 쏟아진 대시는 다른 색으로 번쩍인다. 밀리기만 한 것과 수확을 흘린 것은
    /// 맞은 사람에게 전혀 다른 사건인데, 지금까지 화면에 아무 차이가 없었다 (기획서 6.6).
    [SerializeField] Color spillFlash = new(1f, 0.65f, 0.15f);
    [SerializeField] float flashSeconds = 0.16f;

    [Header("임팩트")]
    /// 맞은 자리에 한 번 터지는 파티클. 비워 두면 생략한다.
    [SerializeField] ParticleSystem impactPrefab;

    /// 다 터진 파티클을 치우기까지의 시간. 파티클의 수명보다 길어야 중간에 잘리지 않는다.
    [SerializeField] float impactLifetime = 2f;

    [Header("화면 흔들림")]
    /// 흔들림을 쏘는 곳. 가상 카메라의 `CinemachineImpulseListener`가 받는다 — 카메라를
    /// 직접 밀면 시네머신이 다음 프레임에 되돌려 놓는다.
    [SerializeField] CinemachineImpulseSource impulse;

    /// 맞은 쪽이 맞힌 쪽보다 세고, 재료를 흘렸으면 가장 세다.
    [SerializeField] float shakeOnLand = 0.10f;
    [SerializeField] float shakeOnTaken = 0.28f;
    [SerializeField] float shakeOnSpill = 0.45f;

    DashHarass dash;
    NetworkObject netObject;

    void Awake()
    {
        dash = GetComponent<DashHarass>();
        netObject = GetComponent<NetworkObject>();
        if (trail != null) trail.emitting = false;
    }

    void OnEnable()
    {
        dash.DashStarted += OnDashStarted;
        dash.HitLanded += OnHitLanded;
        dash.TookHit += OnTookHit;
    }

    void OnDisable()
    {
        dash.DashStarted -= OnDashStarted;
        dash.HitLanded -= OnHitLanded;
        dash.TookHit -= OnTookHit;
    }

    /// 내 화면인가. 흔들림은 이 값이 참일 때만 준다 — 남이 맞은 것으로 내 화면이 흔들리면
    /// 무엇에 맞았는지 알 수 없다.
    bool IsMine => netObject != null && netObject.IsLocalPlayer;

    void OnDashStarted(float seconds)
    {
        if (trail == null) return;

        // 지난 잔상이 남아 있으면 새 돌진이 이전 자리에서 시작한 것처럼 보인다.
        trail.Clear();
        trail.emitting = true;
        DOVirtual.DelayedCall(seconds, StopTrail).SetLink(gameObject);
    }

    void StopTrail()
    {
        if (trail != null) trail.emitting = false;
    }

    /// 내가 맞혔다. 임팩트는 맞은 자리에 남기고, 흔들림은 내 화면에만 준다.
    void OnHitLanded(Vector3 at, bool spilled)
    {
        SpawnImpact(at, spilled);
        if (IsMine) Shake(shakeOnLand);
    }

    /// 내가 맞았다. 번쩍임은 모두에게 보이고 — 누가 맞았는지가 상대에게도 정보다 —
    /// 흔들림은 내 화면에만 준다.
    void OnTookHit(Vector3 direction, bool spilled)
    {
        if (look != null) look.FlashClient(spilled ? spillFlash : hitFlash, flashSeconds);
        if (IsMine) Shake(spilled ? shakeOnSpill : shakeOnTaken);
    }

    void SpawnImpact(Vector3 at, bool spilled)
    {
        if (impactPrefab == null) return;

        var fx = Instantiate(impactPrefab, at, Quaternion.identity);
        var main = fx.main;
        main.startColor = spilled ? spillFlash : hitFlash;
        fx.Play();
        Destroy(fx.gameObject, impactLifetime);
    }

    void Shake(float amount)
    {
        if (impulse != null) impulse.GenerateImpulseWithForce(amount);
    }
}
