using UnityEngine;

/// 캐릭터 종류가 화면에서 어떻게 생겼는가. 종류마다 모델 프리팹과 아이콘을 한 애셋이 쥔다.
///
/// 캐릭터 선택창의 무대와 인게임 플레이어, 두 자리가 같은 표를 본다. 자리마다 배열을
/// 따로 두면 고른 오리와 인게임 오리가 어긋날 수 있고, FBX가 들어올 때 고칠 곳이 둘이
/// 된다. 값이 아니라 **표현 설정**이라 ScriptableObject가 맞다 (`ItemVisualConfig`와 같은 자리).
///
/// FBX가 준비되면 여기 프리팹만 갈아 끼운다. 코드는 손대지 않는다.
[CreateAssetMenu(menuName = "Blood & Beans/캐릭터 표시", fileName = AssetName)]
public class CharacterVisualConfig : ScriptableObject
{
    public const string AssetName = "CharacterVisualConfig";

    /// 키가 `DayPassive`인 이유는 `CharacterCatalog.All`의 배열 인덱스가 흔들리기 때문이다
    /// (종 수가 기획서 14장 #10 미결). 인덱스로 짝지으면 카탈로그에 한 줄 끼워 넣는 순간
    /// 표 전체가 한 칸씩 밀린다. 낮 패시브는 종류와 1:1이라 안정적인 식별자다.
    [System.Serializable]
    public struct Entry
    {
        [Tooltip("저장 호환을 위한 외형 ID. 낮 스킬을 바꿔도 이 값은 유지한다.")]
        public DayPassive id;

        [Tooltip("FBX를 중첩한 프리팹. 모델·Animator·CharacterModel을 담고 게임 로직과 충돌체는 넣지 않는다.")]
        public GameObject model;

        [Tooltip("선택창 하단 스트립의 아이콘. 비면 카드가 글자만 보여 준다.")]
        public Sprite icon;

        [Tooltip("선택창과 인게임에 공통 적용할 발 위치 보정.")]
        public Vector3 localPosition;
        [Tooltip("정면은 +Z. 임포트 모델의 방향을 보정한다.")]
        public Vector3 localEulerAngles;
        [Tooltip("공통 모델 크기 배율. 0은 기존 데이터 호환을 위해 1로 취급한다.")]
        [Min(0f)] public float scale;
    }

    [SerializeField] Entry[] entries;

    [Header("예외")]
    [Tooltip("모델이 아직 안 들어온 종류가 대신 세우는 것. 비면 그 자리에 아무것도 서지 않는다.")]
    [SerializeField] GameObject placeholderModel;

    /// 이 종류가 무대에 세울 프리팹. 표에 없거나 빈 칸이면 대역이 선다.
    public GameObject ModelFor(DayPassive id)
    {
        if (entries != null)
            for (var i = 0; i < entries.Length; i++)
                if (entries[i].id == id && entries[i].model != null) return entries[i].model;

        return placeholderModel;
    }

    /// 양쪽 화면이 같은 보정과 레이어 처리를 쓴다. 모델 교체가 게임 규칙에 닿지 않는다.
    public GameObject SpawnModel(DayPassive id, Transform parent, int layer)
    {
        var prefab = ModelFor(id);
        if (prefab == null || parent == null) return null;
        var model = Instantiate(prefab, parent);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        if (entries != null)
            foreach (var entry in entries)
            {
                if (entry.id != id || entry.model == null) continue;
                model.transform.localPosition = entry.localPosition;
                model.transform.localRotation = Quaternion.Euler(entry.localEulerAngles);
                model.transform.localScale *= entry.scale > 0f ? entry.scale : 1f;
                break;
            }
        foreach (var child in model.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;
        return model;
    }
    /// 하단 스트립의 아이콘. 없으면 null이고 카드는 글자만 보여 준다.
    public Sprite IconFor(DayPassive id)
    {
        if (entries == null) return null;
        for (var i = 0; i < entries.Length; i++)
            if (entries[i].id == id) return entries[i].icon;

        return null;
    }
}
