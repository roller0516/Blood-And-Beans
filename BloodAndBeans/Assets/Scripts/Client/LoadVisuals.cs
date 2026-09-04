using Unity.Cinemachine;
using UnityEngine;

/// 적재를 몸과 감각으로 드러낸다 (기획서 6.6 · 6.7).
///
/// 셋을 함께 맡는 이유는 셋 다 같은 값 하나(적재 비율)에서 나오고, 갈라 두면 같은 값을
/// 세 컴포넌트가 각자 구독하게 되기 때문이다. `DashVisuals`가 잔상·번쩍임·흔들림을 함께
/// 맡는 것과 같은 단위다.
///
/// | 무엇 | 누가 보는가 | 기획서 |
/// |---|---|---|
/// | 배낭이 부푼다 | **전원** — 견제 판단의 근거다 | 6.6 |
/// | 구간이 바뀔 때 발소리가 바뀐다 | 소유자 | 6.7 |
/// | 100%를 넘으면 화면이 흔들린다 | 소유자 | 6.7 |
///
/// 부푼 모습만 전원에게 보이는 것은 복제 범위가 그렇기 때문이다 — 무게도 내용물도
/// 소유자만 읽고, 공개되는 것은 `PlayerInventory.Overloaded` 한 비트뿐이다.
[RequireComponent(typeof(PlayerInventory))]
public class LoadVisuals : MonoBehaviour
{
    [Header("겉보기 (기획서 6.6)")]
    /// 부풀릴 대상. 비워 두면 첫 자식 렌더러의 transform을 쓴다.
    [SerializeField] Transform bulgeTarget;

    /// 적재 80%를 넘겼을 때 옆으로 부푸는 배율. 위로는 늘리지 않는다 — 키가 커지면
    /// 짐을 진 것이 아니라 다른 캐릭터로 보인다.
    [SerializeField] Vector3 bulgeScale = new(1.25f, 1f, 1.25f);

    /// 부풀고 꺼지는 데 걸리는 시간.
    [SerializeField] float bulgeSeconds = 0.25f;

    /// 짐이 삐져나온 모습. 프리팹에 이어 두면 80%를 넘길 때 켜진다 (선택).
    [SerializeField] GameObject overloadBadge;

    [Header("발소리 (기획서 6.7)")]
    /// 발소리를 내보낼 곳. 비워 두면 소리는 생략하고 나머지 연출만 돈다.
    [SerializeField] AudioSource footsteps;

    /// 무게 구간별 발소리. `LoadBands`의 밴드 인덱스로 고른다. 빈 칸은 건너뛴다.
    /// ponytail: 오디오 애셋이 아직 `sfx_bell` 하나뿐이라 전부 비어 있다. 클립이 생기면
    /// 프리팹에서 꽂는다 — 코드는 이미 밴드별로 고른다.
    [SerializeField] AudioClip[] stepByBand = new AudioClip[LoadBands.Count];

    /// 한 걸음 사이의 시간. 무거울수록 느려진다.
    [SerializeField] float stepSeconds = 0.42f;
    [SerializeField] float heavyStepSeconds = 0.72f;

    [Header("화면 흔들림 (기획서 6.7)")]
    /// 흔들림을 쏘는 곳. 비워 두면 같은 오브젝트에서 찾는다 (`DashVisuals`와 같은 소스).
    [SerializeField] CinemachineImpulseSource impulse;

    /// 100%를 넘긴 뒤 한 걸음마다 주는 흔들림. 대시 피격(0.28)보다 훨씬 약해야 한다 —
    /// 걷는 내내 계속되는 것이라 같은 세기면 화면을 볼 수 없다.
    [SerializeField] float shakePerStep = 0.05f;

    PlayerInventory inventory;
    Unity.Netcode.NetworkObject netObject;

    Vector3 baseScale = Vector3.one;

    /// 지금 다가가고 있는 크기. `Refresh`가 정하고 `Update`가 메운다.
    Vector3 targetScale = Vector3.one;

    float nextStep;
    Vector3 lastPosition;

    /// 지금 그려 둔 상태. 매 프레임 트윈을 새로 걸지 않기 위한 값이다.
    bool bulged;

    void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
        netObject = GetComponent<Unity.Netcode.NetworkObject>();

        // 이을 곳이 비어 있으면 여기서 한 번만 찾는다. 주기 실행 안에서는 찾지 않는다
        // (AGENTS.md 참조와 결합도).
        if (bulgeTarget == null)
        {
            var renderer = GetComponentInChildren<Renderer>();
            if (renderer != null) bulgeTarget = renderer.transform;
        }
        if (impulse == null) impulse = GetComponentInChildren<CinemachineImpulseSource>();

        if (bulgeTarget != null) baseScale = bulgeTarget.localScale;
        targetScale = baseScale;
        if (overloadBadge != null) overloadBadge.SetActive(false);

        lastPosition = transform.position;
    }

    void OnEnable()
    {
        inventory.LoadChanged += Refresh;
        Refresh();
    }

    void OnDisable() => inventory.LoadChanged -= Refresh;

    /// 내 화면인가. 발소리와 흔들림은 이 값이 참일 때만 준다 — 남이 무거운 것으로 내
    /// 화면이 흔들리면 무엇 때문에 흔들리는지 알 수 없다.
    bool IsMine => netObject != null && netObject.IsLocalPlayer;

    /// 적재 80%를 넘긴 모습. 전원에게 보인다 (기획서 6.6).
    void Refresh()
    {
        var want = inventory.Overloaded;
        if (want == bulged) return;
        bulged = want;

        if (overloadBadge != null) overloadBadge.SetActive(want);
        if (bulgeTarget == null) return;

        // DOTween을 쓰지 않는다. 이 트윈은 한 값만 왕복하고 다른 연출과 겹치지 않아
        // 시퀀스가 필요 없다 — `Update`의 한 줄 보간으로 끝난다.
        targetScale = want ? Vector3.Scale(baseScale, bulgeScale) : baseScale;
    }

    void Update()
    {
        StepBulge();

        if (!IsMine) return;
        StepFootsteps();
    }

    void StepBulge()
    {
        if (bulgeTarget == null) return;

        if (bulgeSeconds <= 0f)
        {
            bulgeTarget.localScale = targetScale;
            return;
        }

        bulgeTarget.localScale = Vector3.MoveTowards(
            bulgeTarget.localScale, targetScale,
            (baseScale.magnitude / bulgeSeconds) * Time.deltaTime);
    }

    /// 구간이 바뀌면 발소리가 바뀌고, 100%를 넘으면 걸음마다 화면이 흔들린다 (기획서 6.7).
    ///
    /// 걸음은 시간이 아니라 **실제로 움직였는가**로 센다. 서 있는 동안 발소리가 나면
    /// 무게가 아니라 시계를 듣는 것이 된다.
    void StepFootsteps()
    {
        var moved = (transform.position - lastPosition).sqrMagnitude > 0.0004f;
        lastPosition = transform.position;

        if (!moved || !inventory.HasBag) return;

        var ratio = inventory.LoadRatio;
        var band = LoadBands.BandOf(ratio);

        // 무거울수록 걸음이 느려진다. 속도 배수를 그대로 쓰면 200% 구간에서 걸음 간격이
        // 100배가 되어 사실상 소리가 멎는다.
        var interval = Mathf.Lerp(stepSeconds, heavyStepSeconds,
            band / Mathf.Max(1f, LoadBands.Count - 1f));

        if (Time.time < nextStep) return;
        nextStep = Time.time + interval;

        if (footsteps != null && band < stepByBand.Length && stepByBand[band] != null)
            footsteps.PlayOneShot(stepByBand[band]);

        // 100%를 넘긴 동안에만 흔든다.
        if (impulse != null && ratio >= LoadBands.ShakeRatio)
            impulse.GenerateImpulseWithForce(shakePerStep);
    }
}
