using UnityEngine;
using UnityEngine.AddressableAssets;

/// 캐릭터 종류가 화면에서 어떻게 생기는가. 종류마다 모델 프리팹(어드레서블 참조)과 배치
/// 보정을 한 애셋이 쥔다.
///
/// 캐릭터 선택창의 무대와 인게임 플레이어, 두 자리가 같은 표를 본다. 자리마다 배열을
/// 따로 두면 고른 모델과 인게임 모델이 어긋날 수 있고, FBX가 들어올 때 고칠 곳이 둘이
/// 된다. 값이 아니라 **표현 설정**이라 ScriptableObject가 맞다 (`ItemVisualConfig`와 같은 자리).
///
/// **이 애셋은 무엇을 어떻게 세울지만 안다. 세우는 것(로드·Instantiate·해제)은 여기 없다**
/// — 그 일은 `CharacterModelSpawner`가 `ResourceManager`를 통해 한다. 모델이 캐릭터 5종
/// 전부 무겁게 물려 있던 예전에는, 이 애셋 하나가 통째로 로드되는 순간(캐릭터 선택 화면을
/// 여는 즉시) 모델 5개가 전부 같이 로드됐다 — 실제로 보이는 것은 한 번에 최대 8명분뿐인데도.
///
/// FBX가 준비되면 여기 참조만 갈아 끼운다. 코드는 손대지 않는다.
[CreateAssetMenu(menuName = "Blood & Beans/캐릭터 표시", fileName = AssetName)]
public class CharacterVisualConfig : ScriptableObject
{
    public const string AssetName = "CharacterVisualConfig";

    /// 키가 배열 인덱스가 아니라 `CharacterId`인 이유는 카탈로그 순서가 바뀌어도 표가 밀리지 않게 하려는 것이다.
    [System.Serializable]
    public struct Entry
    {
        [Tooltip("저장 호환을 위한 외형 ID. 낮 스킬을 바꿔도 이 값은 유지한다.")]
        public CharacterId id;

        [Tooltip("FBX를 중첩한 프리팹의 어드레서블 참조. 모델·Animator·CharacterModel을 담고 게임 로직과 충돌체는 넣지 않는다.")]
        public AssetReference model;

        [Tooltip("선택창과 인게임에 공통 적용할 발 위치 보정.")]
        public Vector3 localPosition;
        [Tooltip("정면은 +Z. 임포트 모델의 방향을 보정한다.")]
        public Vector3 localEulerAngles;
        [Tooltip("공통 모델 크기 배율. 0은 기존 데이터 호환을 위해 1로 취급한다.")]
        [Min(0f)] public float scale;
    }

    [SerializeField] Entry[] entries;

    [Header("예외")]
    [Tooltip("모델이 아직 안 들어온 종류가 대신 세우는 것의 어드레서블 참조. 비면 그 자리에 아무것도 서지 않는다.")]
    [SerializeField] AssetReference placeholderModel;

    /// 이 종류가 무대에 세울 프리팹의 참조. 표에 없거나 빈 칸이면 대역 참조를 준다.
    /// 어느 쪽도 유효하지 않으면 null이다 — 호출자는 이때 아무것도 세우지 않는다.
    public AssetReference ReferenceFor(CharacterId id)
    {
        if (entries != null)
            for (var i = 0; i < entries.Length; i++)
                if (entries[i].id == id && entries[i].model != null && entries[i].model.RuntimeKeyIsValid())
                    return entries[i].model;

        return placeholderModel != null && placeholderModel.RuntimeKeyIsValid() ? placeholderModel : null;
    }

    /// 이미 세운 모델에 이 종류의 배치 보정을 입힌다. 표에 없는 종류(대역이 선 경우)는
    /// 건드리지 않는다 — 대역은 자기 프리팹의 기본 자세로 선다.
    public void ApplyTransform(CharacterId id, Transform model)
    {
        if (entries == null) return;
        foreach (var entry in entries)
        {
            if (entry.id != id || entry.model == null || !entry.model.RuntimeKeyIsValid()) continue;
            model.localPosition = entry.localPosition;
            model.localRotation = Quaternion.Euler(entry.localEulerAngles);
            model.localScale *= entry.scale > 0f ? entry.scale : 1f;
            return;
        }
    }
}
