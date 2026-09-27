using UnityEngine;

/// 대시·피격 연출의 색·시간·흔들림 수치. 전 플레이어가 같은 값을 쓰므로 프리팹마다
/// 필드로 흩어 두지 않고 한 애셋이 쥔다 (`CharacterVisualConfig`·`ItemVisualConfig`와
/// 같은 자리 — 여러 화면·인스턴스가 공유하는 **표현 설정**).
///
/// **여기 담는 것은 값뿐이다.** 트윈·현재 파티클·플레이어 참조 같은 런타임 상태는 두지
/// 않는다 — 그런 상태는 인스턴스마다 다르고 이 애셋은 인스턴스 사이에서 공유되기 때문에,
/// 여기 담으면 한 플레이어의 대시가 다른 플레이어의 트윈을 밟는다. 그 몫은 여전히
/// `DashPresentation`(일반 C# 객체, 플레이어 인스턴스당 하나)이 진다.
[CreateAssetMenu(menuName = "Blood & Beans/플레이어 피드백", fileName = AssetName)]
public class PlayerFeedbackConfig : ScriptableObject
{
    public const string AssetName = "PlayerFeedbackConfig";

    [Header("피격 번쩍임")]
    public Color hitFlash = Color.white;

    /// 재료가 쏟아진 대시는 다른 색으로 번쩍인다. 밀리기만 한 것과 수확을 흘린 것은
    /// 맞은 사람에게 전혀 다른 사건인데, 화면에 차이가 없으면 안 된다 (기획서 6.6).
    public Color spillFlash = new(1f, 0.65f, 0.15f);
    public float flashSeconds = 0.16f;

    [Header("임팩트")]
    /// 맞은 자리에 터지는 연출의 크기. 프리팹 자체는 `EffectManager`의 표가 들고 있다.
    public float impactScale = 1f;

    [Header("화면 흔들림")]
    /// 맞은 쪽이 맞힌 쪽보다 세고, 재료를 흘렸으면 가장 세다.
    public float shakeOnLand = 0.10f;
    public float shakeOnTaken = 0.28f;
    public float shakeOnSpill = 0.45f;
}
