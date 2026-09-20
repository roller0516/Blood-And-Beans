using Unity.Netcode;
using UnityEngine;

/// 플레이어 한 명이 고른 캐릭터 (기획서 9장).
///
/// **고르는 것은 화면이고 확정하는 것은 서버다.** 팀 내 중복 픽 금지(9.1)는 두 클라이언트가
/// 각자 판정할 수 없다 — 동시에 같은 칸을 누르면 둘 다 통과한다. 그래서 판정은 여기 한 곳뿐이다.
///
/// **액티브는 여기 없다.** 발동은 `PlayerAbilities`가, 능력 하나하나는 순수 C# 객체가
/// 맡는다 (`Player/Abilities/`). 이 클래스에 남은 것은 "누가 무엇을 고를 수 있는가"와,
/// 캐릭터가 들고 있는 **상시 배수**뿐이다 — 그것은 보석·페널티와 한 축에서 곱해져야 해서
/// 합치는 자리가 하나여야 한다.
///
/// 픽은 전원에게 공개된다. 기획서 3.1이 비공개로 둔 것은 재료·설비·캐릭터인데, 그것은
/// *상대 팀에게* 감춘다는 뜻이고 캐릭터 선택 화면(9.1 중복 픽 금지)은 팀원의 픽을 보여
/// 줘야 성립한다. 팀 밖으로 감추는 것은 복제 범위가 아니라 화면이 정한다.
[RequireComponent(typeof(PlayerTeam))]
public class PlayerCharacter : NetworkBehaviour
{
    readonly NetworkVariable<int> character = new(CharacterCatalog.NoPick);

    PlayerTeam team;
    PlayerMove move;
    PlayerAbilities abilities;
    MatchDirector director;

    /// 마지막으로 이동에 밀어 넣은 캐릭터 배수. 같은 값을 매번 다시 밀지 않는다.
    float pushedPassiveScale = 1f;

    Cafe cachedCafe;

    public event System.Action<int> CharacterChanged;
    public int Index => character.Value;
    public bool HasPick => CharacterCatalog.IsValid(character.Value);

    public CharacterDef Def => CharacterCatalog.All[
        Mathf.Clamp(character.Value, 0, CharacterCatalog.All.Length - 1)];

    public NightSkill Skill => HasPick ? Def.Night : NightSkill.None;
    public DaySkill DaySkill => HasPick ? Def.Day : DaySkill.None;

    void Awake()
    {
        team = GetComponent<PlayerTeam>();
        move = GetComponent<PlayerMove>();
        abilities = GetComponent<PlayerAbilities>();
    }

    public override void OnNetworkSpawn()
    {
        MatchDirector.Bind(BindDirector);
        character.OnValueChanged += OnCharacterChanged;

        // 접속 승인 때 고정한 픽만 서버에서 적용한다 (기획서 9.3).
        if (IsServer) character.Value = InitialPickServer();
    }

    public override void OnNetworkDespawn()
    {
        MatchDirector.Unbind(BindDirector);
        character.OnValueChanged -= OnCharacterChanged;
    }

    /// 같은 인스턴스로 두 번 불려도 되게 짠다 (`MatchDirector.Bind` 계약).
    void BindDirector(MatchDirector next)
    {
        director = next;
        cachedCafe = null;    // 판이 바뀌면 카페도 바뀐다
    }

    /// 안 고르고 들어오면 팀에서 비어 있는 가장 앞 번호(기본 0번)를 준다. 픽이 없으면 모델이 서지 않는다.
    int InitialPickServer()
    {
        var seating = GameManager.Seating;
        var pick = seating.CharacterOf(OwnerClientId);
        if (CharacterCatalog.IsValid(pick)) return pick;

        // 팀 중복 픽 금지(기획서 9.1)를 지킨다. 좌석을 직접 물어 PlayerTeam 스폰 순서에 기대지 않는다.
        var seat = seating.SeatServer(OwnerClientId);
        for (var i = 0; i < CharacterCatalog.All.Length; i++)
            if (!TakenInTeam(seat, i, OwnerClientId)) return i;
        return CharacterCatalog.NoPick;
    }

    void OnCharacterChanged(int _, int index)
    {
        CharacterChanged?.Invoke(index);
        if (IsServer) PushPassiveScaleServer();
    }

    /// 픽을 바꾼다. 캐릭터 선택 화면이 「확정」을 눌렀을 때 온다.
    ///
    /// `SendTo.Server`는 아무 클라이언트나 부를 수 있으므로 본문에서 발신자를 검증한다
    /// (AGENTS.md 「Netcode에서 쓰지 말아야 할 방식」). 소유자만 자기 픽을 정한다.
    [Rpc(SendTo.Server)]
    public void PickRpc(int index, RpcParams p = default)
    {
        if (p.Receive.SenderClientId != OwnerClientId || director != null) return;
        if (!CharacterCatalog.IsValid(index)) return;

        // 팀 내 중복 픽 금지 (기획서 9.1). 판정이 여기 한 곳뿐이라 동시에 같은 칸을
        // 눌러도 먼저 도착한 쪽만 통과한다.
        if (TakenInTeam(team != null ? team.Team : -1, index, OwnerClientId)) return;

        character.Value = index;
    }

    /// 개발 치트. 서버가 픽을 직접 박는다.
    ///
    /// **팀 내 중복 픽 금지(기획서 9.1)를 건너뛴다.** 같은 스킬을 둘에게 걸어 보는 검증용이다.
    ///
    /// `CharacterCatalog.NoPick`을 주면 픽을 지운다. 스킬 없는 상태와 비교하는 데 쓴다.
    public void SetCharacterCheatServer(int index)
    {
        if (!IsServer) return;
        if (index != CharacterCatalog.NoPick && !CharacterCatalog.IsValid(index)) return;

        character.Value = index;
    }

    /// 그 팀에서 이미 누가 집어 간 칸인가. `exceptClient`는 자기 자신이다 — 같은 칸을
    /// 다시 확정하는 것은 중복이 아니다.
    public static bool TakenInTeam(int teamId, int index, ulong exceptClient)
    {
        if (teamId < 0) return false;

        var nm = NetworkManager.Singleton;
        if (nm == null) return false;

        foreach (var client in nm.ConnectedClientsList)
        {
            if (client.ClientId == exceptClient) continue;
            if (PlayerTeam.Of(client.ClientId) != teamId) continue;

            var other = Of(client.ClientId);
            if (other != null && other.Index == index) return true;
        }
        return false;
    }

    public static PlayerCharacter Of(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.ConnectedClients.TryGetValue(clientId, out var c)) return null;
        return c.PlayerObject != null ? c.PlayerObject.GetComponent<PlayerCharacter>() : null;
    }

    public static PlayerCharacter Local()
    {
        var po = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        return po != null ? po.GetComponent<PlayerCharacter>() : null;
    }

    Cafe CafeServer()
    {
        if (cachedCafe == null && director != null && team != null) cachedCafe = director.CafeOf(team.Team);
        return cachedCafe;
    }

    void Update()
    {
        if (!IsServer) return;
        PushPassiveScaleServer();
    }

    /// 이동 배수를 한 축에서 합친다. 보석·미납 페널티·활공이 **곱으로 쌓이는 유일한 자리**다
    /// (기획서 3.3 · 8.2 · 9.1.2). 나눠 두면 셋이 서로를 덮어쓴다.
    void PushPassiveScaleServer()
    {
        if (!IsServer || move == null) return;

        var cafe = CafeServer();
        var day = director != null && director.Phase.Current == Phase.Day;
        var scale = day && cafe != null && cafe.HasGem(Gem.Wind) ? Gems.MoveSpeedScale : 1f;

        // 미납 페널티는 보석과 같은 축에 마이너스로 붙는다 (기획서 3.3). 낮 페널티라 밤에는 걸지 않는다.
        var ledger = day && director != null && team != null ? director.LedgerOf(team.Team) : null;
        if (ledger != null) scale *= ledger.MoveSpeedScale;

        // 활공은 라우터의 지속 슬롯이 든다. 켜져 있는 낮 액티브가 활공일 때만 곱한다.
        if (day && abilities != null && abilities.Ability<GlideAbility>() != null &&
            abilities.DurationRemaining > 0f) scale *= DaySkills.GlideSpeed;

        if (Mathf.Approximately(scale, pushedPassiveScale)) return;
        pushedPassiveScale = scale;
        move.SetPassiveScaleServer(scale);
    }
}
