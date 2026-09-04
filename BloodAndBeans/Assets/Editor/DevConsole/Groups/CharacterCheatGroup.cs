using System.Collections.Generic;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// 캐릭터 패시브 치트 (기획서 9장). **접속한 사람 수만큼 버튼이 생기고, 버튼을 누르면
/// 패시브 목록이 뜬다. 목록에서 하나를 고르면 그 캐릭터에 즉시 걸린다.**
///
/// 순환 버튼이 아니라 목록인 이유는 캐릭터가 여덟이기 때문이다. 「제빵사」를 보려고
/// 일곱 번 누르는 동안 그 사이 값들이 전부 실제로 적용됐다가 사라진다 — 팀 단위 패시브는
/// 손님 스폰 시점에 걸리므로(`CustomerQueue.Spawn`) 지나가는 값도 판에 흔적을 남긴다.
///
/// 정규 경로(`PlayerCharacter.PickRpc`)로는 만들 수 없는 조합을 만드는 것이 목적이다.
/// 그쪽은 팀 내 중복 픽을 금지하므로(9.1) 같은 패시브를 둘에게 걸 수 없고, 그러면 팀 단위
/// 패시브(인기 카페·붙임성·제빵사)가 짝꿍에게도 걸리는지 확인할 방법이 없다.
///
/// 픽은 서버 권위라 서버(호스트)에서만 뜻이 있다. 클라이언트에서는 잠긴다 —
/// `TeamCheatGroup`과 같은 이유다. 눌러 봐야 서버의 값은 그대로이고, 그걸 모른 채
/// 테스트하면 없는 결함을 쫓게 된다.
public class CharacterCheatGroup : DevConsoleGroup
{
    public override string Tab => "치트";
    public override string Title => "캐릭터 패시브";

    Label note;

    /// 사람마다 한 줄. 접속 인원이 바뀔 때만 다시 만든다 — `Refresh`는 10Hz라 매번
    /// 다시 만들면 버튼을 누르는 순간 그 버튼이 사라지고 목록도 함께 닫힌다.
    VisualElement list;

    readonly List<ulong> shown = new();
    readonly List<Button> buttons = new();

    protected override void Build(VisualElement group)
    {
        note = Row(group, "상태", "-");

        list = new VisualElement();
        group.Add(list);
    }

    public override void Refresh(in DevConsoleState state)
    {
        if (!state.IsServer)
        {
            note.text = state.Listening ? "서버에서만 (호스트로 실행)" : "접속 전";
            Clear();
            return;
        }

        var manager = NetworkManager.Singleton;
        if (manager == null) { note.text = "-"; Clear(); return; }

        if (!SameClients(manager)) Rebuild(manager);

        note.text = shown.Count == 0
            ? "접속한 사람이 없다"
            : $"{shown.Count}명 · 눌러서 패시브 고르기";

        for (var i = 0; i < shown.Count && i < buttons.Count; i++)
            buttons[i].text = LabelOf(shown[i]);
    }

    /// 접속 명단이 그대로인가. 순서까지 같아야 버튼이 엉뚱한 사람을 가리키지 않는다.
    bool SameClients(NetworkManager manager)
    {
        var clients = manager.ConnectedClientsList;
        if (clients.Count != shown.Count) return false;

        for (var i = 0; i < clients.Count; i++)
            if (clients[i].ClientId != shown[i]) return false;

        return true;
    }

    void Rebuild(NetworkManager manager)
    {
        Clear();

        foreach (var client in manager.ConnectedClientsList)
        {
            var id = client.ClientId;
            shown.Add(id);

            // 사람마다 줄 하나. 버튼 텍스트가 곧 그 사람의 현재 상태라 라벨을 따로 두지 않는다.
            var row = ButtonRow(list);

            // 목록을 버튼 바로 아래에 붙이려면 콜백이 버튼 자신을 알아야 한다. 변수를 먼저
            // 선언해 두면 람다가 그 변수를 잡으므로, 할당이 끝난 뒤에 눌러도 값이 들어 있다.
            Button button = null;
            button = Btn(row, LabelOf(id), () => OpenPassiveList(id, button));
            buttons.Add(button);
        }
    }

    void Clear()
    {
        shown.Clear();
        buttons.Clear();
        list?.Clear();
    }

    /// 패시브 목록 창. 버튼 아래에 붙어 뜨고, 지금 걸린 것에 체크가 붙는다.
    ///
    /// 목록을 여는 시점에 그 사람의 현재 값을 다시 읽는다. 창이 떠 있는 동안 다른 경로로
    /// (정규 선택 화면, 다른 줄의 치트) 값이 바뀔 수 있어서, 열 때의 값을 들고 있으면
    /// 체크가 실제와 어긋난다.
    static void OpenPassiveList(ulong clientId, VisualElement anchor)
    {
        var pc = PlayerCharacter.Of(clientId);
        if (pc == null) return;

        var menu = new GenericMenu();

        // 「없음」이 먼저다. 패시브를 뗀 상태와 비교할 수 있어야 그것이 무엇을 바꾸는지
        // 확인된다.
        menu.AddItem(new GUIContent("없음"), !pc.HasPick,
                     () => Apply(clientId, CharacterCatalog.NoPick));
        menu.AddSeparator(string.Empty);

        var all = CharacterCatalog.All;
        for (var i = 0; i < all.Length; i++)
        {
            var index = i;

            // 낮 효과를 함께 적는다. 이름만으로는 「강심장」과 「잰걸음」이 둘 다 이동속도
            // 패시브라는 것이 보이지 않는다.
            var label = $"{all[i].DayName} — {all[i].DayEffect}";

            menu.AddItem(new GUIContent(label), pc.Index == index,
                         () => Apply(clientId, index));
        }

        // UIElements의 `worldBound`는 패널 좌표이고 `GenericMenu`는 GUI 좌표를 받는다.
        // 에디터 창 안에서는 둘이 같은 원점을 쓰므로 그대로 넘긴다.
        menu.DropDown(anchor.worldBound);
    }

    /// 고른 값을 박는다. 이 창은 에디터라 서버와 같은 프로세스에 있으므로 RPC를 거치지
    /// 않는다 — 그룹 자체가 서버에서만 활성화된다 (`Refresh`).
    static void Apply(ulong clientId, int index)
    {
        var pc = PlayerCharacter.Of(clientId);
        if (pc == null) return;

        pc.SetCharacterCheatServer(index);
    }

    static string LabelOf(ulong clientId)
    {
        var team = PlayerTeam.Of(clientId);
        var teamText = team < 0 ? "팀 미정" : $"{team}팀";

        var pc = PlayerCharacter.Of(clientId);
        if (pc == null) return $"#{clientId} · {teamText} · 스폰 대기";

        if (!pc.HasPick) return $"#{clientId} · {teamText} · 없음  ▾";

        var def = pc.Def;
        var night = NightSkills.Exists(def.Night) ? def.NightName : "밤 없음";
        return $"#{clientId} · {teamText} · {def.DayName} / {night}  ▾";
    }
}
