using Unity.Netcode;
using UnityEngine;

/// 캐릭터를 팀 색으로 물들인다. 밤에는 두 팀이 같은 숲에 서고 서로를 방해할 수 있으므로
/// (기획서 6.6), 누가 아군인지는 표시가 아니라 게임플레이 정보다.
///
/// PlayerTeam과 나눈 것은 의도다. 자리 배정은 서버 권위이고 색은 보는 쪽 일이라
/// 바뀌는 이유가 다르다 — CustomerLook이 Customer와 나뉜 것과 같다.
[RequireComponent(typeof(PlayerTeam))]
public class PlayerLook : NetworkBehaviour
{
    /// 캐릭터는 팀을 한눈에 알아봐야 하므로 카페와 달리 그대로 물들인다.
    [SerializeField, Range(0f, 1f)] float tintStrength = 1f;

    PlayerTeam playerTeam;

    void Awake() => playerTeam = GetComponent<PlayerTeam>();

    public override void OnNetworkSpawn()
    {
        playerTeam.TeamChanged += Apply;
        Apply(playerTeam.Team);
    }

    public override void OnNetworkDespawn()
    {
        if (playerTeam != null) playerTeam.TeamChanged -= Apply;
    }

    void Apply(int team) => TeamColors.Tint(gameObject, team, tintStrength);
}
