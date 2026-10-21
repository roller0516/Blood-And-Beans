using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

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
    static readonly int HitStrongHash = Animator.StringToHash("HitStrong");

    // 일회성 몸짓 (18장). 상태 이름은 FBX 클립 이름을 따른다.
    static readonly int PickUpStartHash = Animator.StringToHash("PickUpStart");
    static readonly int PickUpHoldHash = Animator.StringToHash("PickUpHold");
    static readonly int PickUpEndHash = Animator.StringToHash("PickUpEnd");
    static readonly int CollectHash = Animator.StringToHash("Collect");
    static readonly int BuryHash = Animator.StringToHash("Bury");
    static readonly int ThrowAwayHash = Animator.StringToHash("ThrowAway");
    static readonly int KnotHash = Animator.StringToHash("Knot");
    static readonly int PerfectHash = Animator.StringToHash("Perfect");
    static readonly int MissHash = Animator.StringToHash("Miss");
    static readonly int HandOverHash = Animator.StringToHash("HandOver");
    static readonly int ReceiveHash = Animator.StringToHash("Receive");
    static readonly int CollapseHash = Animator.StringToHash("Collapse");
    static readonly int ReturnHash = Animator.StringToHash("Return");
    static readonly int ThrowHash = Animator.StringToHash("Throw");
    static readonly int CastingHash = Animator.StringToHash("Casting");

    // 「안 됨」을 걸으면서 하면 이동은 두고 상체만 젓는다 (DA-08).
    const string UpperLayerName = "UpperBody";
    static readonly int IdleNoHash = Animator.StringToHash("IdleNo");

    // 걸음걸이. 컨트롤러의 이동 상태(블렌드 트리) 이름이다.
    static readonly int RunHash = Animator.StringToHash("Run");
    static readonly int WalkHash = Animator.StringToHash("Walk");
    static readonly int MoveHeavyHash = Animator.StringToHash("MoveHeavy");
    static readonly int ZombieWalkHash = Animator.StringToHash("ZombieWalk");
    const string IdleClipName = "PC_rig|Idle";

    public enum Gait { Run, Walk, Heavy, Zombie }

    // 들기 자세는 팔 본만 덮는 마스크가 걸린 Hold 레이어에서 이동 위에 겹쳐 돈다.
    const string HoldLayerName = "Hold";
    static readonly int HoldNoneHash = Animator.StringToHash("HoldNone");
    static readonly int HoldHotCoffeeHash = Animator.StringToHash("HoldHotCoffee");
    static readonly int HoldIcedCoffeeHash = Animator.StringToHash("HoldIcedCoffee");
    static readonly int HoldPlateHash = Animator.StringToHash("HoldPlate");

    enum Hold { None, HotCoffee, IcedCoffee, Plate }

    [Tooltip("팀 색을 입힐 의상이나 장식의 루트. 원래 재질을 유지할 부위는 넣지 않는다.")]
    [SerializeField] Transform[] teamTintRoots = System.Array.Empty<Transform>();

    [Header("애니메이션")]
    [SerializeField] Animator animator;
    [Tooltip("이 속도(m/s)에서 이동 블렌드가 끝까지 간다. PlayerController.speed와 맞춘다.")]
    [SerializeField, Min(0.01f)] float fullBlendSpeed = 5f;
    [Tooltip("이동 블렌드 값이 따라가는 시간. 원격 플레이어의 보간 떨림을 흡수한다.")]
    [SerializeField, Min(0f)] float blendDampSeconds = 0.1f;
    [SerializeField, Min(0f)] float crossFadeSeconds = 0.08f;
    [Tooltip("이동 블렌드(0~1)가 이 값을 넘으면 움직이는 것으로 본다. 「안 됨」을 상체로 할지, 서서 하던 몸짓을 끊을지 가른다.")]
    [SerializeField, Range(0f, 1f)] float movingBlend = 0.2f;

    [Header("발걸음 먼지")]
    [SerializeField] Transform leftFoot;
    [SerializeField] Transform rightFoot;

    public event System.Action<Vector3> Footstep;
    readonly List<AnimatorClipInfo> footstepClips = new();

    [Header("손 소켓")]
    [Tooltip("잔을 쥐는 자리. 뜨거운·아이스 커피와 식기 없는 재료가 여기 붙는다.")]
    [SerializeField, FormerlySerializedAs("hotCoffeeSocket")] Transform coffeeSocket;
    [Tooltip("접시를 받쳐 드는 자리 (기획서 5.3.1).")]
    [SerializeField] Transform plateSocket;

    [Tooltip("등에 멘 가방. 묻으면 꺼진다 (기획서 6.7). 가방이 없는 모델이면 비워 둔다.")]
    [SerializeField] GameObject bag;

    static readonly int OpacityId = Shader.PropertyToID("_Opacity");

    Vector3 lastPosition;
    bool moving;
    int holdLayer = -1;
    int upperLayer = -1;
    int holdState;

    readonly List<(Renderer renderer, Material[] materials)> fadedLooks = new();
    Material fadeMaterial;
    Tween fade;

    void Awake()
    {
        if (animator == null)
        {
            CDebug.LogError($"{name}: Animator가 연결되지 않았다. 애니메이션이 재생되지 않는다.", this);
            enabled = false;
            return;
        }

        holdLayer = animator.GetLayerIndex(HoldLayerName);
        if (holdLayer < 0)
            CDebug.LogError($"{name}: 컨트롤러에 '{HoldLayerName}' 레이어가 없다. 들기 자세가 재생되지 않는다.", this);

        upperLayer = animator.GetLayerIndex(UpperLayerName);
        if (upperLayer < 0)
            CDebug.LogError($"{name}: 컨트롤러에 '{UpperLayerName}' 레이어가 없다. 걸으며 하는 「안 됨」이 재생되지 않는다.", this);
        if (leftFoot == null || rightFoot == null)
            CDebug.LogError($"{name}: 발걸음 먼지의 발 본이 연결되지 않았다.", this);
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
        moving = local.magnitude > movingBlend;

        // 일회성 동작에서 이동으로 돌아오면 컨트롤러는 기본 걸음(Run)으로 보낸다. 지금 걸음걸이와 다르면 맞춘다.
        // 서서 하는 몸짓은 걷기 시작하면 끊는다 — 몸이 미끄러지며 춤추지 않게.
        var state = BaseState();
        if (IsGait(state) ? state != gaitHash : moving && StopsWhenMoving(state))
            animator.CrossFadeInFixedTime(gaitHash, crossFadeSeconds);
    }

    /// Animator 오브젝트의 이벤트 수신기가 착지 프레임에 호출한다. 0은 왼발, 1은 오른발이다.
    public void EmitFootstep(AnimationEvent animationEvent)
    {
        if (!isActiveAndEnabled || !moving || Footstep == null) return;
        var transitioning = animator.IsInTransition(0);
        var state = transitioning ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        if (!IsGait(state.shortNameHash) || animationEvent.animatorStateInfo.fullPathHash != state.fullPathHash) return;

        // 방향 블렌드의 여러 클립이 동시에 발걸음을 보내도 가장 크게 보이는 이동 클립만 받는다.
        if (transitioning) animator.GetNextAnimatorClipInfo(0, footstepClips);
        else animator.GetCurrentAnimatorClipInfo(0, footstepClips);
        AnimationClip dominant = null;
        var weight = 0f;
        foreach (var info in footstepClips)
        {
            if (info.clip.name == IdleClipName || info.weight <= weight) continue;
            dominant = info.clip;
            weight = info.weight;
        }
        if (animationEvent.animatorClipInfo.clip != dominant) return;

        var foot = animationEvent.intParameter switch { 0 => leftFoot, 1 => rightFoot, _ => null };
        if (foot == null)
        {
            CDebug.LogError($"{name}: 발걸음 이벤트의 발 번호 또는 발 본 연결이 올바르지 않다.", this);
            return;
        }
        Footstep.Invoke(foot.position);
    }

    int gaitHash = RunHash;

    int BaseState() => animator.IsInTransition(0)
        ? animator.GetNextAnimatorStateInfo(0).shortNameHash
        : animator.GetCurrentAnimatorStateInfo(0).shortNameHash;

    static bool IsGait(int hash) => hash == RunHash || hash == WalkHash || hash == MoveHeavyHash || hash == ZombieWalkHash;

    static bool StopsWhenMoving(int hash) =>
        hash == PerfectHash || hash == MissHash || hash == HandOverHash || hash == ReceiveHash ||
        hash == KnotHash || hash == BuryHash || hash == CollectHash || hash == ThrowAwayHash;

    /// 확정된 몸짓을 재생한다 (18장). 서서 하는 몸짓은 걷기 시작하면 `LateUpdate`가 끊는다.
    public void PlayMotion(BodyMotion motion)
    {
        if (!enabled) return;

        if (motion == BodyMotion.Denied && moving)
        {
            if (upperLayer >= 0) animator.CrossFadeInFixedTime(IdleNoHash, crossFadeSeconds, upperLayer);
            return;
        }

        // 홀드 몸짓이 이미 다른 것(피격·회수 등)으로 덮였으면 이탈 동작을 끼워 넣지 않는다.
        var current = BaseState();
        if (motion == BodyMotion.HoldEnd && current != PickUpStartHash && current != PickUpHoldHash) return;

        var state = StateOf(motion);
        if (state != 0) animator.CrossFadeInFixedTime(state, crossFadeSeconds);
    }

    static int StateOf(BodyMotion motion) => motion switch
    {
        BodyMotion.HoldStart => PickUpStartHash,
        BodyMotion.HoldEnd => PickUpEndHash,
        BodyMotion.Collect => CollectHash,
        BodyMotion.Bury => BuryHash,
        BodyMotion.Dump => ThrowAwayHash,
        BodyMotion.Denied => KnotHash,
        BodyMotion.Perfect => PerfectHash,
        BodyMotion.Miss => MissHash,
        BodyMotion.HandOver => HandOverHash,
        BodyMotion.Receive => ReceiveHash,
        BodyMotion.Collapse => CollapseHash,
        BodyMotion.Return => ReturnHash,
        BodyMotion.Throw => ThrowHash,
        BodyMotion.Cast => CastingHash,
        _ => 0,
    };

    /// 걸음걸이를 바꾼다. 실제 전환은 다음 `LateUpdate`가 이동 상태일 때 한다.
    public void PlayGait(Gait gait) => gaitHash = gait switch
    {
        Gait.Walk => WalkHash,
        Gait.Heavy => MoveHeavyHash,
        Gait.Zombie => ZombieWalkHash,
        _ => RunHash,
    };

    /// 손에 든 것을 붙일 소켓. 접시면 접시 자리, 얼음이 든 완성품이면 아이스 잔 자리, 나머지는 잔 자리다.
    public void ShowBag(bool worn)
    {
        if (bag != null) bag.SetActive(worn);
    }

    public Transform HandSocketFor(CarryView view) => HoldFor(view) == Hold.Plate ? plateSocket : coffeeSocket;

    /// 든 것에 맞는 들기 자세로 바꾼다. 빈손이면 팔을 이동 애니메이션에 돌려준다.
    public void PlayHold(CarryView view)
    {
        var state = HoldFor(view) switch
        {
            Hold.Plate => HoldPlateHash,
            Hold.IcedCoffee => HoldIcedCoffeeHash,
            Hold.HotCoffee => HoldHotCoffeeHash,
            _ => HoldNoneHash,
        };
        if (!enabled || holdLayer < 0 || state == holdState) return;

        holdState = state;
        animator.CrossFadeInFixedTime(state, crossFadeSeconds, holdLayer);
    }

    static Hold HoldFor(CarryView view)
    {
        if (view.Empty) return Hold.None;
        if (view.DishIsPlate) return Hold.Plate;
        return view.IsProduct && IsIced(view.Menu) ? Hold.IcedCoffee : Hold.HotCoffee;
    }

    /// 온도는 얼음이 정한다 (기획서 7.1). 손에 든 것이 바뀔 때만 불린다.
    static bool IsIced(MenuId menu)
    {
        foreach (var m in Menus.All)
            if (m.Id == menu) return (Menus.TagsOf(m.Parts) & MenuTag.Cold) != 0;
        return false;
    }

    public void Tint(Color color, float strength)
    {
        // 사라지는 동안 팀 색 블록이 발광 셰이더의 `_BaseColor`(빛 색)를 덮지 않게 한다.
        if (fadeMaterial != null) return;
        foreach (var root in teamTintRoots)
            if (root != null) TeamColors.TintWith(root.gameObject, color, strength);
    }

    /// 순간이동 시전 동안 몸을 발광 머티리얼로 바꾸고 `_Opacity`를 1→0으로 내린다.
    /// 다 사라져도 그대로 둔다. 되돌리는 것은 도착 신호다 (<see cref="RestoreLook"/>).
    public void FadeOut(Material glow, float seconds)
    {
        RestoreLook();
        fadeMaterial = new Material(glow);
        var empty = new MaterialPropertyBlock();
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r is not (MeshRenderer or SkinnedMeshRenderer)) continue;
            var originals = r.sharedMaterials;
            fadedLooks.Add((r, originals));
            var glows = new Material[originals.Length];
            for (var i = 0; i < glows.Length; i++) glows[i] = fadeMaterial;
            r.sharedMaterials = glows;
            // 팀 색 블록을 비워야 발광 색이 보인다. 되돌린 뒤 팀 색은 부른 쪽이 다시 입힌다.
            for (var i = 0; i < glows.Length; i++) r.SetPropertyBlock(empty, i);
        }
        fade = DOVirtual.Float(1f, 0f, Mathf.Max(0.01f, seconds), a => fadeMaterial.SetFloat(OpacityId, a))
                        .SetEase(Ease.InQuad).SetLink(gameObject);
    }

    /// <see cref="FadeOut"/> 전의 머티리얼로 되돌린다.
    public void RestoreLook()
    {
        fade?.Kill();
        fade = null;
        foreach (var (r, materials) in fadedLooks)
            if (r != null) r.sharedMaterials = materials;
        fadedLooks.Clear();
        if (fadeMaterial != null) Destroy(fadeMaterial);
        fadeMaterial = null;
    }

    void OnDestroy()
    {
        if (fadeMaterial != null) Destroy(fadeMaterial);
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

    /// `strong`이면 밤에 대시를 맞은 강한 반응이다 (18장 DA-06 강).
    public void PlayHit(bool strong) { if (enabled) animator.CrossFadeInFixedTime(strong ? HitStrongHash : HitHash, crossFadeSeconds); }
}
