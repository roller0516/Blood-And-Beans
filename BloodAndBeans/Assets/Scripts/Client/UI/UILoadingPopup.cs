using TMPro;
using UnityEngine;

/// 무언가를 불러오는 동안 화면을 덮는 창. 씬 전환이든 매치 대기든 같은 창을 쓴다.
///
/// 직접 띄우지 않는다. `UIManager.BeginLoading`이 핸들을 주고, 마지막 핸들이 풀릴 때 닫힌다 —
/// 여러 곳이 동시에 불러와도 하나가 먼저 끝났다고 창이 닫히지 않는다.
public sealed class UILoadingPopup : UIPopup
{
    [SerializeField] TMP_Text statusText;

    /// 부르는 쪽이 문구를 주지 않았을 때.
    [SerializeField] string loadingText = "불러오는 중…";

    /// 불러오는 동안에는 조작할 것이 없다.
    public override bool BlocksPlayerInput => true;
    public override bool WantsCursor => false;

    protected override void Awake()
    {
        base.Awake();
        if (statusText == null) CDebug.LogError($"{name}: {nameof(statusText)}가 비어 있다.", this);
    }

    /// null이면 기본 문구. 같은 문구면 다시 대입하지 않는다 — 매 프레임 불려도 메시 재생성이 없다.
    internal void SetMessage(string message)
    {
        var text = message ?? loadingText;
        if (statusText.text != text) statusText.text = text;
    }
}
