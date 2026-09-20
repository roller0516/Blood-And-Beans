using UnityEngine;

/// 환각 — 빈 가방을 묻는다 (기획서 9.2).
///
/// 적이 소각에 시간을 태우게 만드는 것이 목적이라 내용이 없다.
public sealed class IllusionAbility : INightAbility
{
    public NightSkill Id => NightSkill.Illusion;

    public bool TryCastServer(PlayerAbilities host)
    {
        if (host.DecoyBag == null)
        {
            CDebug.LogError($"{host.name}: decoyBagPrefab이 비어 있다. 환각이 아무것도 심지 못한다.", host);
            return false;
        }

        var bag = Object.Instantiate(host.DecoyBag, host.transform.position, Quaternion.identity);

        // 팀을 먼저 심고 스폰한다 (`PlayerInventory.BuryRpc`와 같은 이유 — 스폰 뒤에 쓰면
        // 적 클라이언트가 팀 미상 상태의 가방을 한 틱 동안 그대로 렌더한다).
        bag.SeedServer(host.TeamId, null);
        bag.NetworkObject.SpawnWithObservers = false;   // 보여주는 시점은 BuriedBag이 정한다
        bag.NetworkObject.Spawn();
        return true;
    }
}
