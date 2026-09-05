using System;
using Unity.Netcode;

/// 손이나 조리대에 놓인 것을 **화면에 그리기 위한** 최소 정보.
///
/// `HeldItem`을 그대로 복제할 수 없어서 따로 둔다 — `Recipe`가 관리 배열이라
/// `NetworkVariable`에 실을 수 없다. 규칙 판정은 여전히 서버의 `HeldItem`이 하고,
/// 여기 있는 것은 이름표를 만드는 데 필요한 만큼뿐이다.
///
/// 두 곳이 같은 것을 보여 준다: 플레이어의 손(`PlayerCarry`)과 조리대에 놓인 것
/// (`PrepIsland`). 그래서 타입 하나로 둔다.
public struct CarryView : INetworkSerializable, IEquatable<CarryView>
{
    public Ingredient Ingredient;
    public MenuId Menu;
    public bool IsProduct;
    public bool Burnt;

    /// 조리대에서 조립 중인 디저트인가 (기획서 5.1). 완성품과 낱개 재료 사이의 상태라
    /// 이 표시가 없으면 재료 여러 개가 바탕 하나로만 보인다.
    public bool Assembled;

    /// 재료 개수 (기획서 9.1 「양손잡이」). 1이면 이름표에 표시하지 않는다.
    public int Count;

    /// `default(CarryView)`는 "우유를 들고 있음"으로 읽힌다. `Ingredient.None`과
    /// `MenuId.None`이 0이 아니라 -1이기 때문이다 (`HeldItem.Nothing`과 같은 함정).
    public static CarryView Nothing =>
        new() { Ingredient = Ingredient.None, Menu = MenuId.None };

    public bool Empty => !IsProduct && Ingredient == Ingredient.None;

    /// 아직 손에 들리지 않은 재료. 재료 칸이 자기 재고를 그릴 때 쓴다.
    public static CarryView Of(Ingredient ingredient) =>
        new() { Ingredient = ingredient, Menu = MenuId.None, Count = 1 };

    /// 조립물의 `Count`는 든 개수가 아니라 **얹힌 재료 수**다. 조립물은 늘 하나뿐이라
    /// (`HeldItem.Amount`) 그 자리에 개수를 넣으면 항상 1만 보인다.
    public static CarryView Of(HeldItem item) => new()
    {
        Ingredient = item.IsProduct ? Ingredient.None : item.Ingredient,
        Menu = item.IsProduct ? item.Menu : MenuId.None,
        IsProduct = item.IsProduct,
        Assembled = item.IsAssembly,
        Burnt = item.Burnt,
        Count = item.IsAssembly ? item.Recipe.Length : (item.Empty ? 0 : item.Amount),
    };

    /// 표시용 이름. 재료·메뉴의 한글 이름표가 아직 없어서 enum 이름을 그대로 쓴다 —
    /// HUD의 인기 재료 표시와 같은 방식이다.
    /// ponytail: 한글 이름표는 localization 작업이다. 생기면 이 한 곳만 고치면 된다.
    public string Label
    {
        get
        {
            if (IsProduct)
            {
                // 메뉴 표에 없는 조합도 완성품이 된다 (`Menus.Match`가 None을 돌려준다).
                // 그것을 "빈손"으로 그리면 들고 있는 것이 화면에서 사라진다.
                var name = Menu == MenuId.None ? "정체불명" : Menu.ToString();
                return Burnt ? $"{name} (탄 것)" : name;
            }
            if (Ingredient == Ingredient.None) return "빈손";
            if (Assembled) return $"{Ingredient} 조립 · 재료 {Count}개";
            return Count > 1 ? $"{Ingredient} ×{Count}" : Ingredient.ToString();
        }
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Ingredient);
        serializer.SerializeValue(ref Menu);
        serializer.SerializeValue(ref IsProduct);
        serializer.SerializeValue(ref Assembled);
        serializer.SerializeValue(ref Burnt);
        serializer.SerializeValue(ref Count);
    }

    /// `NetworkVariable`은 `IEquatable`을 구현한 타입이면 `Equals`로 변경을 판정한다
    /// (NGO 2.13 `NetworkVariableSerialization.AreEqual`). 없으면 값이 그대로여도
    /// 매번 더티로 잡혀 낭비된다.
    public bool Equals(CarryView other) =>
        Ingredient == other.Ingredient && Menu == other.Menu &&
        IsProduct == other.IsProduct && Assembled == other.Assembled &&
        Burnt == other.Burnt && Count == other.Count;
}
