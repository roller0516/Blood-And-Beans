using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

/// 플레이어 한 명이 고른 캐릭터와 그 능력 (기획서 9장).
///
/// **고르는 것은 화면이고 확정하는 것은 서버다.** 팀 내 중복 픽 금지(9.1)는 두 클라이언트가
/// 각자 판정할 수 없다 — 동시에 같은 칸을 누르면 둘 다 통과한다. 그래서 판정은 여기 한 곳뿐이다.
///
/// 스킬 키 하나로 낮에는 낮 액티브(9.1.2), 밤에는 밤 액티브(9.2)를 쓴다. 쿨타임 칸도 하나다.
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

    readonly NetworkVariable<int> character = new(CharacterCatalog.NoPick);

    /// 다음 액티브를 쓸 수 있는 서버 시각. 소유자만 읽으면 되므로 쿨다운 표시도 소유자 몫이다.
    readonly NetworkVariable<double> nextSkillAt = new(0d,
        NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

    PlayerTeam team;
    PlayerMove move;
    MatchDirector director;
    GamePhase subscribedPhase;

    /// 마지막으로 이동에 밀어 넣은 캐릭터 배수. 같은 값을 매번 다시 밀지 않는다.
    float pushedPassiveScale = 1f;

    public event System.Action<int> CharacterChanged;
    public int Index => character.Value;
    public bool HasPick => CharacterCatalog.IsValid(character.Value);

    public CharacterDef Def => CharacterCatalog.All[
        Mathf.Clamp(character.Value, 0, CharacterCatalog.All.Length - 1)];

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
        controller = GetComponent<CharacterController>();
        carry = GetComponent<PlayerCarry>();
    }

    public override void OnNetworkSpawn()
    {
        MatchDirector.Bind(BindDirector);
        character.OnValueChanged += OnCharacterChanged;
        shortcutUntil.OnValueChanged += OnShortcut;

        // 접속 승인 때 고정한 픽만 서버에서 적용한다 (기획서 9.3).
        if (IsServer) character.Value = InitialPickServer();
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

    public override void OnNetworkDespawn()
    {
        MatchDirector.Unbind(BindDirector);
        character.OnValueChanged -= OnCharacterChanged;
        shortcutUntil.OnValueChanged -= OnShortcut;

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

        cachedCafe = null;    // 판이 바뀌면 카페도 바뀐다
    }

    void OnPhaseEntered(Phase p)
    {
        if (IsServer)
        {
            // 낮 액티브 효과는 그 낮 안에서만 산다.
            glideUntil = 0d;
            refineReady.Value = false;
            nextSkillAt.Value = 0;
            PushPassiveScaleServer();
        }
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

    // --- 낮 액티브 (기획서 9.1.2) ---

    public DaySkill DaySkill => HasPick ? Def.Day : DaySkill.None;

    /// 「활공」이 끝나는 서버 시각. 속도는 `PlayerMove`의 복제 배수로 소유자에게 간다.
    double glideUntil;

    /// 「지름길」이 끝나는 서버 시각. 통과는 피어마다 물리에서 풀어야 하므로 전원이 읽는다.
    readonly NetworkVariable<double> shortcutUntil = new();

    /// 「정제」가 장전돼 있는가. 다음 한 잔에서 소모된다.
    readonly NetworkVariable<bool> refineReady = new(false, NetworkVariableReadPermission.Owner);
    public bool RefineReady => refineReady.Value;

    Cafe cachedCafe;
    CharacterController controller;
    PlayerCarry carry;

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

    void PushPassiveScaleServer()
    {
        if (!IsServer || move == null) return;
        var cafe = CafeServer();
        var day = director != null && director.Phase.Current == Phase.Day;
        var scale = day && cafe != null && cafe.HasBuff(TeamBuff.Move) ? DayBalance.BuffSpeed : 1f;
        if (day && NetworkManager.ServerTime.Time < glideUntil) scale *= DaySkills.GlideSpeed;
        if (Mathf.Approximately(scale, pushedPassiveScale)) return;
        pushedPassiveScale = scale;
        move.SetPassiveScaleServer(scale);
    }

    /// 발동에 실패하면 쿨타임을 태우지 않는다 (밤 액티브와 같은 계약).
    bool UseDaySkillServer()
    {
        var skill = DaySkill;
        var now = NetworkManager.ServerTime.Time;
        if (skill == DaySkill.None || now < nextSkillAt.Value) return false;

        switch (skill)
        {
            case DaySkill.Ignite:
                if (!IgniteServer()) return false;
                break;
            case DaySkill.Glide:
                glideUntil = now + DaySkills.GlideSeconds;
                break;
            case DaySkill.Refine:
                if (refineReady.Value) return false;   // 1회성이라 겹쳐 쌓지 않는다
                refineReady.Value = true;
                break;
            case DaySkill.Shortcut:
                shortcutUntil.Value = now + DaySkills.ShortcutSeconds;
                break;
            case DaySkill.Swallow:
                if (!SwallowServer()) return false;
                break;
        }

        nextSkillAt.Value = now + DaySkills.CooldownOf(skill);
        return true;
    }

    /// 불붙이기 — 내가 조리 중인 설비를 찾는다. 설비를 쓰고 있지 않으면 발동하지 않는다.
    bool IgniteServer()
    {
        var cafe = CafeServer();
        if (cafe == null) return false;
        foreach (var gauge in cafe.Gauges)
            if (gauge != null && gauge.Station != null && gauge.Station.IgniteServer(OwnerClientId)) return true;
        return false;
    }

    /// 삼키기 — 1회 1개. 이미 그만큼 진행된 식기에는 걸지 않는다.
    bool SwallowServer()
    {
        if (carry == null || carry.Reserved || !carry.Held.Dirty || carry.Held.WashProgress >= DaySkills.SwallowProgress) return false;
        var item = carry.Held;
        item.WashProgress = DaySkills.SwallowProgress;
        carry.SetServer(item);
        return true;
    }

    /// 정제를 쓴다. 장전돼 있었으면 true.
    public bool ConsumeRefineServer()
    {
        if (!IsServer || !refineReady.Value) return false;
        refineReady.Value = false;
        return true;
    }

    /// 지름길 — 모든 피어가 같은 쌍의 충돌을 끈다. 서버만 끄면 소유자 예측이 막혀 화해가 당긴다.
    /// 끝은 `LocalTime`으로 잰다. 클라이언트의 `ServerTime`은 RTT+버퍼만큼 늦어 서버보다 한참 늦게 끝난다.
    /// `LocalTime`이면 오차가 반 RTT 안쪽으로 줄어든다. 서버에서는 두 시각이 같다.
    void OnShortcut(double _, double until)
    {
        var seconds = (float)(until - NetworkManager.LocalTime.Time);
        if (seconds > 0f) PassThroughAsync(seconds).Forget();
    }

    async UniTaskVoid PassThroughAsync(float seconds)
    {
        SetPassThrough(true);
        var cancelled = await UniTask.Delay(System.TimeSpan.FromSeconds(seconds),
            cancellationToken: this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();
        if (!cancelled) SetPassThrough(false);
    }

    /// 발동과 종료에 한 번씩 도는 순회다. 효과 도중 새로 스폰된 플레이어는 막힌다 (2.5초라 무시한다).
    void SetPassThrough(bool ignore)
    {
        if (controller == null || NetworkManager == null || NetworkManager.SpawnManager == null) return;
        foreach (var player in NetworkManager.SpawnManager.PlayerObjects)
        {
            if (player == null || player == NetworkObject || !player.TryGetComponent<CharacterController>(out var other)) continue;
            // 둘이 겹쳤으면 늦게 끝나는 쪽이 쌍을 되돌린다. 시계가 아니라 종료 시각을 비교해야
            // 두 피어 시계 오차로 서로 미루다 영구히 통과로 남는 일이 없다.
            var keep = ignore || (player.TryGetComponent<PlayerCharacter>(out var pc) && pc.shortcutUntil.Value > shortcutUntil.Value);
            Physics.IgnoreCollision(controller, other, keep);
        }
    }

    // --- 밤 액티브 (기획서 9.2) ---

    /// 스킬 키를 눌렀다. 소유자만 부를 수 있다.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void UseSkillRpc()
    {
        if (director != null && director.Phase.Current == Phase.Day)
        {
            UseDaySkillServer();
            return;
        }
        var skill = Skill;
        if (!NightSkills.Exists(skill)) return;

        // 밤에만 쓴다 (기획서 9.2: "밤의 경우"). 낮에는 발동 자체가 없다.
        if (director == null || director.Phase == null ||
            !director.Phase.Started || director.Phase.Current != Phase.Night) return;

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
        if (director == null) return false;

        var hit = false;
        foreach (var box in director.Boxes)
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
