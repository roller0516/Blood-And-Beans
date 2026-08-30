#ifndef BB_ITEM_BOX_GLOW_INCLUDED
#define BB_ITEM_BOX_GLOW_INCLUDED

// 3등급 상자의 발광 아웃라인. Shader Graph의 Custom Function 노드가 부른다.
//
// 상자 메시를 살짝 키워 **뒷면만** 그리는 고전적인 아웃라인 헐이다. 상자와 겹치는 부분은
// 상자보다 뒤에 있어 깊이 검사에서 떨어지고, 실루엣 바깥으로 삐져나온 테두리만 남는다.
// 그래서 테두리가 구가 아니라 상자 모양을 따른다 - 등급마다 메시가 바뀌어도 그대로 따라간다.
//
// 두 거리에서 다르게 읽힌다. 기획서 6.5.2가 발광에 요구하는 것은 두 가지고, 둘 다
// *멀리서* 걸린다.
// - "등급은 **원거리에서** 형태·재질·색·발광으로 구분된다"
// - "3등급 박스는 안개 너머에서도 **희미하게** 빛이 새어 나온다"
//
// 그래서 멀어질수록 옅어지되 **꺼지지는 않는다**. 원거리 알파(`FarAlpha`)는 감쇠의
// 끝값이 아니라 식별의 바닥값이다 - 여기를 낮추면 "희미하게"가 아니라 "안 보인다"가
// 되고, 그 순간 위 두 줄이 동시에 깨진다. 가까이서 또렷해지는 쪽은 기획서에 근거가
// 없는 연출 선택이다.
//
// 발광 강도 자체는 기획서 14장 미결 2-c("3등급 박스의 안개 투과 발광 강도")다. 지금
// 값은 확정치가 아니라 눈으로 맞춘 임시값이며, 머티리얼에서 조정한다.
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
