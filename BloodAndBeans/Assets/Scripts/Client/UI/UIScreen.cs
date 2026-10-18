/// 프리팹 모드 씬 뷰에서 화면을 눌러 볼 수 있는 상태 하나. `UIScreenPreviewOverlay`가 버튼으로 그린다.
public readonly struct PreviewState
{
    public readonly string Label;
    public readonly System.Action Apply;

    public PreviewState(string label, System.Action apply)
    {
        Label = label;
        Apply = apply;
    }
}

/// 한 번에 하나만 보이는 전체 화면. 타이틀·방 목록·대기실·매치 HUD가 여기 해당한다.
/// `UIManager`가 스택으로 관리하며, 뒤로 가기는 스택을 하나 내리는 것이다.
public abstract class UIScreen : UIView
{
#if UNITY_EDITOR
    /// 프리팹 모드에서 이 화면을 전환해 볼 수 있는 상태들. 비어 있으면 오버레이가 뜨지 않는다.
    /// 게임 로직은 부르지 않는다 — 에디터에서 배치를 확인하는 용도다.
    public virtual PreviewState[] PreviewStates => System.Array.Empty<PreviewState>();
#endif
}
