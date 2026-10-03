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
            CDebug.LogError($"{host.name}: MatchDirector.boxPrefab을 빌리지 못했다. 도깨비불이 아무것도 세우지 못한다.", host);
            return false;
        }

        // y는 진짜 숲 상자와 같은 프리팹 값이다 (`MatchDirector.ResizeBoxesServer`). 플레이어 위치는
        // 캡슐 중심이라 그대로 쓰면 뜨고, 중력이 없어 영영 내려오지 않는다.
        var at = host.transform.position;
        at.y = host.DecoyBox.transform.position.y;
        var box = Object.Instantiate(host.DecoyBox, at, Quaternion.identity);

        // 진짜 상자처럼 단단해서 서 있는 자리에 세우면 그 안에 갇힌다. 캡슐과 겹치지 않을 만큼 앞에 세운다.
        // ponytail: 앞의 벽·나무는 보지 않는다. 박히는 일이 잦으면 Physics.CheckBox로 자리를 거른다.
        var body = box.GetComponent<Collider>();
        if (body != null && host.Controller != null)
        {
            var half = body.bounds.extents;
            var forward = host.transform.forward;
            forward.y = 0f;
            var clear = host.Controller.radius + Mathf.Sqrt(half.x * half.x + half.z * half.z) + host.Controller.skinWidth;
            box.transform.position += forward.normalized * clear;
        }
        box.NetworkObject.Spawn();

        // 빈 목록을 심는다. `SeedServer`는 개수가 0인 칸을 버리므로 결과가 빈 상자다.
        box.SeedServer(System.Array.Empty<LootStack>());

        host.CueToTeamServer(EffectId.Wisp, box.transform.position);
        return true;
    }
}
