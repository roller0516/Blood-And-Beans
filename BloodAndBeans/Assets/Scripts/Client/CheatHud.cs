using Unity.Netcode;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 개발용 팀 배정 치트. 좌상단 개발 열에서 `NetworkHud` 바로 아래에 붙는다.
///
/// 팀 명단은 서버 권위라서 서버(호스트)에서만 뜻이 있다. 클라이언트에서는 잠긴다 —
/// 클라이언트가 자기 화면에서 눌러 봐야 서버의 배정은 그대로이고, 그걸 모른 채
/// 테스트하면 없는 결함을 쫓게 된다.
///
/// 두 가지를 만진다.
/// - 팀 수: 카페가 서버 기동 때 한 번만 스폰되므로 접속을 끊은 상태에서만 바뀐다.
/// - 다음 입장자의 팀: 지금 붙어 있는 사람은 그대로 두고 다음 접속부터 적용된다.
public class CheatHud : MonoBehaviour
{
    [Header("레이아웃")]
    /// `NetworkHud` 패널(높이 180 + 위쪽 여백 12) 아래에서 시작한다. 두 HUD가 캔버스를
    /// 따로 쓰기 때문에 레이아웃이 자동으로 비켜 주지 않는다.
    [SerializeField] Vector2 anchoredPosition = new(12f, -204f);
    [SerializeField] Vector2 size = new(180f, 100f);
    [SerializeField] int sortingOrder = 21;

    /// 라벨 갱신 주기. 매 프레임 문자열을 만들 이유가 없다.
    [SerializeField] float refreshInterval = 0.25f;

    MatchSeating seating;
    Button teamCountButton;
    Button seatButton;
    TMP_Text teamCountLabel;
    TMP_Text seatLabel;
    TMP_Text status;
    float nextRefresh;

    // 마지막으로 그린 값. 바뀌지 않았으면 문자열을 새로 만들지 않는다.
    int shownTeamCount = -1;
    int shownForcedSeat = int.MinValue;
    int shownTeam = int.MinValue;
    bool shownListening;
    bool shownServer;

    void Start()
    {
        // 좌석 권위는 런처 씬에 있다. 이 HUD도 런처에 붙어 있어 같은 씬에서 찾는다.
        seating = MatchSeating.Instance;
        if (seating == null)
        {
            Debug.LogError($"{name}: 씬에 {nameof(MatchSeating)}가 없다. 치트 HUD를 띄우지 않는다.", this);
            enabled = false;
            return;
        }

        DevHud.EnsureEventSystem();
        var panel = DevHud.MakePanel(transform, "Cheat HUD", sortingOrder, anchoredPosition, size);

        status = DevHud.MakeText(panel, string.Empty);
        teamCountButton = DevHud.MakeButton(panel, "팀 수", CycleTeamCount);
        seatButton = DevHud.MakeButton(panel, "다음 입장", CycleForcedSeat);

        // 라벨은 버튼 자식 텍스트 하나뿐이다. 한 번만 풀어서 캐시한다.
        teamCountLabel = teamCountButton.GetComponentInChildren<TMP_Text>();
        seatLabel = seatButton.GetComponentInChildren<TMP_Text>();
        Refresh();
    }

    void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + refreshInterval;
        Refresh();
    }

    /// 팀 수는 1부터 최대치까지 돌아가며 커진다. 접속 중에는 버튼이 잠겨 있어 오지 않는다.
    void CycleTeamCount()
    {
        var next = seating.TeamCount >= seating.MaxTeams ? 1 : seating.TeamCount + 1;
        if (!seating.SetTeamCountCheat(next)) return;
        Refresh();
    }

    /// 자동(라운드 로빈) → 팀 0 → 팀 1 → … → 자동.
    void CycleForcedSeat()
    {
        var next = seating.ForcedSeat + 1;
        seating.SetForcedSeatCheat(next >= seating.TeamCount ? MatchSeating.NoForcedSeat : next);
        Refresh();
    }

    void Refresh()
    {
        var manager = NetworkManager.Singleton;
        var listening = manager != null && manager.IsListening;
        var isServer = manager != null && manager.IsServer;
        var team = PlayerTeam.Local();

        if (listening == shownListening && isServer == shownServer && team == shownTeam &&
            seating.TeamCount == shownTeamCount && seating.ForcedSeat == shownForcedSeat) return;

        shownListening = listening;
        shownServer = isServer;
        shownTeam = team;
        shownTeamCount = seating.TeamCount;
        shownForcedSeat = seating.ForcedSeat;

        // 접속 중에는 카페가 이미 스폰돼 있어 팀 수를 바꿀 수 없다. 배정은 서버만 정한다.
        DevHud.SetInteractable(teamCountButton, !listening);
        DevHud.SetInteractable(seatButton, isServer);

        teamCountLabel.text = $"팀 수: {seating.TeamCount}/{seating.MaxTeams}";
        seatLabel.text = seating.ForcedSeat < 0
            ? "다음 입장: 자동"
            : $"다음 입장: 팀 {seating.ForcedSeat}";

        // 팀 번호는 어느 피어에서든 보여 준다. 버튼만 서버에서 열린다.
        status.text = !listening ? "접속 전"
            : team < 0 ? "내 팀 · 배정 대기"
            : $"내 팀 {team}{(isServer ? "" : " (조작은 서버)")}";
    }
}
