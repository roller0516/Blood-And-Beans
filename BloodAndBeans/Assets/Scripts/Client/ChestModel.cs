using UnityEngine;

/// 상자 한 등급의 뚜껑 모델. 모델 루트에 붙고 자기 애니메이터를 가진다 (`FacilityModel`과 같은 자리).
/// 열림은 Bool 하나로 정하고 Open → 유지 → Close 흐름은 컨트롤러가 정한다.
[DisallowMultipleComponent]
public sealed class ChestModel : MonoBehaviour
{
    public const string OpenParameter = "Open";
    public const string ClosedStateName = "Closed";
    static readonly int Open = Animator.StringToHash(OpenParameter);
    static readonly int ClosedState = Animator.StringToHash(ClosedStateName);

    [SerializeField] Animator animator;

    /// 본체와 뚜껑. 안개 게이트와 등급 머티리얼이 한꺼번에 걸린다.
    [SerializeField] Renderer[] renderers;

    void Awake()
    {
        if (animator == null)
            CDebug.LogError($"{name}: Animator가 연결되지 않았다. 상자 뚜껑이 움직이지 않는다.", this);
    }

    /// 닫힌 상태로 돌아와 가만히 있는가. 꺼져 있으면 움직일 것이 없으니 가라앉은 것으로 본다.
    public bool Settled => !isActiveAndEnabled || animator == null ||
        (!animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).shortNameHash == ClosedState);

    public void SetOpen(bool open)
    {
        if (animator != null) animator.SetBool(Open, open);
    }

    public void SetVisible(bool visible)
    {
        foreach (var r in renderers) r.enabled = visible;
    }

    public void SetMaterial(Material material)
    {
        foreach (var r in renderers) r.sharedMaterial = material;
    }
}
