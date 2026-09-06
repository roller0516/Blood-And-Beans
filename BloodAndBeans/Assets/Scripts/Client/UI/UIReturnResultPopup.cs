using TMPro;
using UnityEngine;

/// 밤이 끝난 직후 자기 귀환 결과를 알려 주는 팝업 (기획서 6.8).
///
/// 문구는 기획서 6.8의 세 갈래를 그대로 쓴다. 여기서 말을 새로 지어내면 기획서와
/// 구현이 서로 다른 규칙을 설명하게 된다.
///
/// | 조건 | 문구 |
/// |---|---|
/// | 소환 위치 O + 가방 소지 O | 획득 아이템 100% 귀환 |
/// | 소환 위치 X + 가방 소지 O | 획득 아이템 n% 소실 |
/// | 가방 소지 X (위치 무관)   | 획득 아이템 100% 소실 |
///
/// **트리는 프리팹에 있다.** 이 클래스는 아무것도 만들지 않고 이어 둔 참조에 값만 넣는다.
/// 색은 결과에 따라 갈리므로 여기 남고, 자리·크기·글자 크기는 프리팹이 갖는다.
public sealed class UIReturnResultPopup : UIPopup
{
    [Header("부품")]
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text lineText;
    [SerializeField] TMP_Text detailText;

    /// 결과에 따라 갈리는 색. 세 갈래가 한 자리를 돌려 쓰므로 프리팹이 아니라 여기 있다.
    [Header("색")]
    [SerializeField] Color success = new(0.55f, 0.85f, 0.52f);
    [SerializeField] Color warning = new(0.95f, 0.76f, 0.33f);
    [SerializeField] Color failure = new(0.93f, 0.35f, 0.28f);

    /// 전환은 10초뿐이고 그동안 플레이어가 할 일이 없다. 창을 띄운 채 두면 정보가
    /// 남아 있는 편이 낫고, 닫는 것은 `MatchFlow`가 낮이 시작될 때 한다.
    public override bool BlocksPlayerInput => false;

    /// 결과를 그린다. `n%`는 `ReturnZone`이 들고 있는 실제 설정값에서 온다 — 문구에
    /// 50을 박아 두면 인스펙터에서 비율을 바꿨을 때 화면만 거짓말을 한다.
    public void Bind(ReturnOutcome outcome, int kept, int lost, int lossPercent)
    {
        switch (outcome)
        {
            case ReturnOutcome.Returned:
                Set(titleText, "귀환 성공", success);
                Set(lineText, "소지한 아이템 100% 귀환", success);
                Set(detailText, lost > 0
                    ? $"재료 {kept}개 반입 · 회수하지 못한 가방에서 {lost}개 소실"
                    : kept > 0
                    ? $"재료 {kept}개를 팀 재고로 넘겼다"
                    : "가져온 재료가 없다");
                break;

            case ReturnOutcome.PartialLoss:
                Set(titleText, "귀환 실패", warning);
                Set(lineText, $"소지한 아이템 {lossPercent}% 소실 · 미회수 가방 전량 소실", warning);
                Set(detailText, $"소환 위치 밖에서 밤이 끝났다 · {lost}개 소실 / {kept}개 반입");
                break;

            default:
                Set(titleText, "가방 분실", failure);
                Set(lineText, "획득 아이템 100% 소실", failure);
                Set(detailText, lost > 0
                    ? $"묻어 둔 가방을 회수하지 못했다 · {lost}개 소실"
                    : "묻어 둔 가방을 회수하지 못했다");
                break;
        }
    }

    /// 설명 줄은 색이 바뀌지 않는다 — 프리팹이 정한 색을 그대로 둔다.
    static void Set(TMP_Text target, string value)
    {
        if (target != null) target.text = value;
    }

    static void Set(TMP_Text target, string value, Color color)
    {
        if (target == null) return;
        target.text = value;
        target.color = color;
    }
}
