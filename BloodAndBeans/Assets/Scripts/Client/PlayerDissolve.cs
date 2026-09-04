using DG.Tweening;
using UnityEngine;

/// 밤이 시작될 때 캐릭터가 숲에 나타나는 연출. 서버는 밤 진입마다 플레이어를 숲 가장자리로
/// 순간이동시키므로(`PlayerTeam.MoveToPhaseStartServer`) 화면에서는 캐릭터가 아무 예고 없이
/// 튀어나온다. 카메라가 그 자리에 미리 잘라 붙는 지금(`MatchCameraDirector`) 그 튐이 더
/// 눈에 띄어서, 아무것도 없는 상태에서 디졸브로 차오르게 한다.
///
/// 무늬와 경계 발광은 셰이더가 만든다 (`Art/Shaders/PlayerDissolve.shadergraph`). 여기서는
/// 진행도 하나만 0에서 1로 민다. 노이즈를 CPU에서 구워 베이스맵에 밀어 넣던 방식을 버린
/// 이유는 세 가지다 — 텍스처 한 장이 통째로 사라지고, 원래 베이스맵을 덮지 않아도 되고,
/// 잘리는 경계에 발광을 얹는 것이 셰이더 쪽에서는 노드 세 개면 끝나기 때문이다.
public class PlayerDissolve : MonoBehaviour
{
    /// 디졸브할 몸통. 비어 있으면 아무것도 하지 않는다 — 잔상(TrailRenderer)까지 같이
    /// 깎이면 대시 연출이 사라지므로 계층 전체를 훑지 않고 하나만 받는다.
    [SerializeField] Renderer body;

    /// 디졸브 셰이더를 쓰는 머티리얼 원본. 무늬·경계 두께·발광 색은 전부 이 애셋에서 만진다.
    [SerializeField] Material dissolveMaterial;

    /// 다 나타나기까지의 시간.
    [SerializeField] float seconds = 0.9f;

    static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    GamePhase clock;
    Material original;
    Material dissolving;
    Tween tween;

    void OnEnable() => MatchDirector.Bind(OnDirectorReady);

    void OnDisable()
    {
        MatchDirector.Unbind(OnDirectorReady);
        if (clock != null) clock.PhaseEntered -= OnPhaseEntered;
        clock = null;

        tween?.Kill();
        tween = null;
        Restore();
        if (dissolving != null) Destroy(dissolving);
        dissolving = null;
    }

    /// 같은 인스턴스로 두 번 불릴 수 있다 (`MatchDirector.Bind` 계약).
    void OnDirectorReady(MatchDirector ready)
    {
        if (clock != null) clock.PhaseEntered -= OnPhaseEntered;

        clock = ready != null ? ready.Phase : null;
        if (clock == null) return;

        clock.PhaseEntered += OnPhaseEntered;

        // 밤이 이미 시작된 뒤에 붙은 클라이언트도 한 번은 나타나야 한다. 페이즈 이벤트만
        // 기다리면 그 사람에게는 다음 밤까지 연출이 없다.
        OnPhaseEntered(clock.Current);
    }

    void OnPhaseEntered(Phase phase)
    {
        if (phase == Phase.Night) Play();
    }

    void Play()
    {
        if (body == null) return;
        if (dissolveMaterial == null)
        {
            CDebug.LogError($"{name}: 디졸브 머티리얼이 비어 있다. 연출 없이 그냥 나타난다.", this);
            return;
        }

        tween?.Kill();
        if (dissolving == null)
        {
            // 머티리얼 애셋을 그대로 쓰면 판에 있는 모든 플레이어가 같은 진행도를 공유해
            // 한꺼번에 나타난다. 사본에만 쓴다.
            original = body.sharedMaterial;
            dissolving = new Material(dissolveMaterial)
            {
                name = dissolveMaterial.name + " (Instance)",
            };

            // ponytail: 몸통 색만 옮긴다. 지금 몸통은 URP 기본 Lit 머티리얼이라 베이스맵이
            // 없다. 캐릭터에 텍스처가 붙으면 그래프에 베이스맵 입력을 열고 여기서 같이 옮긴다.
            if (original != null && original.HasProperty(BaseColorId))
                dissolving.SetColor(BaseColorId, original.GetColor(BaseColorId));
        }

        // 0이면 전부 잘려 나가 아무것도 보이지 않는다. 여기서 시작해 1까지 올린다.
        dissolving.SetFloat(DissolveId, 0f);
        body.sharedMaterial = dissolving;

        tween = DOVirtual.Float(0f, 1f, Mathf.Max(0.01f, seconds),
                                v => dissolving.SetFloat(DissolveId, v))
                         .SetLink(gameObject)
                         .OnComplete(Restore);
    }

    /// 연출이 끝나면 원본 머티리얼로 돌린다. 알파 클리핑을 켠 사본을 계속 쓰면 판이 도는
    /// 내내 쓸데없는 클립 검사가 남고, SRP Batcher도 원본과 갈라진다.
    void Restore()
    {
        if (body != null && original != null) body.sharedMaterial = original;
    }
}
