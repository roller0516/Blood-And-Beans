using UnityEngine;

/// 이 플레이어의 액티브 연출을 그린다.
///
/// **캐릭터별로 갈리지 않는다.** 예전에는 `BatSkillVisuals`·`DokkaebiSkillVisuals`가 각각
/// 자기 캐릭터의 이벤트를 구독하고 프리팹을 들고 있어서, 캐릭터가 늘면 연출 클래스와
/// 프리팹 배선이 같이 늘었다. 지금은 서버가 `EffectId`만 보내고(`PlayerAbilities`),
/// 무엇을 그릴지는 `EffectManager`의 표가 정한다. 여기 남은 일은 **둘을 잇는 것**뿐이다.
[RequireComponent(typeof(PlayerAbilities))]
public class PlayerEffects : MonoBehaviour
{
    PlayerAbilities abilities;

    /// 지금 붙어 있는 지속 연출. 다시 걸릴 때 앞엣것을 끊는다.
    ParticleSystem attached;

    void Awake()
    {
        abilities = GetComponent<PlayerAbilities>();
    }

    void OnEnable()
    {
        abilities.EffectPlayed += OnEffect;
        abilities.AttachedChanged += OnAttached;
    }

    void OnDisable()
    {
        abilities.EffectPlayed -= OnEffect;
        abilities.AttachedChanged -= OnAttached;
        StopAttached();
    }

    void OnEffect(EffectId id, Vector3 position, float scale) => EffectManager.Play(id, position, scale);

    /// 지속 효과가 캐릭터에 붙는다 (활공 등). **어떤 능력인지는 모른다** — 라우터가
    /// 연출 id와 남은 시간만 준다.
    void OnAttached(EffectId id, float seconds)
    {
        StopAttached();
        if (id == EffectId.None || seconds <= 0f) return;

        attached = EffectManager.PlayAttached(id, transform, seconds);
    }

    void StopAttached()
    {
        if (attached == null) return;
        EffectManager.Stop(attached);
        attached = null;
    }
}
