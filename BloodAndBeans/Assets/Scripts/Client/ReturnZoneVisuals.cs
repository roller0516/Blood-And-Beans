using UnityEngine;

/// 귀환 판정의 반경을 그대로 읽어 마법진 크기를 맞춘다.
public sealed class ReturnZoneVisuals : MonoBehaviour
{
    [SerializeField] ReturnZone zone;
    [SerializeField] Transform circle;

    void Start()
    {
        if (zone == null || circle == null)
        {
            //CDebug.LogError($"{name}: 귀환 마법진 참조가 비어 있다.", this);
            return;
        }
        var scale = transform.lossyScale;
        circle.localScale = new Vector3(zone.Radius / Mathf.Max(Mathf.Abs(scale.x), Mathf.Epsilon),
            1f / Mathf.Max(Mathf.Abs(scale.y), Mathf.Epsilon),
            zone.Radius / Mathf.Max(Mathf.Abs(scale.z), Mathf.Epsilon));
    }
}
