#ifndef BB_UI_ROUNDED_INCLUDED
#define BB_UI_ROUNDED_INCLUDED

// 둥근 사각형까지의 부호 있는 거리(SDF). UI 셰이더 둘이 같이 쓴다 —
// `UIRoundedRect`는 이 거리로 모서리를 깎고, `UIGlowFrame`은 이 거리의 절댓값으로
// 테두리를 그린다. 같은 수식을 두 벌 두면 한쪽만 고쳐지므로 여기 한 벌만 둔다.

/// <paramref name="p"/>에서 둥근 사각형 테두리까지의 거리. 안이 음수, 밖이 양수다.
/// <paramref name="halfSize"/>는 중심에서 변까지, <paramref name="radius"/>는 모서리
/// 반지름이며 둘 다 <paramref name="p"/>와 같은 단위여야 한다.
float BB_RoundedBox(float2 p, float2 halfSize, float radius)
{
    float2 q = abs(p) - halfSize + radius;
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
}

#endif
