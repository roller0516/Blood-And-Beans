using Unity.Netcode;
using UnityEngine;

/// 플레이어 한 명이 고른 캐릭터와 그 능력 (기획서 9장).
///
/// **고르는 것은 화면이고 확정하는 것은 서버다.** 팀 내 중복 픽 금지(9.1)는 두 클라이언트가
/// 각자 판정할 수 없다 — 동시에 같은 칸을 누르면 둘 다 통과한다. 그래서 판정은 여기 한 곳뿐이다.
///
/// 낮 패시브는 상시라 발동 지점이 없고, 효과가 걸리는 자리(이동·손님·설거지·손·게이지)가
/// 각자 이 컴포넌트에 물어본다. 밤 액티브만 입력을 받는다 (9.2).
///
/// 픽은 전원에게 공개된다. 기획서 3.1이 비공개로 둔 것은 재료·설비·캐릭터인데, 그것은
/// *상대 팀에게* 감춘다는 뜻이고 캐릭터 선택 화면(9.1 중복 픽 금지)은 팀원의 픽을 보여
/// 줘야 성립한다. 팀 밖으로 감추는 것은 복제 범위가 아니라 화면이 정한다.
[RequireComponent(typeof(PlayerTeam))]
public class PlayerCharacter : NetworkBehaviour
{
    [Header("밤 액티브 (기획서 9.2)")]
    /// 「도깨비불」이 설치하는 가짜 상자. 비워 두면 그 스킬만 동작하지 않는다.
    [SerializeField] ItemBox decoyBoxPrefab;

    /// 「환각」이 심는 가짜 가방. 비워 두면 그 스킬만 동작하지 않는다.
    [SerializeField] BuriedBag decoyBagPrefab;

    /// 「메아리」가 한 번에 걷어내는 반경.
    /// ponytail: 기획서 9.2에 수치가 없다. 안개 반경(`FogOfWar.revealRadius` 7)의 몇 배가
    /// "넓은 범위"인지는 플레이로 정해야 한다. 14장 #2와 같은 자리로 간다.
    [SerializeField] float echoRadius = 20f;

    /// 「감별」과 「추적」이 훑는 반경.
    [SerializeField] float appraiseRadius = 8f;
    [SerializeField] float trackRadius = 18f;

    /// 「추적」이 찾아낸 가방을 보여 주는 시간.
    [SerializeField] float trackRevealSeconds = 6f;

    /// 강심장이 큐 길이를 다시 보는 간격. 대기 인원은 프레임마다 바뀌지 않는다.
    [SerializeField] float queueCheckInterval = 0.25f;

    readonly NetworkVariable<int> character = new(CharacterCatalog.NoPick);

    /// 다음 액티브를 쓸 수 있는 서버 시각. 소유자만 읽으면 되므로 쿨다운 표시도 소유자 몫이다.
    readonly NetworkVariable<double> nextSkillAt = new(0d,
        NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

    PlayerTeam team;
    PlayerMove move;
    MatchDirector director;
    GamePhase subscribedPhase;

    /// 강심장 판정에 쓰는 자기 팀 대기열. 팀이 정해질 때 한 번 찾는다 — 주기 실행 안에서
    /// 컴포넌트를 조회하지 않기 위해서다 (AGENTS.md 참조와 결합도).
    CustomerQueue queue;

    float nextQueueCheck;

    /// 마지막으로 이동에 밀어 넣은 패시브 배수. 같은 값을 매번 다시 밀지 않는다.
    float pushedPassiveScale = 1f;

    public int Index => character.Value;
    public bool HasPick => CharacterCatalog.IsValid(character.Value);

    public CharacterDef Def => CharacterCatalog.All[
        Mathf.Clamp(character.Value, 0, CharacterCatalog.All.Length - 1)];

    /// 이 플레이어가 그 낮 패시브를 가졌는가.
    public bool Has(DayPassive passive) => HasPick && Def.Day == passive;

    public NightSkill Skill => HasPick ? Def.Night : NightSkill.None;

    /// 남은 쿨다운. 소유자 화면이 읽는다.
    public float SkillCooldownRemaining
    {
        get
        {
            if (NetworkManager == null || !NetworkManager.IsListening) return 0f;
            return Mathf.Max(0f, (float)(nextSkillAt.Value - NetworkManager.ServerTime.Time));
        }
    }

    void Awake()
    {
        team = GetComponent<PlayerTeam>();
        move = GetComponent<PlayerMove>();
    }

    public override void OnNetworkSpawn()
    {
        MatchDirector.Bind(BindDirector);
        character.OnValueChanged += OnCharacterChanged;

        // 로비에서 고른 값을 지금 넘긴다. 선택 화면은 플레이어 오브젝트가 서기 전에도
        // 열리므로(타이틀 씬) 픽이 `SteamLobby`에 보관돼 있다.
        if (!IsOwner) return;

        var pending = GameManager.SelectedCharacter;
        if (CharacterCatalog.IsValid(pending)) PickRpc(pending);
    }

    public override void OnNetworkDespawn()
    {
        MatchDirector.Unbind(BindDirector);
        character.OnValueChanged -= OnCharacterChanged;

        if (subscribedPhase != null) subscribedPhase.PhaseEntered -= OnPhaseEntered;
        subscribedPhase = null;
    }

    /// 같은 인스턴스로 두 번 불려도 되게 짠다 (`MatchDirector.Bind` 계약).
    void BindDirector(MatchDirector next)
    {
        director = next;
        var phase = next != null ? next.Phase : null;
        if (phase == subscribedPhase) return;

        if (subscribedPhase != null) subscribedPhase.PhaseEntered -= OnPhaseEntered;
        subscribedPhase = phase;
        if (subscribedPhase != null) subscribedPhase.PhaseEntered += OnPhaseEntered;

        queue = null;    // 판이 바뀌면 카페도 바뀐다
    }

    void OnPhaseEntered(Phase p)
    {
        // 카페는 낮이 처음 시작될 때쯤 이미 스폰돼 있다. 페이즈 경계는 프레임 수에
        // 비례하지 않는 사건이라 여기서 한 번 찾아 캐시해도 된다.
        if (queue == null && director != null && team != null)
            queue = director.CafeOf(team.Team)?.Queue;

        if (IsServer) PushPassiveScaleServer();
    }

    void OnCharacterChanged(int _, int __)
    {
        if (IsServer) PushPassiveScaleServer();
    }

    /// 픽을 바꾼다. 캐릭터 선택 화면이 「확정」을 눌렀을 때 온다.
    ///
    /// `SendTo.Server`는 아무 클라이언트나 부를 수 있으므로 본문에서 발신자를 검증한다
    /// (AGENTS.md 「Netcode에서 쓰지 말아야 할 방식」). 소유자만 자기 픽을 정한다.
    [Rpc(SendTo.Server)]
    public void PickRpc(int index, RpcParams p = default)
    {
        if (p.Receive.SenderClientId != OwnerClientId) return;
        if (!CharacterCatalog.IsValid(index)) return;

        // 팀 내 중복 픽 금지 (기획서 9.1). 판정이 여기 한 곳뿐이라 동시에 같은 칸을
        // 눌러도 먼저 도착한 쪽만 통과한다.
        if (TakenInTeam(team != null ? team.Team : -1, index, OwnerClientId)) return;

        character.Value = index;
    }

    /// 개발 치트. 서버가 픽을 직접 박는다.
    ///
    /// **팀 내 중복 픽 금지(기획서 9.1)를 건너뛴다.** 같은 패시브를 둘에게 걸어 보는 것이
    /// 이 치트를 만든 이유다 — 정규 경로(`PickRpc`)로는 그 조합을 만들 수 없어서 팀 단위
    /// 패시브(인기 카페·붙임성·제빵사)가 짝꿍에게도 걸리는지 확인할 방법이 없었다.
    ///
    /// `CharacterCatalog.NoPick`을 주면 픽을 지운다. 패시브 없는 상태와 비교하는 데 쓴다.
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

    /// 이 팀의 누군가가 그 패시브를 가졌는가 (기획서 9.1의 가게 단위 능력).
    ///
    /// 순회가 들어가므로 **주기 실행에서 부르지 않는다.** 부르는 곳은 손님 스폰과 게이지
    /// 시작처럼 사건 한 번짜리 자리뿐이다.
    public static bool TeamHas(int teamId, DayPassive passive)
    {
        if (teamId < 0) return false;

        var nm = NetworkManager.Singleton;
        if (nm == null) return false;

        foreach (var client in nm.ConnectedClientsList)
        {
            if (PlayerTeam.Of(client.ClientId) != teamId) continue;

            var pc = Of(client.ClientId);
            if (pc != null && pc.Has(passive)) return true;
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

    // --- 낮 패시브 중 이동에 걸리는 둘 (기획서 9.1) ---

    void Update()
    {
        if (!IsServer) return;

        // 강심장만 상태에 따라 켜졌다 꺼진다. 나머지 이동 패시브(잰걸음)는 고정이라
        // 픽이 바뀔 때만 밀면 된다.
        if (!Has(DayPassive.Stouthearted)) return;
        if (Time.time < nextQueueCheck) return;
        nextQueueCheck = Time.time + queueCheckInterval;

        PushPassiveScaleServer();
    }

    /// 캐릭터에서 나오는 이동속도 배수. 무게에서 나오는 배수와는 채널이 다르다 —
    /// 둘을 한 값에 섞으면 어느 쪽이 바뀔 때 다른 쪽을 다시 계산해야 한다.
    float PassiveSpeedScale
    {
        get
        {
            // 낮 패시브다 (기획서 9.1). 밤에는 걸리지 않는다 — 밤의 이동은 무게가 정한다.
            if (director == null || director.Phase == null ||
                director.Phase.Current != global::Phase.Day) return 1f;

            if (Has(DayPassive.Swift)) return DayPassives.SwiftSpeed;

            if (Has(DayPassive.Stouthearted) && queue != null &&
                queue.Waiting.Count >= DayPassives.StoutheartedQueue)
                return DayPassives.StoutheartedSpeed;

            return 1f;
        }
    }

    void PushPassiveScaleServer()
    {
        if (!IsServer || move == null) return;

        var want = PassiveSpeedScale;
        if (Mathf.Approximately(want, pushedPassiveScale)) return;

        pushedPassiveScale = want;
        move.SetPassiveScaleServer(want);
    }

    // --- 밤 액티브 (기획서 9.2) ---

    /// 스킬 키를 눌렀다. 소유자만 부를 수 있다.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void UseSkillRpc()
    {
        var skill = Skill;
        if (!NightSkills.Exists(skill)) return;

        // 밤에만 쓴다 (기획서 9.2: "밤의 경우"). 낮에는 발동 자체가 없다.
        if (director == null || director.Phase == null ||
            director.Phase.Current != Phase.Night) return;

        var now = NetworkManager.ServerTime.Time;
        if (now < nextSkillAt.Value) return;

        if (!CastServer(skill)) return;
        nextSkillAt.Value = now + NightSkills.CooldownOf(skill);
    }

    /// 실제로 발동한다. 실패하면 쿨다운을 태우지 않는다 — 프리팹을 이어 두지 않아
    /// 아무 일도 일어나지 않았는데 40초를 기다리게 하면 안 된다.
    bool CastServer(NightSkill skill) => skill switch
    {
        NightSkill.WillOWisp => PlaceDecoyBoxServer(),
        NightSkill.Echo => EchoServer(),
        NightSkill.Appraise => AppraiseServer(),
        NightSkill.Track => TrackServer(),
        NightSkill.Illusion => PlaceDecoyBagServer(),
        _ => false,
    };

    /// 도깨비불 — 가짜 상자를 세운다. 내용이 비어 있는 임시 상자라, 여는 데 든 시간이
    /// 그대로 손해가 된다.
    bool PlaceDecoyBoxServer()
    {
        if (decoyBoxPrefab == null)
        {
            CDebug.LogError($"{name}: decoyBoxPrefab이 비어 있다. 도깨비불이 아무것도 세우지 못한다.", this);
            return false;
        }

        var box = Instantiate(decoyBoxPrefab, transform.position, Quaternion.identity);
        box.NetworkObject.Spawn();

        // 빈 목록을 심는다. `SeedServer`는 개수가 0인 칸을 버리므로 결과가 빈 상자다.
        box.SeedServer(System.Array.Empty<LootStack>());
        return true;
    }

    /// 메아리 — 주변 안개를 즉시 걷는다. 걷힌 칸은 전원이 공유하므로(기획서 6.1-3)
    /// 남에게도 길을 열어 준다. 그것이 이 스킬의 값이다.
    bool EchoServer()
    {
        var fog = GetComponent<FogOfWar>();
        if (fog == null) return false;

        fog.RevealBurstServer(transform.position, echoRadius);
        return true;
    }

    /// 감별 — 근처 상자의 가려진 칸을 즉시 공개한다. 공개 상태는 상자마다 공유되므로
    /// (기획서 6.5.3) 뒤에 오는 사람도 그대로 본다.
    bool AppraiseServer()
    {
        var hit = false;
        foreach (var box in FindObjectsByType<ItemBox>(FindObjectsSortMode.None))
        {
            if (box == null || !box.NetworkObject.IsSpawned) continue;
            if (Vector3.Distance(box.transform.position, transform.position) > appraiseRadius) continue;

            box.RevealAllServer();
            hit = true;
        }
        return hit;
    }

    /// 추적 — 주변에 묻힌 가방을 잠시 드러낸다. 적이 묻은 것도 보인다 — 그것이 목적이다
    /// (기획서 6.7 「적 가방 탐색 및 파괴」).
    bool TrackServer()
    {
        var hit = false;
        foreach (var bag in FindObjectsByType<BuriedBag>(FindObjectsSortMode.None))
        {
            if (bag == null || !bag.NetworkObject.IsSpawned) continue;
            if (Vector3.Distance(bag.transform.position, transform.position) > trackRadius) continue;

            bag.RevealToServer(OwnerClientId, trackRevealSeconds);
            hit = true;
        }
        return hit;
    }

    /// 환각 — 빈 가방을 묻는다. 적이 소각에 시간을 태우게 만드는 것이 목적이라 내용이 없다.
    bool PlaceDecoyBagServer()
    {
        if (decoyBagPrefab == null)
        {
            CDebug.LogError($"{name}: decoyBagPrefab이 비어 있다. 환각이 아무것도 심지 못한다.", this);
            return false;
        }

        var bag = Instantiate(decoyBagPrefab, transform.position, Quaternion.identity);

        // 팀을 먼저 심고 스폰한다 (`PlayerInventory.BuryRpc`와 같은 이유 — 스폰 뒤에 쓰면
        // 적 클라이언트가 팀 미상 상태의 가방을 한 틱 동안 그대로 렌더한다).
        bag.SeedServer(team != null ? team.Team : -1, null);
        bag.NetworkObject.SpawnWithObservers = false;   // 보여주는 시점은 BuriedBag이 정한다
        bag.NetworkObject.Spawn();
        return true;
    }
}
