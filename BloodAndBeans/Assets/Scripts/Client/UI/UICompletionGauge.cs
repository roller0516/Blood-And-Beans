using TMPro;
using UnityEngine;

/// HUD 오버레이에서 로컬 플레이어가 다루는 광장 설비의 완성 게이지를 표시한다 (기획서 5.2·5.4.1).
public sealed class UICompletionGauge : MonoBehaviour
{
    [SerializeField] Canvas canvas;
    [SerializeField] RectTransform bar;
    [SerializeField] RectTransform good;
    [SerializeField] RectTransform perfect;
    [SerializeField] RectTransform needle;
    [SerializeField] TMP_Text label;
    [SerializeField] UnityEngine.UI.Image progress;
    [SerializeField] UICraftingJudgement judgement;
    int lastTenths = -1;
    bool lastTarget;
    CompletionGauge soundingGauge;
    int soundingSweep = -1;
    void Awake() => canvas.enabled = false;
    void OnEnable() => Station.JudgementShown += OnJudgement;
    void OnDisable()
    {
        Station.JudgementShown -= OnJudgement;
        judgement?.Clear();
    }
    void OnJudgement(Station station, Judgement result, int team)
    {
        var director = MatchDirector.Instance;
        if (judgement == null || station == null || team < 0 || team != PlayerTeam.Local() ||
            director == null || director.Phase.Current != Phase.Day) return;
        judgement.PlayAt(result, station.FacilityPosition);
    }
    void LateUpdate()
    {
        var gauge = Pick();
        var day = MatchDirector.Instance != null && MatchDirector.Instance.Phase.Current == Phase.Day;
        if (!day) judgement?.Clear();
        judgement?.RefreshPosition();
        canvas.enabled = gauge != null || (judgement != null && judgement.IsPlaying);
        bar.gameObject.SetActive(gauge != null);
        if (gauge == null) { soundingGauge = null; soundingSweep = -1; lastTenths = -1; return; }
        var cooking = gauge.Station.State == StationState.Cooking;
        good.gameObject.SetActive(!cooking);
        perfect.gameObject.SetActive(!cooking);
        needle.gameObject.SetActive(!cooking);
        if (progress != null)
        {
            progress.gameObject.SetActive(cooking);
            progress.fillAmount = gauge.Station.CookProgress;
        }
        if (cooking) { label.text = gauge.Station is Oven ? "굽는 중" : "추출 중"; lastTenths = -1; soundingSweep = -1; return; }
        var sweep = gauge.Sweep;
        if (soundingGauge == gauge && soundingSweep >= 0 && sweep > soundingSweep)
            SoundManager.Instance?.PlayCue(SfxCue.GaugeTick, gauge.Station.FacilityPosition, gauge.TeamId);
        soundingGauge = gauge;
        soundingSweep = sweep;
        var width = bar.rect.width;
        SetWidth(good, width * gauge.GoodHalfWidth * 2f);
        SetWidth(perfect, width * gauge.PerfectHalfWidth * 2f);
        needle.anchoredPosition = new Vector2((gauge.Needle - 0.5f) * width, 0f);
        var target = CompletionGauge.LocalTarget() == gauge;
        var tenths = Mathf.CeilToInt(gauge.Remaining * 10f);
        if (tenths == lastTenths && target == lastTarget) return;
        lastTenths = tenths; lastTarget = target;
        label.text = $"{(target ? "F · " : "대기 · ")}{tenths * 0.1f:0.0}초";
    }
    /// 내가 조작 중인 설비(굽는 중 포함)가 먼저, 없으면 F가 멈출 게이지다. 후보는 캐시된 광장 게이지뿐이다.
    static CompletionGauge Pick()
    {
        var director = MatchDirector.Instance;
        var manager = Unity.Netcode.NetworkManager.Singleton;
        var team = PlayerTeam.Local();
        if (director == null || manager == null || team < 0) return null;
        foreach (var g in director.PlazaGauges)
            if (g != null && g.IsSpawned && g.Station != null && g.TeamId == team && g.IsDay &&
                g.Station.OperatorId == manager.LocalClientId &&
                (g.Active || g.Station.State == StationState.Cooking))
                return g;
        return CompletionGauge.LocalTarget();
    }
    static void SetWidth(RectTransform target, float width)
    {
        if (!Mathf.Approximately(target.sizeDelta.x, width)) target.sizeDelta = new Vector2(width, target.sizeDelta.y);
    }
}
