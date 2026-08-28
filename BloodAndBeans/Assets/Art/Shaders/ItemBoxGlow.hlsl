#ifndef BB_ITEM_BOX_GLOW_INCLUDED
#define BB_ITEM_BOX_GLOW_INCLUDED

// 3등급 상자의 발광 아웃라인. Shader Graph의 Custom Function 노드가 부른다.
//
// 상자 메시를 살짝 키워 **뒷면만** 그리는 고전적인 아웃라인 헐이다. 상자와 겹치는 부분은
// 상자보다 뒤에 있어 깊이 검사에서 떨어지고, 실루엣 바깥으로 삐져나온 테두리만 남는다.
// 그래서 테두리가 구가 아니라 상자 모양을 따른다 - 등급마다 메시가 바뀌어도 그대로 따라간다.
//
// 두 거리에서 다르게 읽힌다.
// - **멀리**: 아주 옅게 켜져 있다. 기획서 6.5.2의 "3등급 박스는 안개 너머에서도 희미하게
//   빛이 새어 나온다. 발광은 위치만 알려주며 내용은 알려주지 않는다" 가 이것이다.
// - **가까이**: 또렷해진다. 상자 앞에 섰을 때 형태가 분명해진다.
//
// 안개를 통과하는 이유는 큐가 Transparent라 안개 패스(BeforeRenderingTransparents) 뒤에
// 그려지기 때문이다. 깊이 검사는 씬 불투명 깊이에 대해서만 걸리므로, 나무 뒤에 있으면
// 가려지는 것이 맞다.

void BB_ItemBoxGlow_float(
    float3 PositionWS,
    float  FarAlpha,
    float  NearAlpha,
    float  NearDistance,
    float  FarDistance,
    out float Alpha)
{
    // 가까울수록 1. NearDistance 안쪽이면 최대로 또렷해진다.
    float distanceToCamera = distance(_WorldSpaceCameraPos, PositionWS);
    float nearness = 1.0 - saturate((distanceToCamera - NearDistance)
                                  / max(1e-4, FarDistance - NearDistance));

    Alpha = saturate(lerp(FarAlpha, NearAlpha, nearness));
}

#endif
