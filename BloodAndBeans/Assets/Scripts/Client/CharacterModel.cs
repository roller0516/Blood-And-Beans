using UnityEngine;

/// 모델에서 팀 색을 입힐 부분을 지정하고, 모델의 애니메이션을 재생한다. 얼굴·눈·텍스처와 플레이어 소품은 보존한다.
[DisallowMultipleComponent]
public sealed class CharacterModel : MonoBehaviour
{
    // AC_Ghost.controller의 파라미터·상태 이름. 컨트롤러를 고치면 여기도 같이 고친다.
    static readonly int MoveXHash = Animator.StringToHash("MoveX");
    static readonly int MoveZHash = Animator.StringToHash("MoveZ");
    static readonly int DashStartHash = Animator.StringToHash("DashStart");
    static readonly int DashHoldHash = Animator.StringToHash("DashHold");
    static readonly int DashEndHash = Animator.StringToHash("DashEnd");
    static readonly int HitHash = Animator.StringToHash("Hit");

    [Tooltip("팀 색을 입힐 의상이나 장식의 루트. 원래 재질을 유지할 부위는 넣지 않는다.")]
    [SerializeField] Transform[] teamTintRoots = System.Array.Empty<Transform>();

    [Header("애니메이션")]
    [SerializeField] Animator animator;
    [Tooltip("이 속도(m/s)에서 이동 블렌드가 끝까지 간다. PlayerMove.speed와 맞춘다.")]
    [SerializeField, Min(0.01f)] float fullBlendSpeed = 5f;
    [Tooltip("이동 블렌드 값이 따라가는 시간. 원격 플레이어의 보간 떨림을 흡수한다.")]
    [SerializeField, Min(0f)] float blendDampSeconds = 0.1f;
    [SerializeField, Min(0f)] float crossFadeSeconds = 0.08f;

    Vector3 lastPosition;

    void Awake()
    {
        if (animator == null)
        {
            CDebug.LogError($"{name}: Animator가 연결되지 않았다. 애니메이션이 재생되지 않는다.", this);
            enabled = false;
        }
    }

    void OnEnable() => lastPosition = transform.position;

    /// 이동 블렌드는 매 프레임 자기 위치 변화로 정한다. 소유자 예측·원격 보간·선택창 무대가
    /// 모두 transform만 움직이므로 네트워크 값을 따로 읽지 않아도 된다.
    void LateUpdate()
    {
        var dt = Time.deltaTime;
        if (dt <= 0f) return;

        var position = transform.position;
        var velocity = (position - lastPosition) / dt;
        lastPosition = position;

        // 모델 루트는 임포트 방향 보정이 들어가 있어 부모(캐릭터 몸통) 기준으로 잰다.
        var basis = transform.parent != null ? transform.parent : transform;
        var local = basis.InverseTransformDirection(velocity) / fullBlendSpeed;
        local = Vector3.ClampMagnitude(new Vector3(local.x, 0f, local.z), 1f);

        animator.SetFloat(MoveXHash, local.x, blendDampSeconds, dt);
        animator.SetFloat(MoveZHash, local.z, blendDampSeconds, dt);
    }

    public void Tint(Color color, float strength)
    {
        foreach (var root in teamTintRoots)
            if (root != null) TeamColors.TintWith(root.gameObject, color, strength);
    }

    /// 준비 → 돌진 포즈 유지(루프)까지 간다. 돌진 길이는 서버가 정하므로 끝은 <see cref="EndDash"/>가 낸다.
    public void PlayDash() { if (enabled) animator.CrossFadeInFixedTime(DashStartHash, crossFadeSeconds); }

    /// 회복 동작을 재생하고 컨트롤러의 종료 전이로 이동 상태에 돌아간다.
    /// 돌진 중에 맞았으면 이미 Hit으로 넘어갔으므로 덮어쓰지 않는다.
    public void EndDash()
    {
        if (!enabled || !IsDashing()) return;
        animator.CrossFadeInFixedTime(DashEndHash, crossFadeSeconds);
    }

    bool IsDashing()
    {
        var state = animator.IsInTransition(0)
            ? animator.GetNextAnimatorStateInfo(0)
            : animator.GetCurrentAnimatorStateInfo(0);
        return state.shortNameHash == DashStartHash || state.shortNameHash == DashHoldHash;
    }

    public void PlayHit() { if (enabled) animator.CrossFadeInFixedTime(HitHash, crossFadeSeconds); }
}
