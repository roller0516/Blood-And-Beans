#ifndef BB_FOLIAGE_WIND_INCLUDED
#define BB_FOLIAGE_WIND_INCLUDED

// 잎이 바람에 흔들리는 정점 변형. Shader Graph의 Custom Function 노드가 부른다.
//
// 규칙 셋만 지킨다.
// 1. **밑동은 고정한다.** 오브젝트 공간 높이로 마스크를 만들고 제곱해서, 뿌리 쪽은 거의
//    움직이지 않고 우듬지만 흔들린다. 마스크 없이 통째로 밀면 나무가 미끄러지는 것처럼 보인다.
// 2. **위상은 월드 좌표에서 뽑는다.** 이웃한 나무가 같은 박자로 흔들리면 숲 전체가 한 덩어리로
//    출렁인다. 월드 XZ를 위상에 넣으면 나무마다 다른 시점에 흔들린다.
// 3. **방향은 월드 기준이다.** 바람은 한 방향으로 분다. 나무마다 Y 회전이 무작위라
//    (`ForestMapBuilder`) 오브젝트 공간에서 밀면 나무마다 바람 방향이 달라진다.
//    그래서 월드 방향을 오브젝트 공간으로 되돌려 더한다.
//
// 정적 배칭(BatchingStatic)이 걸린 인스턴스는 오브젝트 공간이 곧 월드 공간이라
// `TransformWorldToObjectDir`가 항등이 된다. 두 경우 모두 결과가 같다.

void BB_FoliageWind_float(
    float3 PositionOS,
    float3 PositionWS,
    float  Time,
    float  Strength,
    float  Speed,
    float  Scale,
    float  SwayHeight,
    out float3 Position)
{
    Position = PositionOS;
    if (SwayHeight <= 0.0 || Strength <= 0.0) return;

    // 밑동 0, 우듬지 1. 제곱해서 아래쪽이 더 단단하게 붙어 있게 한다.
    float mask = saturate(PositionOS.y / SwayHeight);
    mask *= mask;

    float phase = (PositionWS.x + PositionWS.z) * Scale + Time * Speed;

    // 두 축의 주기를 어긋나게 해서 좌우 왕복이 아니라 느슨한 8자를 그리게 한다.
    float3 windWS = float3(sin(phase), 0.0, cos(phase * 0.7)) * (Strength * mask);

    Position = PositionOS + TransformWorldToObjectDir(windWS, false);
}

#endif
