/// 상호작용 후보를 판단할 때 대상에게 주는 묻는 쪽의 손 상태.
///
/// 대상이 안내 가능 여부와 문구를 스스로 답하는 데 필요한 만큼만 담는다 — 서비스
/// 로케이터나 전역 상태가 아니라 호출마다 값으로 넘기는 입력이다.
public readonly struct InteractionContext
{
    public readonly CarryView Held;
    public readonly bool Reserved;

    public InteractionContext(CarryView held, bool reserved)
    {
        Held = held;
        Reserved = reserved;
    }
}

public interface IInteractable
{
    /// 지금 이 손 상태로 안내(프롬프트 노출)해도 되는가. 대상마다 다른 설비 규칙은
    /// 여기서 각자 답한다 — 플레이어는 후보 탐색·선택만 한다.
    ///
    /// 서버 실행 권한과는 다르다. 여기서 걸러도 실제 판정은 각 RPC 본문이 다시 한다
    /// (AGENTS.md 「Netcode에서 쓰지 말아야 할 방식」).
    bool CanPromptClient(in InteractionContext context);

    /// 안내 문구. 손 상태에 따라 갈리는 대상(개수대 「세척 필요」 등)은 context를 본다.
    string PromptFor(in InteractionContext context);

    void BeginInteractionClient();
    void EndInteractionClient();
}
