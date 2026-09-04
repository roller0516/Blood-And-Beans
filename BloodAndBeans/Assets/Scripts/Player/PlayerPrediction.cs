using Unity.Netcode;
using UnityEngine;

/// 소유자 클라이언트의 예측 화해. 서버가 보낸 권위 위치를 "같은 틱에 내가 예측했던 위치"와
/// 비교해 그 차이만큼 되돌린다.
///
/// 왜 시각이 아니라 틱으로 짝을 맞추나: NGO의 LocalTime은 ServerTime보다 왕복 지연만큼
/// 앞서 있다(NetworkTimeSystem.LocalTime). 소유자가 LocalTime 틱 T에 넣은 입력은 서버 틱
/// T쯤에 처리되므로, "내가 T에 예측한 위치"와 "서버의 T 상태"가 같은 사건을 가리킨다.
/// 현재 위치와 방금 도착한 서버 위치를 그냥 빼면 지연만큼의 거리가 항상 오차로 잡혀
/// 가만히 걷기만 해도 매 프레임 헛교정이 난다.
///
/// 권위는 서버에 그대로 있다. 예측은 화면을 앞당길 뿐이고 어떤 판정에도 쓰이지 않는다.
///
/// ponytail: 입력 재생(replay)이 없다. 서버 위치를 받아 차이를 부드럽게 메울 뿐이라
/// 지연이 크거나 벽·설비에 계속 부딪히는 상황에서는 교정이 눈에 보인다. 필요해지면
/// 입력에 틱을 붙여 서버가 틱 단위로 적용하고 소유자가 되감아 재생하는 방식으로 올린다.
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerMove))]
public class PlayerPrediction : NetworkBehaviour
{
    /// 이 값보다 작은 차이는 무시한다. 서버와 소유자의 프레임 간격이 달라 생기는 자잘한
    /// 오차까지 따라가면 가만히 서 있어도 떨린다.
    [SerializeField] float deadZone = 0.05f;

    /// 이 값을 넘으면 메우지 않고 즉시 맞춘다. 순간이동(페이즈 시작, 귀환 구역)을 메우려
    /// 들면 맵을 가로질러 미끄러진다.
    [SerializeField] float snapDistance = 2f;

    /// 차이를 메우는 속도(1/초). 클수록 빨리 맞고 거칠다.
    [SerializeField] float correctionRate = 12f;

    /// 예측 이력을 남기는 시간. 왕복 지연보다 넉넉해야 서버 상태가 도착했을 때 같은 틱의
    /// 예측이 아직 남아 있다. 없으면 그 상태는 비교하지 않고 버린다.
    [SerializeField] float historySeconds = 1f;

    CharacterController controller;
    PredictionHistory history;
    Vector3 error;

    /// 소유자이면서 서버가 아닐 때만 예측한다. 호스트는 자기 캐릭터를 직접 움직이므로
    /// 예측할 것이 없다.
    public bool Predicting => IsSpawned && IsOwner && !IsServer;

    void Awake() => controller = GetComponent<CharacterController>();

    public override void OnNetworkSpawn()
    {
        if (!IsOwner || IsServer) return;

        history = new PredictionHistory(
            Mathf.CeilToInt(historySeconds * NetworkManager.NetworkConfig.TickRate));
    }

    /// PlayerMove가 예측 이동을 끝낸 뒤에 돌아야 이번 프레임의 결과가 이력에 남는다.
    /// 교정을 먼저 적용하고 기록한다. 그래야 다음 서버 상태와 비교할 때 이미 메운 만큼이
    /// 빠져 같은 차이를 두 번 세지 않는다.
    void LateUpdate()
    {
        if (!Predicting || history == null) return;

        ApplyCorrection();
        history.Record(NetworkManager.LocalTime.Tick, transform.position);
    }

    /// 권위 상태가 도착할 때마다 PlayerNetworkTransform이 부른다.
    public void ReconcileClient(int serverTick, Vector3 serverPosition)
    {
        if (!Predicting || history == null) return;

        // 그 틱의 예측이 이력에 없으면 비교 기준이 없다. 지연이 이력 길이를 넘었거나 막
        // 스폰·순간이동한 직후다.
        if (!history.TryGet(serverTick, out var predicted))
        {
            // 델타 교정의 기준이 없다. 그렇다고 버리면 안 된다 — NetworkTransform은 값이
            // 바뀔 때만 보내고, 예측 중인 소유자는 권위 상태를 적용하지 않으며
            // (PlayerNetworkTransform), 중력도 없어 스스로 돌아오지 않는다. 여기서 놓친
            // 순간이동 한 번이 그대로 영구 어긋남이 된다. 실제로 밤 진입 순간이동이 이
            // 경로로 사라져서, 클라이언트가 원점에 선 채 남고 카메라도 거기를 비췄다.
            //
            // 지연으로 설명되는 거리는 그대로 무시한다. 그 이상이면 권위 위치로 맞춘다 —
            // 기준이 없을 때 옳은 답은 델타가 아니라 서버가 말한 절대 위치다.
            var stray = serverPosition - transform.position;
            stray.y = 0f;
            if (stray.magnitude > snapDistance) SnapTo(serverPosition);
            return;
        }

        var diff = serverPosition - predicted;
        diff.y = 0f;                    // 평면 탑다운이라 y는 아무도 움직이지 않는다 (PlayerMove)

        // 순간이동은 절대 위치로 맞춘다. 여기서 델타(transform.position + diff)를 더하면
        // 현재 위치가 예측과 어긋나 있던 만큼이 그대로 남아, 페이즈 전환마다 오차가
        // 쌓인다 — 클라이언트가 서버와 다른 곳에 서서 안개도 걷히지 않은 검은 화면을 본다.
        if (diff.magnitude > snapDistance)
        {
            SnapTo(serverPosition);
            return;
        }

        error = diff.magnitude < deadZone ? Vector3.zero : diff;
    }

    void ApplyCorrection()
    {
        if (error.sqrMagnitude < 1e-8f) return;

        // 프레임레이트에 무관하게 같은 속도로 수렴한다. correctionRate * deltaTime을 그대로
        // 쓰면 60fps와 144fps에서 수렴 속도가 달라진다.
        var step = error * (1f - Mathf.Exp(-correctionRate * Time.deltaTime));

        // 대입이 아니라 Move다. 교정이 벽과 설비를 뚫고 지나가지 않는다.
        controller.Move(step);
        error -= step;
    }

    /// 메울 수 없는 차이는 즉시 맞춘다. CharacterController가 자기 내부 위치를 다시 써
    /// 넣으므로 잠깐 꺼야 대입이 남는다 (PlayerTeleport와 같은 이유).
    ///
    /// 이력은 통째로 버린다. 순간이동 전 궤적으로 남은 예측과 이후 서버 상태를 비교하면
    /// 맵을 가로지르는 가짜 오차가 나온다.
    void SnapTo(Vector3 position)
    {
        controller.enabled = false;
        transform.position = position;
        controller.enabled = true;

        error = Vector3.zero;
        history.Clear();
    }
}
