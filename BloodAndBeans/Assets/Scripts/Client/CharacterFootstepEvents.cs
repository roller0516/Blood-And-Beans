using UnityEngine;

/// Animation Event는 Animator와 같은 오브젝트에서 받으므로 모델 루트로 전달한다.
[DisallowMultipleComponent]
public sealed class CharacterFootstepEvents : MonoBehaviour
{
    [SerializeField] CharacterModel model;

    void Awake()
    {
        if (model != null) return;
        CDebug.LogError($"{name}: 발걸음 이벤트의 모델이 연결되지 않았다.", this);
        enabled = false;
    }

    public void OnFootstep(AnimationEvent animationEvent)
    {
        if (enabled) model.EmitFootstep(animationEvent);
    }
}
