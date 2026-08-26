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

        if (hasAuthorityRotation) transform.rotation = authorityRotation;
    }

    protected override void OnNetworkTransformStateUpdated(
        ref NetworkTransformState oldState, ref NetworkTransformState newState)
    {
        base.OnNetworkTransformStateUpdated(ref oldState, ref newState);

        if (!OwnerPredicts) return;

        // 바뀐 항목만 값이 들어 있다. HasPositionChange가 false면 GetPosition()은 0을 준다.
        if (newState.HasRotAngleChange)
        {
            authorityRotation = newState.GetRotation();
            hasAuthorityRotation = true;
        }

        if (newState.HasPositionChange)
            prediction.ReconcileClient(newState.GetNetworkTick(), newState.GetPosition());
    }
}
