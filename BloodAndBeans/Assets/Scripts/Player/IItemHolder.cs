/// 아이템이 눈에 보이는 자리. 손(`PlayerCarry`) · 조리대(`PrepIsland`) · 설비(`Station`) ·
/// 재료 칸(`IngredientShelf`)이 모두 이것을 구현하고, 클라이언트 표현(`ItemDisplay`)은
/// 이 계약 하나만 안다.
///
/// 값 하나가 아니라 **자리**를 노출하는 이유는 설비가 재료를 여러 개 물고 있기 때문이다
/// (`Station.maxIngredients`). 자리 수가 곧 프리팹에 꽂아 둔 앵커 수다.
///
/// 여기 오는 값은 전부 이미 복제된 것이다. 표현이 서버 상태를 직접 읽지 않게 하려고
/// `HeldItem`이 아니라 `CarryView`를 돌려준다 (`CarryView` 주석).
public interface IItemHolder
{
    /// 내용이 바뀌었다. 복제 값이 도착할 때마다 오르므로 표현은 이때만 다시 그린다 —
    /// 매 프레임 물어보지 않기 위한 장치다 (AGENTS.md 참조와 결합도).
    event System.Action ContentsChanged;

    /// 이 자리가 가진 칸 수. 프리팹의 앵커 수와 같아야 한다. 모자라면 뒤쪽 칸이 안 보인다.
    int SlotCount { get; }

    /// 빈 칸은 `CarryView.Nothing`이다.
    CarryView SlotAt(int index);

    /// 지금 강조할 칸. 없으면 -1. 재료 칸이 "다음에 F를 누르면 이게 온다"를 보이는 데 쓴다.
    /// **이 값만 클라이언트마다 다를 수 있다** — 선반의 순환 인덱스가 로컬 상태다
    /// (`IngredientShelf.index`).
    int HighlightSlot { get; }
}
