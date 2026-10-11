using UnityEngine;

/// 하울링 — 반경 안의 다른 팀은 반대쪽으로 도망치고, 같은 팀(자기 포함)은 빨라진다 (기획서 9.1.2).
public sealed class HowlAbility : IDayAbility
{
    public DaySkill Id => DaySkill.Howl;

    public bool TryCastServer(PlayerAbilities host)
    {
        var at = host.transform.position;
        foreach (var client in host.NetworkManager.ConnectedClientsList)
        {
            var player = client.PlayerObject;
            if (player == null || Vector3.Distance(player.transform.position, at) > DaySkills.HowlRadius) continue;
            var status = player.GetComponent<PlayerStatus>();
            if (status == null) continue;

            if (PlayerTeam.Of(client.ClientId) == host.TeamId) status.HasteServer();
            else status.FleeServer(at);
        }

        host.CueServer(EffectId.Howl, at);
        return true;
    }
}
