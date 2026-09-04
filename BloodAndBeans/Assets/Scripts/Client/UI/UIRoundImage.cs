using UnityEngine;
using UnityEngine.UI;

/// 이 오브젝트의 <see cref="Image"/> 하나를 둥근 모서리로 만든다.
///
/// uGUI의 Image에는 모서리 반지름이라는 것이 없다. 사각 메시라 둥글게 하려면 알파로
/// 깎는 수밖에 없어서, 유니티가 기본으로 들고 있는 9-slice 스프라이트를 깔고 테두리가
/// 그려지는 크기로 반지름을 만든다 (<see cref="UIThemeConfig.RoundedSprite"/>).
/// 이미지 파일을 새로 굽지 않는 이유가 이것이다 — 반지름이 테마 값이라 파일로 구우면
/// 값을 바꿀 때마다 다시 구워야 한다.
///
/// **둥글게 할 Image에 하나씩 붙인다.** 화면 루트에 붙여 자식을 훑지 않는 이유는, 화면을
/// 덮는 배경과 1px 구분선처럼 둥글면 안 되는 것을 코드가 크기로 짐작해야 하기 때문이다.
/// 어느 판이 둥글어야 하는지는 화면을 만든 사람이 안다.
///
/// 프리팹에 값을 박지 않고 붙이기만 하는 것은 <see cref="UIFontScale"/>과 같은 이유다.
/// 원본은 그대로 두고 표시할 때만 테마를 먹인다.
///
/// ponytail: 내장 스프라이트의 모서리 10px을 늘려 쓰므로 반지름이 커질수록 가장자리가
/// 부드러워진다. 큰 반지름을 또렷하게 뽑아야 하면 테마의 스프라이트를 그 반지름으로
/// 그린 9-slice로 바꿔 끼우면 된다 — 이 코드는 그대로 둔다.
[RequireComponent(typeof(Image))]
[DisallowMultipleComponent]
public class UIRoundImage : MonoBehaviour
{
    /// 이 이미지만 다른 반지름을 쓸 때 채운다. 음수면 테마 값을 따른다.
    [SerializeField] float radiusOverride = -1f;

    Image image;

    /// 마지막으로 먹인 반지름. 같은 값을 두 번 칠하지 않기 위해서다 — 개발 콘솔이 재생
    /// 중에 반복해서 부른다.
    float applied = float.NaN;

    void Awake() => Apply(radiusOverride >= 0f ? radiusOverride : UITheme.Config.CornerRadius);

    /// <paramref name="radius"/>픽셀 반지름을 먹인다.
    public void Apply(float radius)
    {
        // 참조를 `Awake`가 아니라 여기서 잡는다. 개발 콘솔은 재생 중에 부르지만 에디터
        // 도구는 `Awake`가 돌지 않는 편집 중에도 부를 수 있다. 주기 실행이 아니라 한 번이다.
        if (image == null) image = GetComponent<Image>();

        var sprite = UITheme.Config.RoundedSprite;
        if (sprite == null || sprite.border.x <= 0f)
        {
            CDebug.LogError($"{name}: 테마에 둥근 9-slice 스프라이트가 없어 각진 채로 둔다. "
                          + "UIThemeConfig의 '모서리'에 UI/Skin/UISprite를 이어야 한다.", this);
            return;
        }

        // 반지름보다 얇은 판은 uGUI가 알아서 테두리를 줄여 그린다(`Image.GetAdjustedBorders`).
        // 여기서 rect를 보고 깎지 않는 이유는, LayoutGroup이 크기를 정하는 Image는 첫 레이아웃
        // 전인 `Awake` 시점에 rect가 0이라 반지름까지 0으로 죽기 때문이다.
        if (radius <= 0f || Mathf.Approximately(applied, radius)) return;
        applied = radius;

        // `pixelsPerUnit`이 스프라이트와 캔버스의 PPU 비를 담고 있으므로, 스프라이트를
        // 먼저 끼운 뒤에 읽어야 한다. 비어 있으면 유니티가 자기 기본값으로 답한다.
        image.sprite = sprite;
        image.type = Image.Type.Sliced;

        // 그려지는 9-slice 테두리 크기가 곧 모서리 반지름이다 (`border / (ppu * multiplier)`).
        image.pixelsPerUnitMultiplier = sprite.border.x / (radius * image.pixelsPerUnit);
    }
}
