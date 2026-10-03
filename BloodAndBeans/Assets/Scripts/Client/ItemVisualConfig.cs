using UnityEngine;
using UnityEngine.AddressableAssets;

/// 아이템이 화면에서 어떻게 생기는가. 재료 10종과 메뉴 10종이 각각 어느 프리팹(어드레서블
/// 참조)으로 그려지는지를 한 애셋이 쥔다.
///
/// 손 · 조리대 · 커피 머신 둘 · 오븐 · 재료 칸, 여섯 자리가 같은 표를 본다. 프리팹마다
/// 배열을 따로 두면 같은 원두가 자리마다 다르게 보일 수 있고, 진짜 메시가 들어올 때
/// 고칠 곳이 여섯 군데가 된다. 값이 아니라 **표현 설정**이라 ScriptableObject가 맞다
/// (`UIThemeConfig`와 같은 자리).
///
/// **이 애셋도 무엇을 세울지만 안다.** 실제 로드·Instantiate·해제는 `ItemSlotPresenter`가
/// `ResourceManager`를 통해 한다 — 시설과 손이 같은 자리 표시 로직을 쓰기 위한 것이다
/// (`ItemDisplay`, `PlayerVisuals`).
///
/// 아트가 준비되면 여기 참조만 갈아 끼운다. 코드는 손대지 않는다.
[CreateAssetMenu(menuName = "Blood & Beans/아이템 표시", fileName = AssetName)]
public class ItemVisualConfig : ScriptableObject
{
    public const string AssetName = "ItemVisualConfig";

    /// 열거자 값과 프리팹을 눈에 보이게 짝지어 둔다. 배열 인덱스로 대응시키면 열거자에
    /// 한 줄 끼워 넣는 순간 전부 한 칸씩 밀린다.
    [System.Serializable]
    public struct IngredientEntry
    {
        public Ingredient id;
        public AssetReference prefab;
    }

    [System.Serializable]
    public struct MenuEntry
    {
        public MenuId id;
        public AssetReference prefab;
    }

    [SerializeField] IngredientEntry[] ingredients;
    [SerializeField] MenuEntry[] menus;

    [Header("예외")]
    [Tooltip("메뉴 표에 없는 조합의 완성품. CarryView가 「정체불명」이라 부르는 그것이다.")]
    [SerializeField] AssetReference unknownProduct;

    [Tooltip("빈 접시. 비면 빵 베이스로 대신 그린다.")]
    [SerializeField] AssetReference emptyPlate;

    [Tooltip("빈 잔. 비면 원두로 대신 그린다.")]
    [SerializeField] AssetReference emptyCup;

    [Tooltip("잔에 담는 재료(원두)를 잔 폭의 몇 배로 줄일지.")]
    [SerializeField, Range(0.1f, 1f)] float cupFill = 0.8f;

    /// 기본 머티리얼 하나와 그 상태별 사본 (기획서 5.3.1 `Dirty`·`Burnt`). 메시는 그대로 두고 재질만 바꾼다.
    /// 값을 셰이더에 덮어쓰지 않고 사본으로 갈아 끼우는 이유는 SRP Batcher다 — MaterialPropertyBlock은 배칭에서 빠진다.
    [System.Serializable]
    public struct MaterialStates
    {
        public Material clean;
        public Material dirty;
        public Material toasted;
        public Material burnt;
    }

    [Header("상태별 머티리얼")]
    [Tooltip("기본 머티리얼 → 더러운 것 · 구운 것 · 탄 것. 표에 없거나 칸이 비면 원래 재질 그대로 그린다. " +
             "재질은 가벼운 공유 애셋이라 직접 참조로 둔다(어드레서블화 대상은 무거운 모델뿐).")]
    [SerializeField] MaterialStates[] materialStates;

    System.Collections.Generic.Dictionary<Material, MaterialStates> statesByClean;

    void OnValidate() => statesByClean = null;

    /// 오븐을 거친 것 — 접시에 담긴 완성품. 구운 빵 베이스와 그 위에 토핑을 얹은 디저트다.
    public static bool IsBaked(CarryView view) => view.IsProduct && view.DishIsPlate;

    /// 이 상태에서 `clean` 대신 쓸 재질. 탄 것 > 더러운 것 > 구운 것 순이다.
    public Material StateOf(Material clean, bool dirty, bool burnt, bool toasted)
    {
        if (clean == null || (!dirty && !burnt && !toasted)) return clean;

        if (statesByClean == null)
        {
            statesByClean = new System.Collections.Generic.Dictionary<Material, MaterialStates>();
            if (materialStates != null)
                foreach (var entry in materialStates)
                    if (entry.clean != null) statesByClean[entry.clean] = entry;
        }

        if (!statesByClean.TryGetValue(clean, out var states)) return clean;
        var swap = burnt ? states.burnt : dirty ? states.dirty : states.toasted;
        return swap != null ? swap : clean;
    }

    /// 내용물을 받칠 그릇. 빵·디저트 모델에는 접시가, 원두 모델에는 잔이 없어 따로 세운다.
    /// 음료 완성품은 모델에 잔이 들어 있어 받치지 않는다.
    public AssetReference DishFor(CarryView view)
    {
        if (!view.HasDish || (!view.IsProduct && view.Ingredient == Ingredient.None)) return null;
        if (!view.DishIsPlate && view.IsProduct) return null;
        var dish = view.DishIsPlate ? emptyPlate : emptyCup;
        return dish != null && dish.RuntimeKeyIsValid() ? dish : null;
    }

    public float CupFill => cupFill;

    /// 이 자리에 세울 프리팹의 참조. 빈 칸이거나 표에 없으면 null이고, 그러면 아무것도 서지 않는다.
    public AssetReference ReferenceFor(CarryView view)
    {
        if (view.Empty) return null;
        if (view.HasDish && !view.IsProduct && view.Ingredient == Ingredient.None)
        {
            var empty = view.DishIsPlate ? emptyPlate : emptyCup;
            if (empty != null && empty.RuntimeKeyIsValid()) return empty;
            view.Ingredient = view.DishIsPlate ? Ingredient.BreadBase : Ingredient.Bean;
        }

        if (view.IsProduct)
        {
            if (menus != null)
                for (var i = 0; i < menus.Length; i++)
                    if (menus[i].id == view.Menu && menus[i].prefab != null && menus[i].prefab.RuntimeKeyIsValid())
                        return menus[i].prefab;

            // 조합은 성립했는데 메뉴가 아니다 (`Menus.Match`). 바탕만 있는 완성품은 아래에서 바탕 모델로 그린다.
            if (view.Ingredient == Ingredient.None)
                return unknownProduct != null && unknownProduct.RuntimeKeyIsValid() ? unknownProduct : null;
        }

        if (ingredients != null)
            for (var i = 0; i < ingredients.Length; i++)
                if (ingredients[i].id == view.Ingredient)
                    return ingredients[i].prefab != null && ingredients[i].prefab.RuntimeKeyIsValid()
                        ? ingredients[i].prefab : null;

        return null;
    }
}
