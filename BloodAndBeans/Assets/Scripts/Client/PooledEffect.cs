using UnityEngine;

/// 풀에서 나온 연출 하나. **다 터지면 스스로 돌아간다.**
///
/// `ParticleSystem`의 Stop Action을 `Callback`으로 두면 입자가 전부 사라진 프레임에
/// `OnParticleSystemStopped`가 온다. 회수 시점을 부르는 쪽이 타이머로 재지 않아도 되는
/// 이유가 이것이다 — 파티클 수명이 프리팹에만 있고 코드에는 없다.
///
/// `EffectManager`가 붙인다. 손으로 붙일 일은 없다.
[RequireComponent(typeof(ParticleSystem))]
public class PooledEffect : MonoBehaviour
{
    EffectManager owner;
    EffectId id;
    ParticleSystem effect;

    internal void Bind(EffectManager manager, EffectId effectId)
    {
        owner = manager;
        id = effectId;
        effect = GetComponent<ParticleSystem>();
    }

    void OnParticleSystemStopped()
    {
        if (owner != null) owner.Release(id, effect);
    }
}
