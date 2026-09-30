using UnityEngine;

/// 밤과 낮에 하늘을 바꾼다. 페이즈는 이미 복제되므로 각 클라이언트가 자기 화면에서 바꾼다.
/// 전환 페이즈에는 건드리지 않아 직전 하늘(밤)이 그대로 남는다.
public class PhaseSkybox : MonoBehaviour
{
    [SerializeField] Material nightSky;
    [SerializeField] Material daySky;

    GamePhase clock;

    void OnEnable() => MatchDirector.Bind(OnDirectorReady);

    void OnDisable()
    {
        MatchDirector.Unbind(OnDirectorReady);
        if (clock != null) clock.PhaseEntered -= OnPhaseEntered;
        clock = null;
    }

    /// 같은 인스턴스로 두 번 불릴 수 있다 (`MatchDirector.Bind` 계약).
    void OnDirectorReady(MatchDirector ready)
    {
        if (clock != null) clock.PhaseEntered -= OnPhaseEntered;

        clock = ready != null ? ready.Phase : null;
        if (clock == null) return;

        clock.PhaseEntered += OnPhaseEntered;
        // 낮 도중에 붙은 클라이언트도 지금 하늘을 봐야 한다.
        OnPhaseEntered(clock.Current);
    }

    void OnPhaseEntered(Phase phase)
    {
        var sky = phase switch
        {
            Phase.Night => nightSky,
            Phase.Day => daySky,
            _ => null,
        };
        if (sky == null || RenderSettings.skybox == sky) return;

        RenderSettings.skybox = sky;
        // 환경광·기본 반사가 하늘을 쓰는 설정이면 새 하늘로 다시 계산한다. 페이즈당 한 번뿐이다.
        DynamicGI.UpdateEnvironment();
    }

    void OnValidate()
    {
        if (nightSky == null || daySky == null)
            CDebug.LogWarning($"{name}: 밤·낮 하늘 머티리얼을 둘 다 넣어야 한다.", this);
    }
}
