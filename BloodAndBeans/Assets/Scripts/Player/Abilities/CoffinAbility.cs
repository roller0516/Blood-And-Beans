using UnityEngine;

/// 관 떨어뜨리기 — 지정한 자리에 관이 떨어져 벽이 된다 (기획서 9.1.2). 우리 팀도 막는다.
public sealed class CoffinAbility : IDayAbility
{
    public DaySkill Id => DaySkill.CoffinDrop;

    public bool TryCastServer(PlayerAbilities host)
    {
        if (host.CoffinPrefab == null)
        {
            CDebug.LogError($"{host.name}: PlayerAbilities.coffinPrefab이 비었다. 관이 떨어지지 않는다.", host);
            return false;
        }
        if (!host.TryGroundAhead(DaySkills.CoffinRange, out var at)) return false;

        // 긴 변이 길을 가로지르게 눕힌다.
        var facing = host.Move != null ? host.Move.FacingServer : host.transform.forward;
        var coffin = Object.Instantiate(host.CoffinPrefab, at, Quaternion.LookRotation(facing, Vector3.up));
        var scale = coffin.transform.localScale;
        coffin.transform.localScale = new Vector3(DaySkills.CoffinWidth, scale.y, DaySkills.CoffinDepth);
        coffin.NetworkObject.Spawn();
        coffin.ShoveOccupantsServer();

        host.CueServer(EffectId.CoffinImpact, at);
        return true;
    }
}
