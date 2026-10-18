using UnityEngine;

/// 월드 오브젝트 위에 뜨는 표식(재료 배지·주문 말풍선)을 코드로 세운다.
/// 표식 프리팹을 소유자 프리팹에 박아 두면 소유자마다 UI 계층이 딸려 오고 높이가 오버라이드로 흩어진다.
/// 표식은 자기 소유자를 부모에서 찾으므로(`GetComponentInParent`) 이 오브젝트 밑에 만든다.
[DisallowMultipleComponent]
public sealed class WorldMarkerSpawner : MonoBehaviour
{
    [SerializeField] GameObject markerPrefab;
    /// 소유자 기준 표식 자리. 표식의 월드 위치가 된다 (`UIMatchHudScreen.PlaceMarker`).
    [SerializeField] Vector3 localOffset;

    void Awake()
    {
        if (markerPrefab == null)
        {
            CDebug.LogError($"{name}: markerPrefab이 비었다. 표식이 생기지 않는다.", this);
            return;
        }

        Instantiate(markerPrefab, transform).transform.localPosition = localOffset;
    }
}
