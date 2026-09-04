using UnityEngine;

/// 목업(`00__Docs/02__Art/02__UI/UI_목업.pptx`)의 색표.
///
/// 값은 전부 <see cref="UIThemeConfig"/> 애셋이 갖는다. 이 클래스는 그 애셋을 한 번
/// 찾아 두고 이름 붙은 색으로 꺼내 주기만 한다 — 화면마다 `Resources.Load`를 반복하지
/// 않으려는 것이다.
///
/// 트리를 세우는 헬퍼(`Stage`·`Box`·`Text`·`Button` 등)는 여기 있었지만 지웠다. 모든
/// 화면이 프리팹 트리로 옮겨져 호출부가 하나도 남지 않았고, 남겨 두면 새 화면을 다시
/// 코드로 세우는 길이 열려 있게 된다.
public static class UITheme
{
    static UIThemeConfig config;

    /// 기본 디자인 원본. `Resources`에 애셋이 없으면 코드 기본값으로 만든 인스턴스를 쓴다 —
    /// 그 기본값은 애셋이 생기기 전과 같은 값이라 화면이 깨지지는 않지만, 개발 콘솔에서
    /// 만진 값이 저장되지 않으므로 한 번 경고한다.
    public static UIThemeConfig Config
    {
        get
        {
            if (config != null) return config;

            config = Resources.Load<UIThemeConfig>(UIThemeConfig.AssetName);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<UIThemeConfig>();
                CDebug.LogWarning(
                    $"Resources/{UIThemeConfig.AssetName} 애셋이 없다. 코드 기본값으로 그린다. " +
                    "개발 콘솔의 'UI 테마'에서 만들 수 있다.");
            }
            return config;
        }
    }

    /// 개발 콘솔이 애셋을 새로 만들거나 고른 뒤 부른다. 다음 조회부터 그 애셋을 읽는다.
    public static void UseConfig(UIThemeConfig replacement) => config = replacement;

    public static Color Ink       => Config.Ink;       // 배경
    public static Color Panel     => Config.Panel;     // 패널 바닥
    public static Color PanelDeep => Config.PanelDeep; // 카드 안쪽
    public static Color Cream     => Config.Cream;     // 본문 글자
    public static Color Gold      => Config.Gold;      // 구분선·라벨
    public static Color GoldLit   => Config.GoldLit;   // 강조 수치
    public static Color Green     => Config.Green;     // 이득
    public static Color Red       => Config.Red;       // 손실·카운트다운
    public static Color Blue      => Config.Blue;      // 밤
    public static Color Purple    => Config.Purple;    // 업그레이드 재료
    public static Color Ice       => Config.Ice;       // 슬롯·아이콘 자리

    /// 에셋이 아직 없는 아이콘·초상·썸네일 자리. 목업의 베이지 사각형이 이것이다.
    public static Color Placeholder => Config.Placeholder;

    /// 플레이어가 고르는 팀 색. 목업 2번의 `MY NAMEPLATE` 팔레트 순서 그대로이며,
    /// 인게임 네임플레이트에도 같은 색이 쓰인다.
    ///
    /// ponytail: 기획서에 팀 색 개념이 없다(9장·10장 어디에도). 목업에서만 온 값이라
    /// 테마 애셋에 두고, 확정되면 팀 데이터 원본으로 옮긴다.
    public static Color[] TeamColors => Config.TeamColors;
}
