/// 설비 업그레이드 한 종의 정의 (기획서 8장).
public readonly struct UpgradeDef
{
    public readonly UpgradeId Id;
    public readonly string Name;
    public readonly string Effect;

    /// 설치에 드는 업그레이드 재료 개수. 3등급 박스에서만 나온다 (기획서 8장).
    public readonly int Cost;

    public UpgradeDef(UpgradeId id, string name, string effect, int cost)
    {
        Id = id; Name = name; Effect = effect; Cost = cost;
    }
}

public enum UpgradeId
{
    IngredientShelf, ConveyorBelt, TwinMachine, OvenExpansion, Dishwasher,
    ExtraDishes, AutoServer, IceMaker, MilkTank,
}

/// 업그레이드 9종의 이름·효과 문구 (기획서 8장). 화면이 문자열을 직접 들고 있지 않도록
/// 한 곳에 모은다. 순수 데이터라 `BB.Rules`에 둔다 — UI도 규칙도 같은 출처를 읽는다.
public static class UpgradeCatalog
{
    // ponytail: 재료 비용은 기획서 8장에 표가 없다. 기획서 14장에도 항목이 없어
    // 미결이라 목업(UI_목업.pptx 7번)의 값을 그대로 옮겼다. 표가 생기면 DT_Upgrade로 옮긴다.
    public static readonly UpgradeDef[] All =
    {
        new(UpgradeId.IngredientShelf, "재료 선반",  "특정 재료 1종이 제조존에도 비치된다", 1),
        new(UpgradeId.ConveyorBelt,    "전달 벨트",  "조리대에 올린 재료가 반대편으로 자동 이동", 2),
        new(UpgradeId.TwinMachine,     "2구 머신",   "한 대에서 2잔 동시 제조", 2),
        new(UpgradeId.OvenExpansion,   "오븐 확장",  "디저트 2개 동시 굽기", 2),
        new(UpgradeId.Dishwasher,      "식기세척기", "더러운 그릇이 자동으로 세척된다 (느리게)", 3),
        new(UpgradeId.ExtraDishes,     "그릇 추가",  "그릇 4개 → 6개", 1),
        new(UpgradeId.AutoServer,      "자동 서빙대", "완성품을 올려두면 자동으로 손님에게 나간다", 3),
        new(UpgradeId.IceMaker,        "제빙기",     "얼음 무한 공급 (재료 칸에 항상 있음)", 2),
        new(UpgradeId.MilkTank,        "우유 탱크",  "우유 무한 공급", 2),
    };

    public static UpgradeDef Get(UpgradeId id) => All[(int)id];
}
