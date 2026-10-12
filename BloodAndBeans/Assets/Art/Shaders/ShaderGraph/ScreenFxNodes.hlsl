#include "ShaderApiReflectionSupport.hlsl"

// 전체 화면 효과용 셰이더 그래프 노드. 반환값은 비네트가 쓰는 "중심까지의 거리"를 대신한다. ScreenEffectFeature가 넘기는 _EffectTime(연출 시각)·
// _EffectElapsed(켜진 뒤 지난 초)를 받는다.

/// <funchints>
///     <sg:ProviderKey>BB.ScreenFx.WavyEdge</sg:ProviderKey>
///     <sg:DisplayName>Wavy Edge</sg:DisplayName>
///     <sg:ReturnDisplayName>Distance</sg:ReturnDisplayName>
///     <sg:SearchCategory>Blood And Beans/Screen FX</sg:SearchCategory>
///     <sg:SearchTerms>Vignette, Border, Edge, Wave, Ripple, Screen</sg:SearchTerms>
/// </funchints>
/// <paramhints name="UV">
///     <UV/>
///     <Default>UV0</Default>
/// </paramhints>
/// <paramhints name="Distance">
///     <sg:DisplayName>Distance</sg:DisplayName>
///     <sg:Default>0</sg:Default>
/// </paramhints>
/// <paramhints name="Elapsed">
///     <sg:DisplayName>Elapsed</sg:DisplayName>
///     <sg:Default>1</sg:Default>
/// </paramhints>
/// <paramhints name="Time">
///     <sg:DisplayName>Time</sg:DisplayName>
///     <sg:Default>0</sg:Default>
/// </paramhints>
/// <paramhints name="Waves">
///     <sg:DisplayName>Wave Count</sg:DisplayName>
///     <sg:Default>7</sg:Default>
/// </paramhints>
/// <paramhints name="Amplitude">
///     <sg:DisplayName>Wave Amplitude</sg:DisplayName>
///     <sg:Range>0, 0.5</sg:Range>
///     <sg:Default>0.12</sg:Default>
/// </paramhints>
/// <paramhints name="Speed">
///     <sg:DisplayName>Wave Speed</sg:DisplayName>
///     <sg:Default>1.6</sg:Default>
/// </paramhints>
/// <paramhints name="Reach">
///     <sg:DisplayName>Ring Reach</sg:DisplayName>
///     <sg:Default>0.8</sg:Default>
/// </paramhints>
/// <paramhints name="Duration">
///     <sg:DisplayName>Ring Duration</sg:DisplayName>
///     <sg:Default>0.6</sg:Default>
/// </paramhints>
/// <paramhints name="Width">
///     <sg:DisplayName>Ring Width</sg:DisplayName>
///     <sg:Range>0.01, 0.5</sg:Range>
///     <sg:Default>0.08</sg:Default>
/// </paramhints>
UNITY_EXPORT_REFLECTION float WavyEdge(float2 UV, float Distance, float Elapsed, float Time, float Waves,
    float Amplitude, float Speed, float Reach, float Duration, float Width)
{
    // 화면 둘레를 따라 도는 각도. 정수 배수 사인만 겹쳐 0°와 360°가 이음매 없이 맞물린다.
    float aspect = _ScreenParams.x / _ScreenParams.y;
    float2 p = (UV - 0.5) * float2(aspect, 1);
    float angle = atan2(p.y, p.x);
    float k = max(1, round(Waves));
    float wave = sin(angle * k + Time * Speed) * 0.5
               + sin(angle * (k * 2 + 1) - Time * Speed * 1.7) * 0.3
               + sin(angle * (k * 3 + 2) + Time * Speed * 2.9) * 0.2;

    float wavy = Distance * (1 + wave * Amplitude);

    // 테두리와 같은 물결 거리로 얇은 고리를 만들고, 도착할 때만 테두리에 합쳐지며 옅어진다.
    float t = saturate(Elapsed / max(Duration, 0.001));
    float width = max(Width, 0.001);
    float reach = max(Reach, width);
    float head = t * reach;
    float ring = 1 - smoothstep(width * 0.5, width, abs(wavy - head));
    ring *= smoothstep(0, width, head) * (1 - smoothstep(reach - width, reach, head));

    // 거리 덧셈으로 고리 주변까지 붉게 채우지 않고 고리와 테두리를 겹친다.
    return max(wavy, ring);
}

/// <funchints>
///     <sg:ProviderKey>BB.ScreenFx.Tendrils</sg:ProviderKey>
///     <sg:DisplayName>Tendrils</sg:DisplayName>
///     <sg:ReturnDisplayName>Alpha</sg:ReturnDisplayName>
///     <sg:SearchCategory>Blood And Beans/Screen FX</sg:SearchCategory>
///     <sg:SearchTerms>Tendril, Vein, Noise, Spread, Screen</sg:SearchTerms>
/// </funchints>
/// <paramhints name="Noise">
///     <sg:DisplayName>Noise</sg:DisplayName>
/// </paramhints>
/// <paramhints name="UV">
///     <UV/>
///     <Default>UV0</Default>
/// </paramhints>
/// <paramhints name="Base">
///     <sg:DisplayName>Base Alpha</sg:DisplayName>
///     <sg:Default>0</sg:Default>
/// </paramhints>
/// <paramhints name="Elapsed">
///     <sg:DisplayName>Elapsed</sg:DisplayName>
///     <sg:Default>0.3</sg:Default>
/// </paramhints>
/// <paramhints name="Time">
///     <sg:DisplayName>Time</sg:DisplayName>
///     <sg:Default>0</sg:Default>
/// </paramhints>
/// <paramhints name="Angular">
///     <sg:DisplayName>Around Tiling</sg:DisplayName>
///     <sg:Default>3</sg:Default>
/// </paramhints>
/// <paramhints name="Radial">
///     <sg:DisplayName>Outward Tiling</sg:DisplayName>
///     <sg:Default>0.6</sg:Default>
/// </paramhints>
/// <paramhints name="Curl">
///     <sg:DisplayName>Curl</sg:DisplayName>
///     <sg:Range>0, 1</sg:Range>
///     <sg:Default>0.25</sg:Default>
/// </paramhints>
/// <paramhints name="Threshold">
///     <sg:DisplayName>Line Threshold</sg:DisplayName>
///     <sg:Range>0, 1</sg:Range>
///     <sg:Default>0.9</sg:Default>
/// </paramhints>
/// <paramhints name="Softness">
///     <sg:DisplayName>Line Softness</sg:DisplayName>
///     <sg:Range>0.001, 0.5</sg:Range>
///     <sg:Default>0.04</sg:Default>
/// </paramhints>
/// <paramhints name="Flow">
///     <sg:DisplayName>Flow Speed</sg:DisplayName>
///     <sg:Default>0.1</sg:Default>
/// </paramhints>
/// <paramhints name="Inner">
///     <sg:DisplayName>Clear Center</sg:DisplayName>
///     <sg:Range>0, 1</sg:Range>
///     <sg:Default>0.08</sg:Default>
/// </paramhints>
/// <paramhints name="Reach">
///     <sg:DisplayName>Reach</sg:DisplayName>
///     <sg:Default>1</sg:Default>
/// </paramhints>
/// <paramhints name="Duration">
///     <sg:DisplayName>Grow Duration</sg:DisplayName>
///     <sg:Default>0.45</sg:Default>
/// </paramhints>
/// <paramhints name="Fade">
///     <sg:DisplayName>Fade Duration</sg:DisplayName>
///     <sg:Default>0.5</sg:Default>
/// </paramhints>
/// <paramhints name="Vein">
///     <sg:DisplayName>Vein Mask</sg:DisplayName>
/// </paramhints>
UNITY_EXPORT_REFLECTION float Tendrils(UnityTexture2D Noise, float2 UV, float Base, float Elapsed, float Time,
    float Angular, float Radial, float Curl, float Threshold, float Softness, float Flow, float Inner,
    float Reach, float Duration, float Fade, out float Vein)
{
    // 반경은 중심 0 → 모서리 1, 둘레는 0 → 1.
    float aspect = _ScreenParams.x / _ScreenParams.y;
    float2 p = (UV - 0.5) * float2(aspect, 1);
    float r = length(p) / (0.5 * length(float2(aspect, 1)));
    float around = atan2(p.y, p.x) / 6.2831853 + 0.5;

    // 둘레 반복 수는 정수여야 한 바퀴 돌았을 때 이음매가 없다. 이음매에서 밉맵이 튀지 않게 0레벨로 읽는다.
    float tiles = max(1, round(Angular));
    float warp = SAMPLE_TEXTURE2D_LOD(Noise.tex, Noise.samplerstate, float2(around * 2, r * 0.8 + Time * 0.05), 0).r - 0.5;
    float2 uv = float2((around + warp * Curl * r) * tiles, r * Radial - Time * Flow);
    float n = SAMPLE_TEXTURE2D_LOD(Noise.tex, Noise.samplerstate, uv, 0).r;

    // 노이즈 등고선(0.5 근처)을 가는 선으로 뽑는다. 바깥으로 길게 늘려 읽어서 줄기가 방사형으로 선다.
    float ridge = 1 - abs(n * 2 - 1);
    float vein = smoothstep(Threshold, Threshold + Softness, ridge);

    // 가운데 근처에서 바깥으로 자라고, 다 자란 뒤 옅어진다.
    float grow = saturate(Elapsed / max(Duration, 0.001));
    float head = Inner + (1 - (1 - grow) * (1 - grow)) * Reach;
    float mask = smoothstep(head, head - 0.1, r) * smoothstep(Inner, Inner + 0.08, r);
    float life = 1 - saturate((Elapsed - Duration) / max(Fade, 0.001));

    // 줄기 자리만 따로 내보내 색을 줄기 전용 색으로 칠할 수 있게 한다.
    Vein = vein * mask * life;
    return max(Base, Vein);
}


/// <funchints>
///     <sg:ProviderKey>BB.ScreenFx.Streaks</sg:ProviderKey>
///     <sg:DisplayName>Streaks</sg:DisplayName>
///     <sg:ReturnDisplayName>Alpha</sg:ReturnDisplayName>
///     <sg:SearchCategory>Blood And Beans/Screen FX</sg:SearchCategory>
///     <sg:SearchTerms>Streak, Wisp, Wind, Swirl, Screen</sg:SearchTerms>
/// </funchints>
/// <paramhints name="Noise">
///     <sg:DisplayName>Noise</sg:DisplayName>
/// </paramhints>
/// <paramhints name="UV">
///     <UV/>
///     <Default>UV0</Default>
/// </paramhints>
/// <paramhints name="Base">
///     <sg:DisplayName>Base Alpha</sg:DisplayName>
///     <sg:Default>0</sg:Default>
/// </paramhints>
/// <paramhints name="Elapsed">
///     <sg:DisplayName>Elapsed</sg:DisplayName>
///     <sg:Default>0.4</sg:Default>
/// </paramhints>
/// <paramhints name="Time">
///     <sg:DisplayName>Time</sg:DisplayName>
///     <sg:Default>0</sg:Default>
/// </paramhints>
/// <paramhints name="Count">
///     <sg:DisplayName>Count</sg:DisplayName>
///     <sg:Range>1, 12</sg:Range>
///     <sg:Default>7</sg:Default>
/// </paramhints>
/// <paramhints name="Inner">
///     <sg:DisplayName>Inner Radius</sg:DisplayName>
///     <sg:Range>0, 1</sg:Range>
///     <sg:Default>0.12</sg:Default>
/// </paramhints>
/// <paramhints name="Length">
///     <sg:DisplayName>Length</sg:DisplayName>
///     <sg:Default>0.45</sg:Default>
/// </paramhints>
/// <paramhints name="Width">
///     <sg:DisplayName>Width</sg:DisplayName>
///     <sg:Range>0.005, 0.3</sg:Range>
///     <sg:Default>0.035</sg:Default>
/// </paramhints>
/// <paramhints name="Twist">
///     <sg:DisplayName>Twist</sg:DisplayName>
///     <sg:Default>2.5</sg:Default>
/// </paramhints>
/// <paramhints name="Drift">
///     <sg:DisplayName>Drift</sg:DisplayName>
///     <sg:Default>0.25</sg:Default>
/// </paramhints>
/// <paramhints name="Flow">
///     <sg:DisplayName>Flow Speed</sg:DisplayName>
///     <sg:Default>0.8</sg:Default>
/// </paramhints>
/// <paramhints name="Breakup">
///     <sg:DisplayName>Wispiness</sg:DisplayName>
///     <sg:Range>0, 1</sg:Range>
///     <sg:Default>0.5</sg:Default>
/// </paramhints>
/// <paramhints name="Intensity">
///     <sg:DisplayName>Intensity</sg:DisplayName>
///     <sg:Range>0, 1</sg:Range>
///     <sg:Default>0.7</sg:Default>
/// </paramhints>
/// <paramhints name="FadeIn">
///     <sg:DisplayName>Fade In</sg:DisplayName>
///     <sg:Default>0.15</sg:Default>
/// </paramhints>
/// <paramhints name="Duration">
///     <sg:DisplayName>Hold</sg:DisplayName>
///     <sg:Default>0.5</sg:Default>
/// </paramhints>
/// <paramhints name="Fade">
///     <sg:DisplayName>Fade Out</sg:DisplayName>
///     <sg:Default>0.4</sg:Default>
/// </paramhints>
/// <paramhints name="Mask">
///     <sg:DisplayName>Streak Mask</sg:DisplayName>
/// </paramhints>
UNITY_EXPORT_REFLECTION float Streaks(UnityTexture2D Noise, float2 UV, float Base, float Elapsed, float Time,
    float Count, float Inner, float Length, float Width, float Twist, float Drift, float Flow, float Breakup,
    float Intensity, float FadeIn, float Duration, float Fade, out float Mask)
{
    // 반경은 중심 0 → 모서리 1. 각도는 라디안.
    float aspect = _ScreenParams.x / _ScreenParams.y;
    float2 p = (UV - 0.5) * float2(aspect, 1);
    float r = length(p) / (0.5 * length(float2(aspect, 1)));
    float angle = atan2(p.y, p.x);

    float best = 0;
    int count = (int)clamp(round(Count), 1, 12);

    // 화면 가장자리 쪽에 연기 같은 바람결이 생겨 그 자리에서 결이 흐르고, 전체가 천천히 돈다.
    [loop] for (int i = 0; i < count; i++)
    {
        float h1 = frac(sin(i * 127.1 + 3.7) * 43758.5453);
        float h2 = frac(sin(i * 311.7 + 17.3) * 43758.5453);
        float h3 = frac(sin(i * 74.7 + 5.1) * 43758.5453);

        // 줄기마다 조금씩 늦게 나타났다 사라진다.
        float t = Elapsed - h3 * 0.12;
        float life = smoothstep(0, max(FadeIn, 0.001), t) * (1 - saturate((t - FadeIn - Duration) / max(Fade, 0.001)));
        if (life <= 0) continue;

        // 안쪽 끝(r0)에서 바깥으로 Length만큼 뻗는다. 화면 밖으로 넘어가도 된다.
        float r0 = lerp(Inner, Inner + 0.35, h1);
        float along = (r - r0) / max(Length * lerp(0.7, 1.3, h2), 0.001);
        if (along < 0 || along > 1) continue;

        // 중심을 향해 비스듬히 휘는 경로. Drift로 시간이 지나며 천천히 돈다.
        float center = (i + h2 * 0.7) / count * 6.2831853 + Twist * (r - r0) + Drift * Elapsed;
        float d = abs(atan2(sin(angle - center), cos(angle - center))) * r;
        float width = Width * lerp(0.7, 1.3, h3) * (0.6 + r);
        float across = exp(-(d / width) * (d / width));
        float ends = smoothstep(0, 0.3, along) * smoothstep(1, 0.55, along);

        // 노이즈를 길이 방향으로 늘려 읽고 결을 흘려 연기처럼 거칠게 만든다.
        float2 nuv = float2(along * 1.2 - Time * Flow + i * 0.37, d / width * 0.25 + h1);
        float grain = SAMPLE_TEXTURE2D_LOD(Noise.tex, Noise.samplerstate, nuv, 0).r;
        float wisp = lerp(1, saturate(grain * 2 - 0.3), Breakup);

        best = max(best, across * ends * wisp * life);
    }
    // Mask는 색 섞기 비율이다. 최종 알파 중 줄기 몫으로 내보내야 옅은 가장자리에 테두리 색이 끼지 않는다.
    float streak = saturate(best * Intensity);
    float alpha = max(Base, streak);
    Mask = streak / max(alpha, 0.0001);
    return alpha;
}
