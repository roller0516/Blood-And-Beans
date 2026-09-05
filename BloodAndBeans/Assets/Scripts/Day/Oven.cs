using UnityEngine;

/// 제작 시간이 길다 (기획서 5.1). 디저트는 재료를 넣는 횟수도 더 많다.
public class Oven : Station
{
    void Reset() => cookSeconds = 9f;   // ponytail: 임시값. 기획서 14장에 시간 표가 없다

    /// 「오븐 확장」는 이 기계를 2레인으로 만든다 (기획서 8장).
    protected override UpgradeId? ParallelUpgrade => UpgradeId.OvenExpansion;

    /// **낱개 재료는 받지 않는다** (기획서 5.1). 디저트는 조리대에서 바탕에 재료를 얹어
    /// 조립한 뒤 통째로 들어온다 (`PrepIsland`). 오븐이 재료를 직접 받으면 조립 단계가
    /// 사라져 디저트가 커피와 같은 단계 수가 된다 — 기획서 5.1은 하나둘 많다고 정했다.
    protected override bool AcceptsAssembly => true;

    protected override bool AcceptsIngredient(Ingredient ingredient, int currentCount) => false;
}
