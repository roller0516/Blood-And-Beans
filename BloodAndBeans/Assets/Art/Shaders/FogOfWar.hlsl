#ifndef BB_FOG_OF_WAR_INCLUDED
#define BB_FOG_OF_WAR_INCLUDED

// 안개 계산 본체. Shader Graph의 Custom Function 노드가 이 파일의 BB_FogOfWar_float을 부른다.
//
// 그래프 노드로 풀지 않은 이유는 아래 9탭 밉 블러다. Sample Texture 2D LOD 노드 9개와
// 곱셈·덧셈 노드를 늘어놓으면 25노드가 넘고, 탭 간격 하나가 틀리면 어디가 틀렸는지
// 그래프에서 읽어낼 수 없다. 이 수식은 이미 실제 렌더 이미지로 두 번 잡아 고친 것이라
// (인수인계_2026-08-27) 글자 그대로 옮긴다. 그래프에는 이 노드 하나만 있고, 색 보정처럼
// 나중에 붙일 것은 노드로 이어 붙이면 된다.
//
// 예전 구현은 지면 높이 평면 한 장에 그렸다. 그래서 평면보다 위에 있는 것(나무·건물)은
// 깊이 검사를 이겨서 안개를 뚫고 보였다. 여기서는 깊이 버퍼로 화면 픽셀의 월드 위치를
// 복원해 그 자리의 걷힘 여부를 읽으므로, 오브젝트 높이와 카메라 투영에 영향받지 않는다.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

/// 안개를 칠하기 전의 화면. Full Screen Pass Renderer Feature는 기존 색 위에 블렌드하지
/// 않고 새 타깃에 그리므로, 셰이더가 직접 합성해야 한다. 이걸 빼면 화면이 통째로 안개색
/// (알파가 0이면 초기화되지 않은 흰색)으로 덮인다.
/// URP의 "URP Sample Buffer / Blit Source" 노드가 쓰는 것과 같은 경로다
/// (UniversalSampleBufferNode.cs: LOAD_TEXTURE2D_X_LOD(_BlitTexture, pixelCoords, 0)).
/// Renderer Feature의 Fetch Color Buffer가 켜져 있어야 이 텍스처가 채워진다.
TEXTURE2D_X(_BlitTexture);

TEXTURE2D(_BB_FogMask);
SAMPLER(sampler_BB_FogMask);

float4 _BB_FogColor;
float  _BB_FogSoftness;
float  _BB_FogBlur;

// xy = 마스크 텍셀 크기(1/Side). 밉 탭 간격을 재는 데만 쓴다.
float4 _BB_FogTexel;

// 월드 XZ → 마스크 UV. uv = world.xz * xy + zw.
// FogOfWar.CellIndex와 같은 식이어야 한다: cell = floor(world/cellSize) + halfCells.
float4 _BB_FogWorldToUV;

float BB_FogMaskSample(float2 uv, float lod)
{
    return SAMPLE_TEXTURE2D_LOD(_BB_FogMask, sampler_BB_FogMask, uv, lod).r;
}

void BB_FogOfWar_float(float2 ScreenUV, out float3 Color, out float Alpha)
{
    // 합성은 여기서 끝낸다. 출력 알파는 항상 1이다 — 이 패스가 화면을 그대로 대체한다.
    float3 scene = LOAD_TEXTURE2D_X_LOD(_BlitTexture, uint2(ScreenUV * _ScreenSize.xy), 0).rgb;
    Alpha = 1.0;

    float rawDepth = SampleSceneDepth(ScreenUV);

    // 아무것도 그려지지 않은 픽셀(하늘)은 월드 위치가 무한대로 나온다. 복원값을 그대로 쓰면
    // 마스크 가장자리 텍셀을 집어서 하늘이 걷힌 것처럼 보인다.
#if UNITY_REVERSED_Z
    bool isSky = rawDepth <= 0.0;
#else
    bool isSky = rawDepth >= 1.0;
#endif

    float3 worldPos = ComputeWorldSpacePosition(ScreenUV, rawDepth, UNITY_MATRIX_I_VP);
    float2 uv = worldPos.xz * _BB_FogWorldToUV.xy + _BB_FogWorldToUV.zw;

    // 격자 밖은 걷힌 적이 없다. 샘플러가 Clamp라 그냥 두면 가장자리 텍셀이 밖으로 번진다.
    bool outside = any(uv < 0.0) || any(uv > 1.0);

    // 마스크는 셀당 텍셀 하나뿐이라 바이리니어만으로는 경계가 사각형으로 보인다.
    // 넓은 평균은 밉맵 피라미드가 만들고, 그 밉의 텍셀 간격으로 찍는 3x3 가우시안 탭이
    // 박스 필터가 남기는 사각 블록을 지운다. 간격은 그 밉 텍셀의 절반이다 — 탭이 서로
    // 겹쳐야 박스가 텐트가 된다.
    float2 texel = _BB_FogTexel.xy * exp2(_BB_FogBlur) * 0.5;

    float m = BB_FogMaskSample(uv, _BB_FogBlur) * 4.0;
    m += BB_FogMaskSample(uv + float2(-texel.x, 0.0), _BB_FogBlur) * 2.0;
    m += BB_FogMaskSample(uv + float2( texel.x, 0.0), _BB_FogBlur) * 2.0;
    m += BB_FogMaskSample(uv + float2(0.0, -texel.y), _BB_FogBlur) * 2.0;
    m += BB_FogMaskSample(uv + float2(0.0,  texel.y), _BB_FogBlur) * 2.0;
    m += BB_FogMaskSample(uv + float2(-texel.x, -texel.y), _BB_FogBlur);
    m += BB_FogMaskSample(uv + float2( texel.x, -texel.y), _BB_FogBlur);
    m += BB_FogMaskSample(uv + float2(-texel.x,  texel.y), _BB_FogBlur);
    m += BB_FogMaskSample(uv + float2( texel.x,  texel.y), _BB_FogBlur);
    m *= 0.0625;   // 1/16

    // 하드 클램프가 아니라 smoothstep이다. 딱 잘라내면 셀 격자의 다각형 윤곽이 드러난다.
    float revealed = smoothstep(0.5 - _BB_FogSoftness, 0.5 + _BB_FogSoftness, m);
    if (isSky || outside) revealed = 0.0;

    Color = lerp(scene, _BB_FogColor.rgb, _BB_FogColor.a * (1.0 - revealed));
}

#endif
