using UnityEngine;

/// 설비 모델의 애니메이션. 모델 루트에 붙고 자기 애니메이터를 가진다 (캐릭터의 `CharacterModel`과 같은 자리).
/// 작동이 켜지면 Input → Action 반복, 꺼지면 Complete다. 흐름은 컨트롤러가 정한다.
[DisallowMultipleComponent]
public sealed class FacilityModel : MonoBehaviour
{
    public const string WorkingParameter = "Working";
    static readonly int Working = Animator.StringToHash(WorkingParameter);

    [SerializeField] Animator animator;

    void Awake()
    {
        if (animator == null)
            CDebug.LogError($"{name}: Animator가 연결되지 않았다. 설비 애니메이션이 재생되지 않는다.", this);
    }

    public void SetWorking(bool working)
    {
        if (animator != null) animator.SetBool(Working, working);
    }
}
