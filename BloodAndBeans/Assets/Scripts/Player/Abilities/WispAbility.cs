using UnityEngine;

/// 도깨비불 — 가짜 상자를 세운다 (기획서 9.2).
///
/// 내용이 비어 있는 임시 상자라, 여는 데 든 시간이 그대로 상대의 손해가 된다.
/// **설치 연출은 설치 팀에게만 보낸다** — 전원에게 보내면 그 상자가 가짜라는 것을
/// 상대에게 알려 주는 꼴이 된다.
public sealed class WispAbility : INightAbility
{
    public NightSkill Id => NightSkill.WillOWisp;

    public bool TryCastServer(PlayerAbilities host)
    {
        if (host.DecoyBox == null)
        {
            CDebug.LogError($"{host.name}: decoyBoxPrefab이 비어 있다. 도깨비불이 아무것도 세우지 못한다.", host);
            return false;
        }

        var box = Object.Instantiate(host.DecoyBox, host.transform.position, Quaternion.identity);
        box.NetworkObject.Spawn();

        // 빈 목록을 심는다. `SeedServer`는 개수가 0인 칸을 버리므로 결과가 빈 상자다.
        box.SeedServer(System.Array.Empty<LootStack>());

        host.CueToTeamServer(EffectId.Wisp, box.transform.position);
        return true;
    }
}
