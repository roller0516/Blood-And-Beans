using UnityEngine;

/// 모델에서 팀 색을 입힐 부분만 지정한다. 얼굴·눈·텍스처와 플레이어 소품은 보존한다.
[DisallowMultipleComponent]
public sealed class CharacterModel : MonoBehaviour
{
    [Tooltip("팀 색을 입힐 의상이나 장식의 루트. 원래 재질을 유지할 부위는 넣지 않는다.")]
    [SerializeField] Transform[] teamTintRoots = System.Array.Empty<Transform>();

    public void Tint(Color color, float strength)
    {
        foreach (var root in teamTintRoots)
            if (root != null) TeamColors.TintWith(root.gameObject, color, strength);
    }
}
