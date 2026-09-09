using UnityEngine;
using UnityEngine.UIElements;

/// 로비를 무엇으로 도는가 (`NetPlatform`). 빌드는 언제나 스팀이고, 여기서 고르는 것은
/// 에디터에서만이다.
///
/// 값은 `EditorPrefs`에 있다. MPPM 가상 플레이어는 별도 프로세스지만 같은 사용자·같은
/// Unity 버전이라 같은 값을 읽는다 — 창마다 따로 고를 필요가 없다.
public class PlatformGroup : DevConsoleGroup
{
    public override string Tab => "접속";
    public override string Title => "플랫폼";

    Button steam, editor;
    Label info;

    /// 재생 중에만 있다. 씬이 늦게 뜨므로 찾을 때까지만 찾고 찾은 뒤에는 안 찾는다.
    SteamLobby lobby;

    protected override void Build(VisualElement group)
    {
        var row = ButtonRow(group);
        steam = Btn(row, "Steam", () => Choose(NetPlatform.Steam));
        editor = Btn(row, "Editor", () => Choose(NetPlatform.Editor));

        info = Row(group, "상태", string.Empty);
    }

    public override void Refresh(in DevConsoleState state)
    {
        if (!state.Playing) lobby = null;
        else if (lobby == null) lobby = Object.FindAnyObjectByType<SteamLobby>();

        var chosen = NetPlatformSetting.Current;
        Mark(steam, chosen == NetPlatform.Steam);
        Mark(editor, chosen == NetPlatform.Editor);

        if (lobby == null)
        {
            info.text = $"{Name(chosen)} · 다음 재생부터";
            return;
        }

        // 방 안이거나 접속 중이면 로비가 갈아 끼우기를 거절한다. 고른 것과 도는 것이
        // 다르면 그 사실이 보여야 한다.
        info.text = lobby.Platform == chosen
            ? $"{Name(chosen)} 실행 중"
            : $"{Name(chosen)} 선택 · {Name(lobby.Platform)} 실행 중";
    }

    void Choose(NetPlatform platform)
    {
        NetPlatformSetting.Set(platform);
        lobby?.SwitchPlatform(platform);
    }

    static void Mark(Button button, bool chosen) => button.EnableInClassList("btn--primary", chosen);

    static string Name(NetPlatform platform) =>
        platform == NetPlatform.Steam ? "Steam" : "Editor(창=계정)";
}
