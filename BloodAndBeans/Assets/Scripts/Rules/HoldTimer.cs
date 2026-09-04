using System.Collections.Generic;

/// 클라이언트별로 홀드 시간을 누적하는 서버 측 계산기.
///
/// 클라이언트는 "누르기 시작함"과 "뗐음"만 보고하고, 경과 시간은 서버 시계로 여기서 잰다.
/// 홀드를 소유자 쪽에서 재면 완료 RPC를 미리 보내는 것만으로 건너뛸 수 있었고, 밤의 파밍
/// 루프 전체가 그 상태였다 (아키텍처_v1.0.md §1.1).
///
/// UnityEngine에 의존하지 않는 것은 의도다. 이것은 규칙이고, 규칙은 씬 없이 테스트한다
/// (아키텍처_v1.0.md §2.1).
public class HoldTimer
{
    readonly Dictionary<ulong, double> startedAt = new();

    public bool Holding(ulong client) => startedAt.ContainsKey(client);

    /// 중복 Begin은 무시한다. 그러지 않으면 클라이언트가 이걸 연타해 진행도를 0에 묶어
    /// 두거나, 더 나쁘게는 자기가 채우지도 않은 정상 홀드를 덮어쓸 수 있다.
    public void Begin(ulong client, double now)
    {
        if (!startedAt.ContainsKey(client)) startedAt[client] = now;
    }

    public void Cancel(ulong client) => startedAt.Remove(client);

    public void CancelAll() => startedAt.Clear();

    public float Elapsed(ulong client, double now) =>
        startedAt.TryGetValue(client, out var t) ? (float)(now - t) : 0f;

    /// `seconds`가 지나면 true를 반환하고 성공 시 시계를 다시 돌린다. 즉 키를 계속 누르고
    /// 있으면 `seconds`마다 아이템 하나씩 나온다 (기획서 6.5.1: 담기 개당 0.2초, 연속).
    public bool TryConsume(ulong client, double now, float seconds)
    {
        if (!startedAt.TryGetValue(client, out var t)) return false;
        if (now - t < seconds) return false;
        startedAt[client] = now;
        return true;
    }

    /// 현재 홀드 중인 클라이언트를 호출자 소유 리스트로 복사한다. 직접 순회하면서 이 타이머를
    /// 변경할 수는 없고, 매 프레임 할당도 공짜가 아니다. 그래서 순회 대상을 스냅샷으로 넘긴다.
    public void CopyClientsTo(List<ulong> outp)
    {
        outp.Clear();
        foreach (var pair in startedAt) outp.Add(pair.Key);
    }
}
