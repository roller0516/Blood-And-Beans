/// 이동 배수를 한 축에서 합친다 (기획서 3.3 · 8.2 · 9.1.2). 보석·미납 페널티·캐릭터
/// 액티브가 곱으로 쌓이는 유일한 식이다 — 나눠 합치면 셋이 서로를 덮어쓴다.
///
/// 무게(`PlayerController.loadScale`)는 여기 없다. 무게는 서버 원장이 아니라 인벤토리에서
/// 오고 채널이 다르다 — `PlayerController.PushScaleServer`가 이 결과와 곱해 최종 배수를 낸다.
public static class MoveSpeed
{
    public static float Combine(bool windGem, float ledgerScale, float abilityScale) =>
        (windGem ? Gems.MoveSpeedScale : 1f) * ledgerScale * abilityScale;
}
