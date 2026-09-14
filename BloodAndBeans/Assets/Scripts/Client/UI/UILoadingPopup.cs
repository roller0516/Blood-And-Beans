using TMPro;
using UnityEngine;

/// 방 인원이 다 모여 첫 밤이 시작되기 전까지 덮는 창. 인원은 서버가 센다 (`GamePhase.Started`).
public sealed class UILoadingPopup : UIPopup
{
    [SerializeField] TMP_Text statusText;

    /// 인원을 아직 모를 때(시계가 복제되기 전).
    [SerializeField] string loadingText = "불러오는 중…";

    /// {0} 모인 인원, {1} 방 인원.
    [SerializeField] string waitingFormat = "다른 플레이어를 기다리는 중… {0} / {1}";

    int shownJoined = -1;
    int shownExpected = -1;

    /// 시작 전에는 조작할 것이 없다.
    public override bool BlocksPlayerInput => true;
    public override bool WantsCursor => false;

    protected override void Awake()
    {
        base.Awake();
        if (statusText == null) CDebug.LogError($"{name}: {nameof(statusText)}가 비어 있다.", this);
    }

    /// 값이 바뀔 때만 문자열을 만든다. `expected`가 0이면 아직 인원을 모른다.
    public void SetProgress(int joined, int expected)
    {
        if (joined == shownJoined && expected == shownExpected) return;
        shownJoined = joined;
        shownExpected = expected;
        statusText.text = expected > 0 ? string.Format(waitingFormat, joined, expected) : loadingText;
    }
}
