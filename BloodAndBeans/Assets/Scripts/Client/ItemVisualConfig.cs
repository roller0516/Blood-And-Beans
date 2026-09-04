using UnityEngine;

/// 아이템이 화면에서 어떻게 생겼는가. 재료 10종과 메뉴 10종이 각각 어느 프리팹으로
/// 그려지는지를 한 애셋이 쥔다.
///
/// 손 · 조리대 · 커피 머신 둘 · 오븐 · 재료 칸, 여섯 자리가 같은 표를 본다. 프리팹마다
/// 배열을 따로 두면 같은 원두가 자리마다 다르게 보일 수 있고, 진짜 메시가 들어올 때
/// 고칠 곳이 여섯 군데가 된다. 값이 아니라 **표현 설정**이라 ScriptableObject가 맞다
/// (`UIThemeConfig`와 같은 자리).
///
/// 아트가 준비되면 여기 프리팹만 갈아 끼운다. 코드는 손대지 않는다.
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
        public GameObject prefab;
    }

    [System.Serializable]
    public struct MenuEntry
    {
        public MenuId id;
        public GameObject prefab;
    }

    [SerializeField] IngredientEntry[] ingredients;
    [SerializeField] MenuEntry[] menus;

    [Header("예외")]
    [Tooltip("메뉴 표에 없는 조합의 완성품. CarryView가 「정체불명」이라 부르는 그것이다.")]
    [SerializeField] GameObject unknownProduct;

    [Tooltip("탄 것. 메시는 그대로 두고 재질만 이것으로 바꾼다 — 무엇이 탔는지도 보여야 한다.")]
    [SerializeField] Material burnt;

    public Material Burnt => burnt;

    /// 이 자리에 세울 프리팹. 빈 칸이거나 표에 없으면 null이고, 그러면 아무것도 서지 않는다.
    public GameObject PrefabFor(CarryView view)
    {
        if (view.Empty) return null;

        if (view.IsProduct)
        {
            if (menus != null)
                for (var i = 0; i < menus.Length; i++)
                    if (menus[i].id == view.Menu && menus[i].prefab != null) return menus[i].prefab;

            return unknownProduct;      // 조합은 성립했는데 메뉴가 아니다 (`Menus.Match`)
        }

        if (ingredients != null)
            for (var i = 0; i < ingredients.Length; i++)
                if (ingredients[i].id == view.Ingredient) return ingredients[i].prefab;

        return null;
    }
}
