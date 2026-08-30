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

    /// `default(CarryView)`는 "우유를 들고 있음"으로 읽힌다. `Ingredient.None`과
    /// `MenuId.None`이 0이 아니라 -1이기 때문이다 (`HeldItem.Nothing`과 같은 함정).
    public static CarryView Nothing =>
        new() { Ingredient = Ingredient.None, Menu = MenuId.None };

    public bool Empty => !IsProduct && Ingredient == Ingredient.None;

    public static CarryView Of(HeldItem item) => new()
    {
        Ingredient = item.IsProduct ? Ingredient.None : item.Ingredient,
        Menu = item.IsProduct ? item.Menu : MenuId.None,
        IsProduct = item.IsProduct,
        Burnt = item.Burnt,
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
            return Ingredient == Ingredient.None ? "빈손" : Ingredient.ToString();
        }
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Ingredient);
        serializer.SerializeValue(ref Menu);
        serializer.SerializeValue(ref IsProduct);
        serializer.SerializeValue(ref Burnt);
    }

    /// `NetworkVariable`은 `IEquatable`을 구현한 타입이면 `Equals`로 변경을 판정한다
    /// (NGO 2.13 `NetworkVariableSerialization.AreEqual`). 없으면 값이 그대로여도
    /// 매번 더티로 잡혀 낭비된다.
    public bool Equals(CarryView other) =>
        Ingredient == other.Ingredient && Menu == other.Menu &&
        IsProduct == other.IsProduct && Burnt == other.Burnt;
}
