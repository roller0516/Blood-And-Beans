using Unity.Netcode.Components;
using UnityEngine;

/// 소유자 클라이언트의 위치를 NetworkTransform이 덮어쓰지 않게 막는다.
///
/// 서버 권위는 그대로다(AuthorityMode = Server). 다만 소유자는 자기 위치를 예측으로 먼저
/// 굴리므로(PlayerMove), 권위 상태를 매 프레임 그대로 적용해 버리면 예측이 즉시 지워져
/// 예측이 없는 것과 같아진다. 권위 위치는 PlayerPrediction으로 넘겨 화해에만 쓴다.
///
/// 회전은 계속 서버 값을 그대로 따른다. 넘어짐 연출(DashHarass)은 서버에서만 계산되고
/// 소유자는 회전을 전혀 예측하지 않기 때문이다.
public class PlayerNetworkTransform : NetworkTransform
{
    PlayerPrediction prediction;
    Quaternion authorityRotation = Quaternion.identity;
    bool hasAuthorityRotation;
    Vector3 authorityPosition;
    bool hasAuthorityPosition;
    bool adoptedAuthority;

    bool OwnerPredicts => prediction != null && prediction.Predicting;

    protected override void Awake()
    {
        base.Awake();
        prediction = GetComponent<PlayerPrediction>();
    }

    /// 예측 중인 소유자에게는 base를 호출하지 않는다. base가 하는 일은 보간 갱신과 권위
    /// 상태 적용뿐이라(NetworkTransform.OnUpdate), 건너뛰면 transform이 예측 것으로 남는다.
    public override void OnUpdate()
    {
        if (!OwnerPredicts)
        {
            base.OnUpdate();
            return;
        }

        // 예측이 켜지기 전에 온 위치(스폰 동기화, 스폰 직후 순간이동)는 적용되지 않은 채
        // 남는다. 늦게 붙은 손님이 원점에 서 있다가 첫 이동에서 수십 m 튀었다. 한 번 맞춘다.
        if (!adoptedAuthority && hasAuthorityPosition)
        {
            adoptedAuthority = true;
            prediction.AdoptAuthorityClient(authorityPosition);
        }

        if (hasAuthorityRotation) transform.rotation = authorityRotation;
    }

    protected override void OnNetworkTransformStateUpdated(
        ref NetworkTransformState oldState, ref NetworkTransformState newState)
    {
        base.OnNetworkTransformStateUpdated(ref oldState, ref newState);

        // 축 단위로 쌓는다. 델타는 임계값을 넘은 축만 싣고, 수신 버퍼의 안 온 축은 0으로
        // 남아 있다. GetPosition()을 통째로 쓰면 첫 이동에서 안 움직인 축이 0으로 잡혀
        // 화해가 원점 쪽으로 당기거나 y=0으로 스냅했다. 초기 동기화(전 축)도 여기로 온다.
        if (newState.HasPositionChange)
        {
            var p = newState.GetPosition();
            if (newState.HasPositionX) authorityPosition.x = p.x;
            if (newState.HasPositionY) authorityPosition.y = p.y;
            if (newState.HasPositionZ) authorityPosition.z = p.z;
            hasAuthorityPosition = true;
        }

        if (!OwnerPredicts) return;

        if (newState.HasRotAngleChange)
        {
            authorityRotation = newState.GetRotation();
            hasAuthorityRotation = true;
        }

        if (newState.HasPositionChange)
            prediction.ReconcileClient(newState.GetNetworkTick(), authorityPosition);
    }
}
