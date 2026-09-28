using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

/// 액티브 하나를 발동시키는 자리. **능력 10종이 공유하는 것만 여기 있다** (기획서 9.1.2 · 9.2).
///
/// 원래는 `PlayerCharacter` 한 클래스가 픽·쿨다운·10종의 본문·복제 상태·연출 통지를 전부
/// 들고 있었다(509줄, switch 두 벌). 그다음엔 능력 10종을 각각 `NetworkBehaviour`로 쪼갰는데,
/// NGO가 스폰 뒤에 컴포넌트를 못 붙여서 **모든 플레이어가 남의 능력 8개를 달고 다녔다.**
///
/// 지금은 **프리팹이 능력을 모른다.** 뽑은 캐릭터에 맞는 객체를 런타임에 하나씩 만들고
/// (`AbilityFactory`), 복제가 필요한 것만 여기가 대신 든다:
///
/// | 여기 | 능력 객체 |
/// |---|---|
/// | 소유자만 부르는 요청 · 페이즈 게이트 · 쿨다운(「각인」 할인) | 발동 조건과 하는 일 |
/// | 지속 슬롯 1 · 플래그 1 (복제) | 그 슬롯을 어떻게 쓸지 |
/// | 연출 통지 (전원/팀) | 무엇을 언제 터뜨릴지 |
///
/// **슬롯이 하나씩이면 되는 이유**는 플레이어당 액티브가 낮 1 · 밤 1뿐이라서다. 활공·지름길
/// (지속)과 정제(플래그)는 서로 다른 캐릭터의 낮 스킬이라 한 명에게 동시에 오지 않는다.
///
/// **대시는 공통 슬롯에 따로 있다.** 전원이 갖고 표에서 오지 않으며 쿨타임 칸도 따로다 —
/// 낮·밤 칸을 같이 쓰면 대시 한 번에 액티브가 잠긴다. 발동 경로만 여기로 왔고, 돌진과
/// 피격의 매 틱 물리는 `DashHarass`가 그대로 든다.
[RequireComponent(typeof(PlayerCharacter))]
[RequireComponent(typeof(PlayerTeam))]
public class PlayerAbilities : NetworkBehaviour
{
    /// 다음 액티브를 쓸 수 있는 서버 시각. 소유자만 읽으면 되므로 쿨다운 표시도 소유자 몫이다.
    readonly NetworkVariable<double> nextAt = new(0d,
        NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

    /// 마지막으로 건 쿨타임의 길이. 「각인」 보석이 줄이므로 표가 아니라 서버가 준다 (기획서 8.2).
    readonly NetworkVariable<float> cooldown = new(0f,
        NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

    /// 지속 효과가 끝나는 서버 시각. **전원이 읽는다** — 지름길은 피어마다 물리를 풀어야 하고,
    /// 활공은 관전자 화면에도 붙어야 한다.
    readonly NetworkVariable<double> durationUntil = new();

    /// 1회성 장전 (정제). 소유자만 읽으면 된다 — 남의 장전을 알 이유가 없다.
    readonly NetworkVariable<bool> flag = new(false, NetworkVariableReadPermission.Owner);

    /// 공통 액티브(대시)의 다음 발동 시각과 마지막 쿨타임 길이. 낮·밤 칸과 나뉜 이유는
    /// 둘이 서로를 잠그면 안 되기 때문이다.
    readonly NetworkVariable<double> commonNextAt = new(0d,
        NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

    readonly NetworkVariable<float> commonCooldown = new(0f,
        NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

    PlayerCharacter character;
    PlayerTeam team;
    MatchDirector director;
    GamePhase subscribedPhase;
    PlayerInventory inventory;

    IDayAbility day;
    INightAbility night;

    /// 지속 슬롯 한 번의 "생애"를 묶는 토큰. 슬롯이 바뀔 때마다(재발동·연장·페이즈 리셋·
    /// 캐릭터 교체) 갈아 끼운다 — 이전 생애에 걸린 비동기 대기(`ShortcutAbility.PassThroughAsync`
    /// 등)가 새 생애의 상태를 건드리지 않게 끊는 자리다. 능력별로 따로 두지 않는 이유는
    /// 슬롯 자체가 라우터 하나에 하나뿐이라서다 (`durationUntil`).
    CancellationTokenSource durationCts;

    /// 픽과 무관하므로 `Rebuild`가 건드리지 않는다. 한 번 만들고 끝이다.
    readonly ICommonAbility common = new DashAbility();

    public event System.Action<EffectId, Vector3, float> EffectPlayed;

    /// 캐릭터에 붙는 지속 연출이 바뀌었다. `EffectId.None`이면 끄라는 뜻이다.
    /// 화면은 이 값만 본다 — 어떤 능력인지 클라이언트가 알 필요가 없다.
    public event System.Action<EffectId, float> AttachedChanged;

    public float CooldownDuration => cooldown.Value;

    public float CooldownRemaining
    {
        get
        {
            if (NetworkManager == null || !NetworkManager.IsListening) return 0f;
            return Mathf.Max(0f, (float)(nextAt.Value - NetworkManager.ServerTime.Time));
        }
    }

    public float CommonCooldownDuration => commonCooldown.Value;

    public float CommonCooldownRemaining
    {
        get
        {
            if (NetworkManager == null || !NetworkManager.IsListening) return 0f;
            return Mathf.Max(0f, (float)(commonNextAt.Value - NetworkManager.ServerTime.Time));
        }
    }

    // --- 능력이 쓰는 것 ---

    public double ServerTime => NetworkManager != null ? NetworkManager.ServerTime.Time : 0d;
    public double DurationUntil => durationUntil.Value;
    public bool Flag => flag.Value;

    /// 미끼는 진짜를 만드는 쪽의 프리팹을 빌린다. 따로 꽂으면 진짜만 바뀌었을 때 미끼가 들킨다.
    public ItemBox DecoyBox => director != null ? director.BoxPrefab : null;
    public BuriedBag DecoyBag => inventory != null ? inventory.BuriedBagPrefab : null;
    public int TeamId => team != null ? team.Team : -1;

    /// 같은 오브젝트의 부품들. 능력이 `GetComponent`를 반복하지 않게 여기서 한 번 잡는다.
    public PlayerCarry Carry { get; private set; }
    public FogOfWar Fog { get; private set; }
    public CharacterController Controller { get; private set; }

    /// 공통 액티브가 미는 몸. 돌진·넉백의 실제 처리는 전부 저쪽에 있다.
    public DashHarass Dash { get; private set; }

    public float DurationRemaining => IsSpawned
        ? Mathf.Max(0f, (float)(durationUntil.Value - ServerTime)) : 0f;

    /// 지금 지속 슬롯 생애에 묶인 취소 토큰. 지속 능력의 비동기 대기(`UniTask.Delay` 등)는
    /// 이것을 받아야 다음 슬롯 변화가 자기를 대신 끊어 줄 수 있다.
    public CancellationToken DurationToken => durationCts?.Token ?? default;

    /// 지금 켜져 있는 지속 능력이 미는 이동 배수. 없거나 꺼져 있으면 1이다.
    /// `PlayerCharacter`는 이 값만 읽는다 — 어떤 능력이 활공인지는 몰라도 된다.
    public float DurationMoveSpeedScale =>
        day is IDurationAbility duration && DurationRemaining > 0f ? duration.MoveSpeedScale : 1f;

    public void SetDurationServer(float seconds)
    {
        if (IsServer) durationUntil.Value = ServerTime + seconds;
    }

    public void SetFlagServer(bool value)
    {
        if (IsServer) flag.Value = value;
    }

    /// 지금 들고 있는 능력이 이 타입이면 준다. 밖에서 특정 능력을 찾을 때 쓴다
    /// (완성 게이지가 「정제」를 소모하는 경로).
    public T Ability<T>() where T : class => day as T ?? night as T;

    void Awake()
    {
        character = GetComponent<PlayerCharacter>();
        team = GetComponent<PlayerTeam>();
        Carry = GetComponent<PlayerCarry>();
        inventory = GetComponent<PlayerInventory>();
        Fog = GetComponent<FogOfWar>();
        Controller = GetComponent<CharacterController>();
        Dash = GetComponent<DashHarass>();
    }

    public override void OnNetworkSpawn()
    {
        MatchDirector.Bind(BindDirector);
        character.CharacterChanged += OnCharacterChanged;
        durationUntil.OnValueChanged += OnDuration;

        Rebuild();
    }

    public override void OnNetworkDespawn()
    {
        MatchDirector.Unbind(BindDirector);
        character.CharacterChanged -= OnCharacterChanged;
        durationUntil.OnValueChanged -= OnDuration;

        if (subscribedPhase != null) subscribedPhase.PhaseEntered -= OnPhaseEntered;
        subscribedPhase = null;

        // 디스폰(재사용 포함)도 지속 생애의 끝이다 — 물리 되돌리기 같은 동기 정리가
        // 파괴 없이도 반드시 돌아야 한다 (`ShortcutAbility.OnDurationChanged`).
        if (day is IDurationAbility duration) duration.OnDurationChanged(this, 0d);
        durationCts?.Cancel();
        durationCts?.Dispose();
        durationCts = null;

        AttachedChanged?.Invoke(EffectId.None, 0f);
    }

    void OnCharacterChanged(int _) => Rebuild();

    /// 픽에 맞는 능력을 다시 만든다. **모든 피어에서 돈다** — 지름길처럼 클라이언트에서도
    /// 반응해야 하는 능력이 있어서, 남의 화면에도 그 사람의 능력 객체가 있어야 한다.
    ///
    /// 이전 능력을 그냥 버리지 않는다. 아직 지속 중이었다면(개발 치트로 캐릭터를 바꾸는
    /// 경우 등) `durationUntil` 슬롯 값은 그대로 남아 새 능력에 그대로 전달되므로, 이전
    /// 능력에게 먼저 "지금 끝났다"를 동기로 알려 자기 몫(물리 되돌리기 등)을 정리하게 한다.
    void Rebuild()
    {
        if (day is IDurationAbility oldDuration) oldDuration.OnDurationChanged(this, 0d);

        day = character.HasPick ? AbilityFactory.Day(character.DaySkill) : null;
        night = character.HasPick ? AbilityFactory.Night(character.Skill) : null;
        OnDuration(0d, durationUntil.Value);
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
    }

    /// 낮 액티브 효과는 그 낮 안에서만 산다. 쿨다운도 페이즈를 넘기지 않는다.
    void OnPhaseEntered(Phase p)
    {
        if (!IsServer) return;

        durationUntil.Value = 0d;
        flag.Value = false;
        nextAt.Value = 0d;
    }

    /// 지속 슬롯이 바뀌었다. 능력에 넘기고, 붙는 연출은 화면에 알린다.
    ///
    /// 매번 토큰을 새로 간다. 재발동·연장으로 슬롯이 다시 바뀌면 이전 생애에 걸린
    /// 비동기 대기(`ShortcutAbility.PassThroughAsync`)가 취소되어 자기 몫의 되돌리기를
    /// 하지 않는다 — 지금 불리는 `OnDurationChanged`가 새 생애의 상태를 확정한다.
    void OnDuration(double _, double until)
    {
        durationCts?.Cancel();
        durationCts?.Dispose();
        durationCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());

        if (day is IDurationAbility duration)
        {
            duration.OnDurationChanged(this, until);
            AttachedChanged?.Invoke(duration.AttachedEffect, DurationRemaining);
            return;
        }
        AttachedChanged?.Invoke(EffectId.None, 0f);
    }

    /// 스킬 키를 눌렀다. 소유자만 부를 수 있다.
    ///
    /// 낮이면 낮 액티브, 밤이면 밤 액티브다. 쿨타임 칸이 하나뿐이라 갈래도 하나다 (기획서 9장).
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void UseRpc()
    {
        if (character == null || !character.HasPick || director == null || director.Phase == null) return;
        if (!director.Phase.Started) return;

        var now = ServerTime;
        if (now < nextAt.Value) return;

        // 전환은 조작을 받지 않는 정산 구간이다 (기획서 4장).
        switch (director.Phase.Current)
        {
            case Phase.Day:
                if (day != null && day.TryCastServer(this)) StartCooldownServer(now, DaySkills.CooldownOf(day.Id));
                break;
            case Phase.Night:
                if (night != null && night.TryCastServer(this)) StartCooldownServer(now, NightSkills.CooldownOf(night.Id));
                break;
        }
    }

    /// 공통 액티브 키(대시)를 눌렀다. 소유자만 부를 수 있다.
    ///
    /// 낮·밤 모두 된다 (기획서 11장 조작표: 낮 = "짧은 거리 대시"). 낮에 없어지는 것은 견제
    /// 효과뿐이고 그 판정은 `DashHarass`가 따로 본다. 전환만 조작을 받지 않는다 (기획서 4장).
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void UseCommonRpc()
    {
        if (director == null || director.Phase == null || !director.Phase.Started) return;
        if (director.Phase.Current == Phase.Transition) return;

        var now = ServerTime;
        if (now < commonNextAt.Value) return;
        if (!common.TryCastServer(this)) return;

        // 「각인」은 낮·밤 액티브만 줄인다 (기획서 8.2). 대시는 9장의 액티브가 아니다.
        commonCooldown.Value = common.CooldownSeconds(this);
        commonNextAt.Value = now + commonCooldown.Value;
    }

    /// 「각인」은 낮·밤 액티브 쿨타임을 모두 줄인다 (기획서 8.2).
    /// 밤에는 카페의 턴 수가 아직 귀환 전 값이라, 원장에서 오늘 일차로 센다.
    void StartCooldownServer(double now, float seconds)
    {
        var ledger = director != null && team != null ? director.LedgerOf(team.Team) : null;
        if (ledger != null && ledger.Gems.Remaining(Gem.Engraving, director.Phase.Day) > 0)
            seconds *= Gems.CooldownScale;

        cooldown.Value = seconds;
        nextAt.Value = now + seconds;
    }

    // --- 연출 통지. 판정은 이미 끝났고, 아래는 그리기 위한 통지뿐이다 ---

    /// 전원에게 보이는 연출. 서버에서 부른다.
    public void CueServer(EffectId id, Vector3 position, float scale = 1f)
    {
        if (IsServer) CueRpc(id, position, scale);
    }

    /// 자기 팀에게만 보이는 연출. 가짜 상자의 설치 연출이 그렇다 (기획서 9.2) —
    /// 전원에게 보내면 그 상자가 가짜라는 것을 상대에게 알려 주는 꼴이 된다.
    public void CueToTeamServer(EffectId id, Vector3 position, float scale = 1f)
    {
        if (!IsServer) return;

        var teamId = TeamId;
        foreach (var client in NetworkManager.ConnectedClientsList)
            if (client.ClientId == OwnerClientId || (teamId >= 0 && PlayerTeam.Of(client.ClientId) == teamId))
                CueToRpc(id, position, scale, RpcTarget.Single(client.ClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    void CueRpc(EffectId id, Vector3 position, float scale) => EffectPlayed?.Invoke(id, position, scale);

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    void CueToRpc(EffectId id, Vector3 position, float scale, RpcParams p = default) =>
        EffectPlayed?.Invoke(id, position, scale);

    /// 완성 게이지가 판정 순간에 찾는다.
    public static PlayerAbilities Of(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.ConnectedClients.TryGetValue(clientId, out var c)) return null;
        return c.PlayerObject != null ? c.PlayerObject.GetComponent<PlayerAbilities>() : null;
    }
}
