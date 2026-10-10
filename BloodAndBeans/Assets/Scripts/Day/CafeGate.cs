using UnityEngine;

/// 카페 둘레 울타리. 카페는 모두에게 보이지만 들어오는 것은 같은 팀뿐이다.
///
/// 셸(`CafeShell`)의 벽은 보이기만 하고 콜라이더가 없어서, 막는 것은 이 콜라이더들뿐이다.
/// 같은 팀은 콜라이더 쌍을 무시해 통과시킨다 — 이동은 서버와 소유자가 같이 돌리므로
/// (소유자 예측) 두 피어가 같은 쌍을 무시해야 막힘 판정이 어긋나지 않는다. 그래서 이
/// 컴포넌트는 서버 전용이 아니라 모든 피어에서 돈다.
public class CafeGate : MonoBehaviour
{
    [SerializeField] Collider[] fences;

    void Awake()
    {
        if (fences == null || fences.Length == 0)
            CDebug.LogError($"{name}: 울타리 콜라이더가 비어 있다. 다른 팀이 카페에 들어온다.", this);
    }

    /// 이 플레이어는 울타리를 통과한다. 같은 팀 판정은 부르는 쪽이 한다.
    public void Admit(Collider body)
    {
        if (body == null) return;
        foreach (var fence in fences)
            if (fence != null) Physics.IgnoreCollision(body, fence, true);
    }
}
