using UnityEngine;

/// UI의 기본 디자인 원본. 색·글자 크기·버튼 규격처럼 "따로 지정하지 않으면 이것"인
/// 값을 한 애셋이 쥔다. <see cref="UITheme"/>이 이 애셋을 읽고, 화면들은 UITheme만 부른다.
///
/// 값이 코드에 박혀 있으면 톤을 한 번 바꿀 때마다 컴파일을 기다려야 하고, 화면마다
/// 조금씩 다른 숫자가 새어 든다. 애셋으로 빼 두면 개발 콘솔에서 재생 중에 돌려 보고
/// 마음에 드는 값을 그대로 저장할 수 있다 (`UIThemeGroup`).
///
/// 공유 설정이자 사실상 불변 데이터라 ScriptableObject가 맞다. 런타임에 바뀌는 게임
/// 상태는 여기 두지 않는다.
[CreateAssetMenu(menuName = "Blood & Beans/UI 테마", fileName = AssetName)]
public class UIThemeConfig : ScriptableObject
{
    /// `Resources`에서 찾을 이름. 경로를 여러 곳에 적지 않으려고 여기 하나만 둔다.
    public const string AssetName = "UIThemeConfig";

    [Header("무대")]
    [Tooltip("목업이 그려진 판의 크기. CanvasScaler 기준 해상도와 같아야 좌표가 1:1이다.")]
    [SerializeField] Vector2 stageSize = new(1920f, 1080f);

    [Header("색")]
    [SerializeField] Color ink = Hex(0x120C08);          // 배경
    [SerializeField] Color panel = Hex(0x0E0905);        // 패널 바닥
    [SerializeField] Color panelDeep = Hex(0x180F09);    // 카드 안쪽
    [SerializeField] Color cream = Hex(0xF2E3CB);        // 본문 글자
    [SerializeField] Color gold = Hex(0xC6974A);         // 구분선·라벨
    [SerializeField] Color goldLit = Hex(0xE9B85C);      // 강조 수치
    [SerializeField] Color green = Hex(0x7CD9A8);        // 이득
    [SerializeField] Color red = Hex(0xD9563F);          // 손실·카운트다운
    [SerializeField] Color blue = Hex(0x7CAFD9);         // 밤
    [SerializeField] Color purple = Hex(0xA46EE8);       // 업그레이드 재료
    [SerializeField] Color ice = Hex(0xE6EEF5);          // 슬롯·아이콘 자리
    [SerializeField] Color placeholder = Hex(0xF2E3CB);  // 에셋 없는 아이콘 자리

    /// 플레이어가 고르는 팀 색. 목업 2번 `MY NAMEPLATE` 팔레트 순서 그대로다.
    [SerializeField] Color[] teamColors =
    {
        Hex(0x4FB8E8), Hex(0xE86A4F), Hex(0x7CD9A8),
        Hex(0xE9B85C), Hex(0xB98CF0), Hex(0xF0E4CB),
    };

    [Header("글자")]
    [Tooltip("프리팹에 박힌 글자 크기에 곱하는 배율. 목업 좌표가 작게 잡혀 본문이 10px이라 " +
             "그대로는 1080p에서 읽기 어렵다. 위계를 유지하려고 모든 크기에 같은 배율을 곱한다.")]
    [SerializeField, Min(0.1f)] float fontScale = 1.9f;

    [Tooltip("소제목(`TODAY'S TRADE` 등)의 크기와 줄 높이.")]
    [SerializeField, Min(1f)] float captionSize = 11f;
    [SerializeField, Min(1f)] float captionHeight = 16f;

    [Header("버튼")]
    [SerializeField, Min(1f)] float buttonPrimaryLabelSize = 20f;
    [SerializeField, Min(1f)] float buttonSecondaryLabelSize = 16f;
    [SerializeField, Min(1f)] float buttonLabelHeight = 26f;

    [Header("구분선")]
    [SerializeField, Min(0.1f)] float ruleHeight = 1f;

    public Vector2 StageSize => stageSize;

    public Color Ink => ink;
    public Color Panel => panel;
    public Color PanelDeep => panelDeep;
    public Color Cream => cream;
    public Color Gold => gold;
    public Color GoldLit => goldLit;
    public Color Green => green;
    public Color Red => red;
    public Color Blue => blue;
    public Color Purple => purple;
    public Color Ice => ice;
    public Color Placeholder => placeholder;
    public Color[] TeamColors => teamColors;

    public float FontScale => fontScale;
    public float CaptionSize => captionSize;
    public float CaptionHeight => captionHeight;

    public float ButtonPrimaryLabelSize => buttonPrimaryLabelSize;
    public float ButtonSecondaryLabelSize => buttonSecondaryLabelSize;
    public float ButtonLabelHeight => buttonLabelHeight;

    public float RuleHeight => ruleHeight;

    static Color Hex(int rgb) => new(
        ((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
}
