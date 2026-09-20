using TMPro;
using UnityEngine;

/// 페이즈 경계를 화면 한복판에서 알리는 창. 밤 시작 카운트다운 · 낮 시작 「READY / GO!」 ·
/// 낮 마감 「마감!」이 같은 창을 쓴다 — 셋 다 "큰 글자 하나가 잠깐 떴다 사라진다"로 같다.
///
/// **언제 뜨고 언제 닫히는지는 이 창이 정하지 않는다.** `MatchFlow`가 복제된 페이즈 경과
/// 시간에서 창을 띄울 구간과 지금 보여 줄 글자를 계산해 넘긴다. 그래야 카운트다운이
/// 클라이언트마다 다른 숫자를 띄우지 않는다 — 시계는 서버 것 하나뿐이다 (`GamePhase`).
public sealed class UIPhaseCuePopup : UIPopup
{
    [SerializeField] TMP_Text label;

    /// 글자가 바뀔 때마다 다시 재생할 연출. 여러 개를 두면(스케일 + 회전 등) 함께 돈다.
    /// 비어 있으면 글자만 바뀐다.
    [SerializeField] DG.Tweening.DOTweenAnimation[] beatAnimations =
        System.Array.Empty<DG.Tweening.DOTweenAnimation>();

    [Header("문구")]
    [SerializeField] string readyText = "READY";
    [SerializeField] string goText = "GO!";
    [SerializeField] string closingText = "마감!";

    /// 지금 화면에 있는 글자. 같은 글자를 다시 넘겨도 연출을 처음부터 다시 돌리지 않는다 —
    /// `MatchFlow`가 매 프레임 부르므로 이 검사가 없으면 연출이 영원히 첫 프레임에 머문다.
    string shown;

    /// 카운트다운만 조작을 막는다. 낮 시작·마감 연출은 그 시점에 이미 플레이가 돌고 있어서
    /// 막으면 조작을 빼앗는 것이 된다.
    bool blocksInput;

    public override bool BlocksPlayerInput => blocksInput;
    public override bool WantsCursor => false;

    protected override void Awake()
    {
        base.Awake();
        if (label == null) CDebug.LogError($"{name}: {nameof(label)}가 비어 있다.", this);
    }

    /// 밤 시작 카운트다운. 남은 초를 그대로 띄운다.
    public void ShowCount(int seconds)
    {
        SetBlocking(true);
        Set(seconds.ToString());
    }

    public void ShowReady()
    {
        SetBlocking(false);
        Set(readyText);
    }

    public void ShowGo()
    {
        SetBlocking(false);
        Set(goText);
    }

    public void ShowClosing()
    {
        SetBlocking(false);
        Set(closingText);
    }

    public override void OnHide()
    {
        base.OnHide();
        shown = null;          // 다음에 열릴 때 첫 글자부터 다시 연출한다
        blocksInput = false;

        // 연출 도중에 닫히면 글자가 부푼 채로 굳는다. 되감아야 다음에 제 크기로 뜬다.
        for (var i = 0; i < beatAnimations.Length; i++)
            if (beatAnimations[i] != null) beatAnimations[i].DORewind();
    }

    /// 입력 게이트는 스택이 바뀔 때만 다시 계산된다 (`UIManager.ApplyInputGates`). 창이
    /// **떠 있는 채로** 막느냐 마느냐가 바뀌는 경우가 여기뿐이라 직접 알린다 — 이게 없으면
    /// 이미 열려 있는 창이 카운트다운으로 넘어갈 때 조작이 안 막힌다.
    void SetBlocking(bool value)
    {
        if (blocksInput == value) return;
        blocksInput = value;

        var ui = UIManager.Instance;
        if (ui != null) ui.RefreshInputGates();
    }

    void Set(string text)
    {
        if (shown == text) return;
        shown = text;
        if (label != null) label.text = text;

        for (var i = 0; i < beatAnimations.Length; i++)
            if (beatAnimations[i] != null) beatAnimations[i].RewindThenRecreateTweenAndPlay();
    }
}
