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
        public DayPassive id;

        [Tooltip("FBX를 중첩한 프리팹. FBX 루트는 읽기 전용이라 직접 꽂을 수 없다.")]
        public GameObject model;

        [Tooltip("선택창 하단 스트립의 아이콘. 비면 카드가 글자만 보여 준다.")]
        public Sprite icon;
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

    /// 하단 스트립의 아이콘. 없으면 null이고 카드는 글자만 보여 준다.
    public Sprite IconFor(DayPassive id)
    {
        if (entries == null) return null;
        for (var i = 0; i < entries.Length; i++)
            if (entries[i].id == id) return entries[i].icon;

        return null;
    }
}
