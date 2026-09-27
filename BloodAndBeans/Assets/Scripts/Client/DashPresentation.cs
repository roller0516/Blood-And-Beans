using DG.Tweening;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

/// 대시의 연출만 맡는 일반 C# 객체. 판정·이동·쿨다운은 서버의 `DashHarass`에 있고, 여기서는
/// 그쪽이 던지는 이벤트를 받아 그리기만 한다. `MonoBehaviour`가 아니라서 이벤트 구독/해제,
/// 재발동 시 이전 종료 예약 취소, 비활성·디스폰 정리는 전부 `PlayerVisuals`가 시킨다 —
/// 대시는 밤에 존재하는 유일한 공격 행동이라 연출과 판정을 한 클래스에 섞는 사고가 곧
/// 밸런스 사고다.
public sealed class DashPresentation
{
    readonly TrailRenderer trail;
    readonly PlayerVisuals look;
    readonly CinemachineImpulseSource impulse;
    readonly NetworkObject netObject;

    readonly Color hitFlash;
    readonly Color spillFlash;
    readonly float flashSeconds;
    readonly float impactScale;
    readonly float shakeOnLand;
    readonly float shakeOnTaken;
    readonly float shakeOnSpill;

    /// 예약된 대시 종료. `Cancel`이 재발동·비활성·파괴·디스폰마다 끊는다 — 안 끊으면 이전
    /// 재생의 콜백이 새 대시나 재사용된 컴포넌트의 잔상·포즈를 뒤늦게 건드린다.
    Tween endTween;

    public DashPresentation(TrailRenderer trail, PlayerVisuals look, CinemachineImpulseSource impulse,
        NetworkObject netObject, Color hitFlash, Color spillFlash, float flashSeconds, float impactScale,
        float shakeOnLand, float shakeOnTaken, float shakeOnSpill)
    {
        this.trail = trail;
        this.look = look;
        this.impulse = impulse;
        this.netObject = netObject;
        this.hitFlash = hitFlash;
        this.spillFlash = spillFlash;
        this.flashSeconds = flashSeconds;
        this.impactScale = impactScale;
        this.shakeOnLand = shakeOnLand;
        this.shakeOnTaken = shakeOnTaken;
        this.shakeOnSpill = shakeOnSpill;

        if (trail != null) trail.emitting = false;
    }

    /// 내 화면인가. 흔들림은 이 값이 참일 때만 준다 — 남이 맞은 것으로 내 화면이 흔들리면
    /// 무엇에 맞았는지 알 수 없다.
    bool IsMine => netObject != null && netObject.IsLocalPlayer;

    public void OnDashStarted(float seconds)
    {
        if (look != null && look.Model != null) look.Model.PlayDash();

        if (trail != null)
        {
            // 지난 잔상이 남아 있으면 새 돌진이 이전 자리에서 시작한 것처럼 보인다.
            trail.Clear();
            trail.emitting = true;
        }

        // 재발동이면 이전 종료 예약이 아직 살아 있다. 안 끊으면 그 콜백이 이번 돌진의
        // 잔상·포즈를 도중에 꺼 버린다.
        endTween?.Kill();
        endTween = DOVirtual.DelayedCall(seconds, OnDashEnded);
    }

    /// 서버의 돌진 시간이 끝났다. 잔상을 끄고 돌진 포즈를 회복 동작으로 넘긴다.
    void OnDashEnded()
    {
        endTween = null;
        if (trail != null) trail.emitting = false;
        if (look != null && look.Model != null) look.Model.EndDash();
    }

    /// 내가 맞혔다. 임팩트는 맞은 자리에 남기고, 흔들림은 내 화면에만 준다.
    public void OnHitLanded(Vector3 at, bool spilled)
    {
        SpawnImpact(at, spilled);
        if (IsMine) Shake(shakeOnLand);
    }

    /// 내가 맞았다. 번쩍임은 모두에게 보이고 — 누가 맞았는지가 상대에게도 정보다 —
    /// 흔들림은 내 화면에만 준다.
    public void OnTookHit(Vector3 direction, bool spilled)
    {
        if (look != null)
        {
            look.FlashClient(spilled ? spillFlash : hitFlash, flashSeconds);
            if (look.Model != null) look.Model.PlayHit();
        }
        if (IsMine) Shake(spilled ? shakeOnSpill : shakeOnTaken);
    }

    /// 재료가 쏟아진 대시는 다른 연출로 터진다. 색을 코드에서 덮어쓰지 않는 이유는
    /// 그러면 같은 프리팹이 두 의미를 갖고, 풀에서 나온 인스턴스에 색이 남기 때문이다.
    void SpawnImpact(Vector3 at, bool spilled) =>
        EffectManager.Play(spilled ? EffectId.DashSpill : EffectId.DashHit, at, impactScale);

    void Shake(float amount)
    {
        if (impulse != null) impulse.GenerateImpulseWithForce(amount);
    }

    /// 재발동·비활성·파괴·디스폰에서 부른다. 예약된 종료를 끊고 잔상·포즈를 지금 정리한다.
    /// `EndDash`는 대시 상태가 아니면 스스로 아무것도 안 하므로 조건 없이 불러도 안전하다.
    public void Cancel()
    {
        endTween?.Kill();
        endTween = null;
        if (trail != null) trail.emitting = false;
        if (look != null && look.Model != null) look.Model.EndDash();
    }
}
