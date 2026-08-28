using DG.Tweening;
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
    Tween flash;

    void Awake() => playerTeam = GetComponent<PlayerTeam>();

    public override void OnNetworkSpawn()
    {
        playerTeam.TeamChanged += Apply;
        Apply(playerTeam.Team);
    }

    public override void OnNetworkDespawn()
    {
        flash?.Kill();
        flash = null;
        if (playerTeam != null) playerTeam.TeamChanged -= Apply;
    }

    /// 잠깐 다른 색으로 물들였다가 팀 색으로 되돌린다. 대시에 맞은 순간을 알리는 데 쓴다.
    ///
    /// 연출을 시키는 것은 `DashVisuals`지만 이 메서드는 여기 있다. 되돌릴 색을 아는 것은
    /// 팀 색을 소유한 이쪽뿐이고, 밖에서 되돌리게 하면 팀 배정이 바뀌는 순간 어긋난다.
    public void FlashClient(Color color, float seconds)
    {
        flash?.Kill();

        var team = TeamColors.Of(playerTeam.Team);
        flash = DOVirtual.Color(color, team, Mathf.Max(0.01f, seconds),
                                c => TeamColors.TintWith(gameObject, c, tintStrength))
                         .SetLink(gameObject)
                         .OnKill(() => Apply(playerTeam.Team));
    }

    void Apply(int team) => TeamColors.Tint(gameObject, team, tintStrength);
}
