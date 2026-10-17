using UnityEngine;

/// 문자 없는 공통 외형. 프리팹 변형이 Sprite와 9-slice 규격을 공유한다.
[DisallowMultipleComponent]
public sealed class UISurfacePart : MonoBehaviour
{
    [SerializeField] UnityEngine.UI.Image image;

    public UnityEngine.UI.Image Graphic => image;

    public void SetTint(Color color)
    {
        if (image.color != color) image.color = color;
    }
}
