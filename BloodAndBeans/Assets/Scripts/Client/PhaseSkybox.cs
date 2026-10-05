using UnityEngine;

/// 밤과 낮에 하늘과 주 조명 강도를 바꾼다. 페이즈는 이미 복제되므로 각 클라이언트가 자기 화면에서 바꾼다.
/// 전환 페이즈에는 건드리지 않아 직전 상태(밤)가 그대로 남는다.
public class PhaseSkybox : MonoBehaviour
{
    [SerializeField] Material nightSky;
    [SerializeField] Material daySky;
    [SerializeField] Light sun;
    [SerializeField] float nightIntensity = 0.5f;
    [SerializeField] float dayIntensity = 3f;

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
        if (phase != Phase.Night && phase != Phase.Day) return;
        bool night = phase == Phase.Night;

        sun.intensity = night ? nightIntensity : dayIntensity;

        var sky = night ? nightSky : daySky;
        if (sky == null || RenderSettings.skybox == sky) return;

        RenderSettings.skybox = sky;
        // 환경광·기본 반사가 하늘을 쓰는 설정이면 새 하늘로 다시 계산한다. 페이즈당 한 번뿐이다.
        DynamicGI.UpdateEnvironment();
    }

    void OnValidate()
    {
        if (sun == null)
            CDebug.LogWarning($"{name}: 주 조명(Light)을 넣어야 한다.", this);
        // 하늘은 씬 기본 하늘을 쓸 거면 둘 다 비워 둔다. 하나만 넣으면 한쪽 페이즈만 바뀐다.
        if ((nightSky == null) != (daySky == null))
            CDebug.LogWarning($"{name}: 밤·낮 하늘 머티리얼은 둘 다 넣거나 둘 다 비워야 한다.", this);
    }
}
