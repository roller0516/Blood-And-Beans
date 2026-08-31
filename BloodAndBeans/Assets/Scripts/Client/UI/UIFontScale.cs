using TMPro;
using UnityEngine;

/// 화면 하위의 모든 TMP 글자에 테마 배율을 곱한다.
///
/// 화면들이 프리팹 트리로 옮겨지면서 글자 크기가 프리팹에 박혔다. 목업 좌표가 작게
/// 잡혀 본문이 10px이라 1080p에서 읽기 어려운데, 프리팹 값을 직접 다시 쓰면 목업과
/// 영구히 어긋난다. 그래서 원본은 그대로 두고 표시할 때만 배율을 곱한다 —
/// 모든 크기에 같은 배율을 쓰므로 목업이 정한 위계는 보존된다.
///
/// 배율은 <see cref="UIThemeConfig.FontScale"/>이 정하고 개발 콘솔에서 만질 수 있다.
[DisallowMultipleComponent]
public class UIFontScale : MonoBehaviour
{
    TMP_Text[] texts;
    float[] baseSizes;

    /// 마지막으로 적용한 배율. 되돌리기 위해서가 아니라 같은 값을 두 번 칠하지 않기 위해서다.
    float applied;

    void Awake() => Apply(UITheme.Config.FontScale);

    /// 원본 크기에 <paramref name="scale"/>을 곱한다. 원본을 따로 들고 있으므로 여러 번
    /// 불러도 누적되지 않는다 — 개발 콘솔이 재생 중에 반복해서 부른다.
    public void Apply(float scale)
    {
        if (scale <= 0f) return;

        // 비활성 자식까지 포함해 한 번만 모은다. 이후에는 캐시만 쓴다.
        if (texts == null)
        {
            texts = GetComponentsInChildren<TMP_Text>(true);
            baseSizes = new float[texts.Length];
            for (var i = 0; i < texts.Length; i++)
                baseSizes[i] = texts[i] != null ? texts[i].fontSize : 0f;
        }

        if (Mathf.Approximately(applied, scale)) return;
        applied = scale;

        for (var i = 0; i < texts.Length; i++)
            if (texts[i] != null) texts[i].fontSize = baseSizes[i] * scale;
    }
}
