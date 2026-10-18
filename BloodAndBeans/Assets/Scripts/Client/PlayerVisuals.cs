using Cysharp.Threading.Tasks;
using DG.Tweening;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

/// 이 플레이어가 화면에서 어떻게 보이는가 — 모델·팀 색·대시/피격 연출·액티브 연출·손 아이템을
/// 한 컴포넌트가 진다.
///
/// **예전에는 넷으로 흩어져 있었다** (`PlayerAppearance`·`PlayerEffects`·`PublicCarryDisplay`
/// + `PlayerCarry`에 붙은 `ItemDisplay`). 넷 다 같은 플레이어 인스턴스의 표현이고 수명도
/// 스폰·디스폰에 같이 걸리는데, 진입점이 넷으로 나뉘어 있으면 "이 플레이어가 화면에서
/// 무엇을 하는지" 보려면 네 파일을 오가야 했다. 이제 모델·팀 색·대시/피격·손 아이템이
/// 이 한 클래스의 `OnNetworkSpawn`/`OnNetworkDespawn`에서 같이 걸리고 같이 풀린다.
/// (공개 소지 표시는 손에 든 것을 전원에게 보이게 하면서 없앴다.)
///
/// **DashPresentation(일반 C# 객체)과 손 슬롯 표시(`ItemSlotPresenter`, 시설과 공유)는
/// 그대로 내부 객체로 둔다.** 새 MonoBehaviour나 새 인터페이스를 더 만들지 않는다 — 이미
/// 일반 C# 객체로 분리돼 있던 것을 그대로 재사용한다.
[RequireComponent(typeof(PlayerTeam))]
[RequireComponent(typeof(PlayerCarry))]
[RequireComponent(typeof(PlayerAbilities))]
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(DashHarass))]
[RequireComponent(typeof(PlayerInventory))]
public class PlayerVisuals : NetworkBehaviour
{
    [Header("모델")]
    /// 캐릭터는 팀을 한눈에 알아봐야 하므로 카페와 달리 그대로 물들인다.
    [SerializeField, Range(0f, 1f)] float tintStrength = 1f;
    [SerializeField] CharacterVisualConfig characterVisuals;
    [SerializeField] Transform modelRoot;
    [Tooltip("이 적재 밴드(LoadBands.BandOf)부터 Walk로 걷는다. 2 = 80% 이상 (18장 DA-17 2단).")]
    [SerializeField, Min(0)] int walkFromBand = 2;
    [Tooltip("이 적재 밴드부터 MoveHeavy로 걷는다. 3 = 100% 이상 (18장 DA-17 3단).")]
    [SerializeField, Min(0)] int heavyFromBand = 3;

    [Header("밤 주변 조명")]
    [SerializeField] Light localNightLight;
    [SerializeField] ScreenEffectFeature trackSense;
    Camera localCamera;
    GamePhase lightingPhase;

    [Header("전송 사라짐")]
    [SerializeField] Material teleportAfterimageMaterial;
    /// 시전 시간(`PlayerTeam.teleportWindupSeconds`)에 맞춰야 도착 직전에 다 사라진다.
    [SerializeField, Min(0.01f)] float teleportAfterimageSeconds = 1.35f;

    [Header("대시/피격 피드백")]
    /// 색·시간·흔들림 수치는 전 플레이어가 공유하는 설정이다 (`PlayerFeedbackConfig`).
    [SerializeField] PlayerFeedbackConfig feedback;
    /// 일반 이동 경로에 남기는 잔상. 멈추면 TrailRenderer의 수명에 따라 사라진다.
    [SerializeField] TrailRenderer trail;
    /// 흔들림을 쏘는 곳. 가상 카메라의 `CinemachineImpulseListener`가 받는다.
    [SerializeField] CinemachineImpulseSource impulse;

    [Header("손 아이템 표시")]
    [SerializeField] ItemVisualConfig itemVisuals;
    /// 아이템이 놓일 자리. `PlayerCarry.SlotCount`(1칸)만큼 있으면 된다.
    [SerializeField] Transform[] itemAnchors;

    /// 들고 있는 식기의 자리. 제조 카드가 그 바로 위에 뜬다 (기획서 5.7.3).
    public Transform HeldAnchor => itemAnchors is { Length: > 0 } && itemAnchors[0] != null ? itemAnchors[0] : transform;

    [SerializeField] float itemHighlightScale = 1.4f;
    [SerializeField] Vector3 itemHighlightOffset = new(0f, 0.1f, 0f);

    PlayerTeam playerTeam;
    PlayerCharacter character;
    PlayerCarry carry;
    PlayerAbilities abilities;
    PlayerStatus status;
    PlayerController interaction;
    DashHarass dash;
    PlayerInventory inventory;

    CharacterModelSpawner modelSpawner;
    ItemSlotPresenter itemPresenter;
    DashPresentation dashPresentation;

    /// 손 앵커를 붙여 둔 모델. 모델을 갈기 전에 비운다 — 소켓에 붙은 채 모델이 파괴되면 앵커도 같이 사라진다.
    CharacterModel handModel;
    int spawnRequest;
    Transform[] anchorHomes;
    Vector3[] anchorHomePositions;
    Quaternion[] anchorHomeRotations;
    Vector3[] anchorHomeScales;

    Tween flash;
    ParticleSystem attached;

    /// 지금 서 있는 모델. 캐릭터를 고르지 않았으면 null이다.
    public CharacterModel Model => modelSpawner?.Model;

    void Awake()
    {
        if (localNightLight != null) localNightLight.enabled = false;
        playerTeam = GetComponent<PlayerTeam>();
        character = GetComponent<PlayerCharacter>();
        carry = GetComponent<PlayerCarry>();
        abilities = GetComponent<PlayerAbilities>();
        status = GetComponent<PlayerStatus>();
        interaction = GetComponent<PlayerController>();
        dash = GetComponent<DashHarass>();
        inventory = GetComponent<PlayerInventory>();

        // 몸통 캡슐은 걷기 위해 skinWidth만큼 떠 있다 (PlayerController.OnNetworkSpawn). 모델만 그만큼 내려 발을 바닥에 붙인다.
        var body = GetComponent<CharacterController>();
        if (modelRoot != null && body != null) modelRoot.localPosition += Vector3.down * body.skinWidth;

        modelSpawner = new CharacterModelSpawner(characterVisuals);
        itemPresenter = new ItemSlotPresenter(itemVisuals, itemAnchors, itemHighlightScale, itemHighlightOffset);

        var anchorCount = itemAnchors?.Length ?? 0;
        anchorHomes = new Transform[anchorCount];
        anchorHomePositions = new Vector3[anchorCount];
        anchorHomeRotations = new Quaternion[anchorCount];
        anchorHomeScales = new Vector3[anchorCount];
        for (var i = 0; i < anchorCount; i++)
        {
            if (itemAnchors[i] == null) continue;
            anchorHomes[i] = itemAnchors[i].parent;
            anchorHomePositions[i] = itemAnchors[i].localPosition;
            anchorHomeRotations[i] = itemAnchors[i].localRotation;
            anchorHomeScales[i] = itemAnchors[i].localScale;
        }

        if (trail != null) trail.emitting = false;
        dashPresentation = new DashPresentation(this, impulse, GetComponent<NetworkObject>(),
            feedback != null ? feedback.hitFlash : Color.white,
            feedback != null ? feedback.spillFlash : new Color(1f, 0.65f, 0.15f),
            feedback != null ? feedback.flashSeconds : 0.16f,
            feedback != null ? feedback.impactScale : 1f,
            feedback != null ? feedback.shakeOnLand : 0.10f,
            feedback != null ? feedback.shakeOnTaken : 0.28f,
            feedback != null ? feedback.shakeOnSpill : 0.45f);

        if (feedback == null)
            CDebug.LogError($"{name}: PlayerFeedbackConfig가 비었다. 대시/피격 연출이 기본값으로 돈다.", this);
    }

    public override void OnNetworkSpawn()
    {
        if (IsClient && IsOwner) MatchDirector.Bind(OnLightingDirectorReady);
        playerTeam.TeamChanged += OnTeamChanged;
        playerTeam.Teleported += OnTeleported;
        playerTeam.TeleportStarted += OnTeleportStarted;
        character.CharacterChanged += SetCharacter;
        abilities.EffectPlayed += OnEffect;
        abilities.AttachedChanged += OnAttached;
        if (status != null) status.Applied += OnStatusApplied;
        interaction.InteractionSucceeded += OnInteractionSucceeded;
        interaction.SoundPlayed += OnSound;
        interaction.MotionPlayed += OnMotion;
        dash.DashStarted += OnDashSound;
        dash.DashStarted += dashPresentation.OnDashStarted;
        dash.HitLanded += dashPresentation.OnHitLanded;
        dash.TookHit += dashPresentation.OnTookHit;
        carry.ContentsChanged += RefreshHand;
        carry.RecipeEffectPlayed += OnRecipeEffect;
        inventory.LoadChanged += RefreshBag;
        inventory.LoadChanged += RefreshGait;

        SetCharacter(character.Index);
        OnTeamChanged(playerTeam.Team);
        RefreshHand();
        if (trail != null)
        {
            trail.Clear();
            trail.emitting = true;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner && trackSense != null) trackSense.Stop(localCamera);
        localCamera = null;
        MatchDirector.Unbind(OnLightingDirectorReady);
        if (lightingPhase != null) lightingPhase.PhaseEntered -= RefreshNightLight;
        lightingPhase = null;
        if (localNightLight != null) localNightLight.enabled = false;
        flash?.Kill();
        flash = null;

        if (playerTeam != null) playerTeam.TeamChanged -= OnTeamChanged;
        if (playerTeam != null) playerTeam.Teleported -= OnTeleported;
        if (playerTeam != null) playerTeam.TeleportStarted -= OnTeleportStarted;
        if (character != null) character.CharacterChanged -= SetCharacter;
        if (abilities != null) { abilities.EffectPlayed -= OnEffect; abilities.AttachedChanged -= OnAttached; }
        if (status != null) status.Applied -= OnStatusApplied;
        if (interaction != null) interaction.InteractionSucceeded -= OnInteractionSucceeded;
        if (interaction != null) interaction.SoundPlayed -= OnSound;
        if (interaction != null) interaction.MotionPlayed -= OnMotion;
        if (dash != null)
        {
            dash.DashStarted -= OnDashSound;
            dash.DashStarted -= dashPresentation.OnDashStarted;
            dash.HitLanded -= dashPresentation.OnHitLanded;
            dash.TookHit -= dashPresentation.OnTookHit;
        }
        if (carry != null)
        {
            carry.ContentsChanged -= RefreshHand;
            carry.RecipeEffectPlayed -= OnRecipeEffect;
        }
        if (inventory != null) { inventory.LoadChanged -= RefreshBag; inventory.LoadChanged -= RefreshGait; }

        dashPresentation.Cancel();
        if (trail != null)
        {
            trail.emitting = false;
            trail.Clear();
        }
        StopAttached();
        DetachHand();
        modelSpawner.Clear();
        itemPresenter.Clear();
    }

    void OnLightingDirectorReady(MatchDirector ready)
    {
        if (lightingPhase != null) lightingPhase.PhaseEntered -= RefreshNightLight;
        lightingPhase = ready != null ? ready.Phase : null;
        if (lightingPhase == null) return;
        lightingPhase.PhaseEntered += RefreshNightLight;
        RefreshNightLight(lightingPhase.Current);
    }

    // 내 화면에서 내 주변만 밝힌다. 안개 걷힘과 다른 플레이어의 시야는 바꾸지 않는다.
    void RefreshNightLight(Phase phase)
    {
        if (localNightLight != null) localNightLight.enabled = IsClient && IsOwner && phase == Phase.Night;
        if (phase != Phase.Night && trackSense != null) trackSense.Stop(localCamera);
    }

    // --- 모델·팀 색 (예전 PlayerAppearance) ---

    /// 잠깐 다른 색으로 물들였다가 팀 색으로 되돌린다. 대시에 맞은 순간을 알리는 데 쓴다.
    ///
    /// 되돌릴 색을 아는 것은 팀 색을 소유한 이쪽뿐이고, 밖에서 되돌리게 하면 팀 배정이
    /// 바뀌는 순간 어긋난다.
    public void FlashClient(Color color, float seconds)
    {
        flash?.Kill();

        var team = TeamColors.Of(playerTeam.Team);
        flash = DOVirtual.Color(color, team, Mathf.Max(0.01f, seconds), Tint)
                         .SetLink(gameObject)
                         .OnKill(() => Apply(playerTeam.Team));
    }

    void SetCharacter(int index)
    {
        if (modelRoot == null) return;

        if (!CharacterCatalog.IsValid(index))
        {
            DetachHand();
            modelSpawner.Clear();
            Apply(playerTeam.Team);
            return;
        }

        SpawnCharacterAsync(CharacterCatalog.All[index].Id).Forget();
    }

    /// 모델을 불러와 세운다. 같은 캐릭터를 쓰는 다른 참가자를 위해 능력 연출도 함께 준비한다
    /// (`EffectManager.NoteCharacterAsync`) — 모델과 별개 몫이라 한쪽이 실패해도 다른 쪽을 막지 않는다.
    async UniTaskVoid SpawnCharacterAsync(CharacterId id)
    {
        var ct = this.GetCancellationTokenOnDestroy();
        EffectManager.NoteCharacterAsync(id, ct).Forget();
        DetachHand();
        var request = spawnRequest;
        await modelSpawner.SpawnAsync(id, modelRoot, gameObject.layer, ct);
        // 그사이 다른 교체가 왔으면 지금 모델은 곧 파괴된다. 앵커를 붙이지 않는다.
        if (request == spawnRequest)
        {
            handModel = Model;
            PlaceHand();
            RefreshBag();
            shownGait = GaitNow();   // 모델을 새로 세운 것이지 무게가 늘어난 것이 아니다
            RefreshGait();
        }
        Apply(playerTeam.Team);
    }

    void OnTeamChanged(int team) => Apply(team);

    void Apply(int team) => Tint(TeamColors.Of(team));

    void Tint(Color color)
    {
        if (Model != null) Model.Tint(color, tintStrength);
    }

    // --- 액티브 연출 (예전 PlayerEffects) ---

    void OnMotion(BodyMotion motion)
    {
        if (Model != null) Model.PlayMotion(motion);
    }

    /// 스킬 시전 몸짓은 둘뿐이다 (18장 5절). 무엇을 썼는지는 이펙트가 말한다.
    /// 이 이벤트는 시전자 자신의 `PlayerAbilities`에서 시전 순간 한 번 오른다.
    static BodyMotion CastMotionOf(EffectId id) => id switch
    {
        EffectId.BatSwarm or EffectId.ArmGrab or EffectId.FrogCurse or EffectId.PotionPuddle => BodyMotion.Throw,
        EffectId.CoffinImpact or EffectId.Howl => BodyMotion.Cast,
        _ => BodyMotion.None,
    };

    void OnEffect(EffectId id, Vector3 position, float scale, float seconds)
    {
        OnMotion(CastMotionOf(id));
        if (id == EffectId.TrackSniff)
        {
            if (IsClient && IsOwner && trackSense != null)
            {
                // 플레이어 스폰 뒤 매치 씬의 카메라가 생길 수 있어 발동 사건에서 잡는다.
                localCamera = Camera.main;
                trackSense.Play(localCamera, NightSkills.TrackRevealSeconds);
            }
            return;
        }
        EffectManager.Play(id, position, scale, seconds);
    }

    /// 남이 건 스킬의 연출. 둘 이상 겹칠 수 있어 각자 시간이 다 되면 스스로 멈춘다.
    void OnStatusApplied(EffectId id, float seconds)
    {
        EffectManager.PlayAttached(id, transform, seconds);
        if (id == EffectId.Dizzy) OnMotion(BodyMotion.Collapse);   // 물약에 미끄러져 넘어졌다 (18장 4절)
        if (id != EffectId.ZombieWalk) return;

        // 물렸다 (기획서 9.1.2). 걸린 시간 동안 좀비걸음으로 걷는다 (18장 DA-35).
        zombie = true;
        RefreshGait();
        zombieEnd?.Kill();
        zombieEnd = DOVirtual.DelayedCall(seconds, () => { zombie = false; RefreshGait(); }).SetLink(gameObject);
    }

    bool zombie;
    Tween zombieEnd;

    /// 걸음걸이 (18장 DA-17 · DA-35). 남의 화면에는 80% 이상인지만 복제되므로(기획서 6.6)
    /// 정확한 밴드로 고르는 것은 소유자뿐이고, 남의 캐릭터는 80% 이상이면 Walk까지만 보인다.
    void RefreshGait()
    {
        if (handModel == null) return;
        var gait = GaitNow();
        // 무게 구간이 무거운 쪽으로 넘어가면 배낭에 끌려 주저앉는다 (18장 NA-10).
        if (gait != CharacterModel.Gait.Zombie && shownGait != CharacterModel.Gait.Zombie && gait > shownGait)
            handModel.PlayMotion(BodyMotion.Collapse);
        shownGait = gait;
        handModel.PlayGait(gait);
    }

    CharacterModel.Gait shownGait;

    CharacterModel.Gait GaitNow()
    {
        if (zombie) return CharacterModel.Gait.Zombie;
        if (inventory == null || !inventory.BagVisible) return CharacterModel.Gait.Run;

        var band = IsOwner ? LoadBands.BandOf(inventory.LoadRatio) : inventory.Overloaded ? walkFromBand : 0;
        return band >= heavyFromBand ? CharacterModel.Gait.Heavy
             : band >= walkFromBand ? CharacterModel.Gait.Walk
             : CharacterModel.Gait.Run;
    }

    void OnTeleportStarted(Vector3 origin)
    {
        dashPresentation.Cancel();
        OnMotion(BodyMotion.Return);   // 빛기둥에 빨려 올라간다 (18장 NA-11)
        if (trail != null)
        {
            trail.emitting = false;
            trail.Clear();
        }
        EffectManager.Play(EffectId.TeleportDeparture, origin);
        if (teleportAfterimageMaterial == null)
            CDebug.LogError("전송 머티리얼이 연결되지 않았다.", this);
        else if (Model != null)
            Model.FadeOut(teleportAfterimageMaterial, teleportAfterimageSeconds);
    }

    void OnTeleported(Vector3 origin, Vector3 destination, Quaternion rotation)
    {
        // 전송 전의 이동 궤적을 지우고 도착 후 다시 그린다.
        dashPresentation.Cancel();
        if (trail != null)
        {
            trail.Clear();
            trail.emitting = true;
        }
        EffectManager.Play(EffectId.TeleportArrival, destination);
        if (Model != null)
        {
            Model.RestoreLook();
            Apply(playerTeam.Team);
        }
    }

    /// 상호작용 성공 — 노란 오각별. `SuccessRpc`는 소유자에게만 오므로 이 핸들러는 다른
    /// 사람 화면에서는 그냥 불리지 않는다.
    void OnInteractionSucceeded(Vector3 position) => EffectManager.Play(EffectId.InteractionSuccess, position);

    void OnSound(SfxCue cue, Vector3 position, int team) =>
        SoundManager.Instance?.PlayCue(cue, position, team);

    void OnDashSound(float _, Vector3 direction) => OnSound(SfxCue.Dash, transform.position, playerTeam.Team);

    /// 지속 효과가 캐릭터에 붙는다 (활공 등). **어떤 능력인지는 모른다** — 라우터가
    /// 연출 id와 남은 시간만 준다.
    void OnAttached(EffectId id, float seconds)
    {
        StopAttached();
        if (id == EffectId.None || seconds <= 0f) return;

        attached = EffectManager.PlayAttached(id, transform, seconds);
    }

    void StopAttached()
    {
        if (attached == null) return;
        EffectManager.Stop(attached);
        attached = null;
    }

    // --- 손 아이템 표시 (예전 PlayerCarry의 ItemDisplay) ---

    void OnRecipeEffect(EffectId id)
    {
        PlaceHand();
        if (itemAnchors == null || itemAnchors.Length == 0 || itemAnchors[0] == null)
        {
            CDebug.LogError($"{name}: 조합 VFX를 붙일 손 앵커가 없습니다.", this);
            return;
        }
        EffectManager.PlayAttached(id, itemAnchors[0], 0f);
    }
    void RefreshHand()
    {
        if (carry == null) return;
        PlaceHand();
        itemPresenter.Bind(carry);
    }

    void DetachHand()
    {
        spawnRequest++;
        handModel = null;
        PlaceHand();
    }

    /// 손 앵커를 든 것에 맞는 모델 소켓으로 옮기고 들기 자세를 맞춘다. 모델이나 소켓이 없으면 플레이어의 원래 자리로 돌린다.
    void PlaceHand()
    {
        if (handModel != null)
            handModel.PlayHold(carry != null ? carry.SlotAt(0) : CarryView.Nothing);
        if (itemAnchors == null) return;

        for (var i = 0; i < itemAnchors.Length; i++)
        {
            var anchor = itemAnchors[i];
            if (anchor == null) continue;

            var view = carry != null && i < carry.SlotCount ? carry.SlotAt(i) : CarryView.Nothing;
            var socket = handModel != null ? handModel.HandSocketFor(view) : null;
            if (socket != null)
            {
                anchor.SetParent(socket, false);
                anchor.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                // 리그 단위(×100)와 캐릭터 배율을 상쇄해 아이템이 원래 자리와 같은 월드 크기로 보이게 한다.
                var home = anchorHomes[i] != null ? anchorHomes[i].lossyScale : Vector3.one;
                anchor.localScale = Vector3.Scale(Vector3.Scale(home, anchorHomeScales[i]),
                    new Vector3(1f / socket.lossyScale.x, 1f / socket.lossyScale.y, 1f / socket.lossyScale.z));
            }
            else
            {
                anchor.SetParent(anchorHomes[i], false);
                anchor.SetLocalPositionAndRotation(anchorHomePositions[i], anchorHomeRotations[i]);
                anchor.localScale = anchorHomeScales[i];
            }
        }
    }

    /// 묻으면 등의 가방이 꺼진다. 전원에게 보인다 — 적이 묻은 곳을 찾는 단서다 (기획서 6.7). 낮에도 꺼진다.
    void RefreshBag()
    {
        if (Model != null) Model.ShowBag(inventory.BagVisible);
    }
}
