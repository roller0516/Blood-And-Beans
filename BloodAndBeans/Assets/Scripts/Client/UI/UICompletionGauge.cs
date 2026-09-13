using TMPro;
using UnityEngine;

/// 자기 팀이 점유한 광장 설비마다 완성 게이지를 표시한다 (기획서 5.2·5.4.1).
public sealed class UICompletionGauge : MonoBehaviour
{
    [SerializeField] Canvas canvas;
    [SerializeField] RectTransform bar;
    [SerializeField] RectTransform good;
    [SerializeField] RectTransform perfect;
    [SerializeField] RectTransform needle;
    [SerializeField] TMP_Text label;
    [SerializeField] UnityEngine.UI.Image progress;
    [SerializeField] Vector3 offset = new(0f, 2.8f, 0f);
    CompletionGauge gauge;
    Cafe cafe;
    Camera cameraView;
    PlayerCharacter character;
    Unity.Netcode.NetworkObject localPlayer;
    int lastTenths = -1;
    bool lastTarget;
    void Awake()
    {
        gauge = GetComponentInParent<CompletionGauge>();
        cafe = Cafe.Of(this);
        canvas.enabled = false;
    }
    void LateUpdate()
    {
        var manager = Unity.Netcode.NetworkManager.Singleton;
        var visible = manager != null && gauge != null && gauge.IsSpawned && gauge.Station != null &&
            (gauge.Active || gauge.Station.State == StationState.Cooking) &&
            (gauge.Station.OperatorId == manager.LocalClientId || CompletionGauge.LocalTarget() == gauge) && cafe != null &&
            cafe.TeamId == PlayerTeam.Local() && cafe.Director != null && cafe.Director.Phase.Current == Phase.Day;
        canvas.enabled = visible;
        if (!visible) return;
        if (cameraView == null) cameraView = Camera.main;
        var player = manager != null && manager.LocalClient != null ? manager.LocalClient.PlayerObject : null;
        transform.position = (player != null ? Vector3.Lerp(player.transform.position, gauge.Station.FacilityPosition, 0.5f) : gauge.Station.FacilityPosition) + offset;
        if (cameraView != null) transform.forward = cameraView.transform.forward;
        if (player != localPlayer) { localPlayer = player; character = player != null ? player.GetComponent<PlayerCharacter>() : null; }
        var cooking = gauge.Station.State == StationState.Cooking;
        good.gameObject.SetActive(!cooking);
        perfect.gameObject.SetActive(!cooking);
        needle.gameObject.SetActive(!cooking);
        if (progress != null)
        {
            progress.gameObject.SetActive(cooking);
            progress.fillAmount = gauge.Station.CookProgress;
        }
        if (cooking) { label.text = gauge.Station is Oven ? "굽는 중" : "추출 중"; lastTenths = -1; return; }
        var width = bar.rect.width;
        SetWidth(good, width * gauge.GoodHalfWidth * 2f);
        SetWidth(perfect, width * gauge.PerfectHalfWidth * 2f * (character != null && character.AffectedBy(2) ? 0.5f : 1f));
        needle.anchoredPosition = new Vector2((gauge.Needle - 0.5f) * width, 0f);
        var target = CompletionGauge.LocalTarget() == gauge;
        var tenths = Mathf.CeilToInt(gauge.Remaining * 10f);
        if (tenths == lastTenths && target == lastTarget) return;
        lastTenths = tenths; lastTarget = target;
        label.text = $"{(target ? "F · " : "대기 · ")}{tenths * 0.1f:0.0}초";
    }
    static void SetWidth(RectTransform target, float width)
    {
        if (!Mathf.Approximately(target.sizeDelta.x, width)) target.sizeDelta = new Vector2(width, target.sizeDelta.y);
    }
}
