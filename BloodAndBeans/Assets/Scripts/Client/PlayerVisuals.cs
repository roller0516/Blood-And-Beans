using Cysharp.Threading.Tasks;
using DG.Tweening;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

/// 이 플레이어가 화면에서 어떻게 보이는가 — 모델·팀 색·대시/피격 연출·액티브 연출·손 아이템·
/// 공개 소지 표시를 한 컴포넌트가 진다.
///
/// **예전에는 넷으로 흩어져 있었다** (`PlayerAppearance`·`PlayerEffects`·`PublicCarryDisplay`
/// + `PlayerCarry`에 붙은 `ItemDisplay`). 넷 다 같은 플레이어 인스턴스의 표현이고 수명도
/// 스폰·디스폰에 같이 걸리는데, 진입점이 넷으로 나뉘어 있으면 "이 플레이어가 화면에서
/// 무엇을 하는지" 보려면 네 파일을 오가야 했다. 이제 모델·팀 색·대시/피격·손 아이템·
/// 공개 소지가 이 한 클래스의 `OnNetworkSpawn`/`OnNetworkDespawn`에서 같이 걸리고 같이 풀린다.
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

    [Header("밤 주변 조명")]
    [SerializeField] Light localNightLight;
    GamePhase lightingPhase;

    [Header("전송 잔상")]
    [SerializeField] Material teleportAfterimageMaterial;
    [SerializeField, Min(0.01f)] float teleportAfterimageSeconds = 1.35f;
    [SerializeField, Min(0f)] float teleportAfterimageRise = 0.65f;

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

    [Header("공개 소지 표시")]
    /// 상대 손은 내용물 없이 소지·오염·블러드 빈 빛만 표시한다 (기획서 5.4.1).
    [SerializeField] Renderer publicMarker;
    [SerializeField] Color publicHeldColor = Color.white;
    [SerializeField] Color publicDirtyColor = new(0.35f, 0.2f, 0.1f);
    [SerializeField] Color publicBloodColor = Color.red;

    PlayerTeam playerTeam;
    PlayerCharacter character;
    PlayerCarry carry;
    PlayerAbilities abilities;
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

    Tween flash;
    ParticleSystem attached;
    MaterialPropertyBlock publicProps;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    /// 지금 서 있는 모델. 캐릭터를 고르지 않았으면 null이다.
    public CharacterModel Model => modelSpawner?.Model;

    void Awake()
    {
        if (localNightLight != null) localNightLight.enabled = false;
        playerTeam = GetComponent<PlayerTeam>();
        character = GetComponent<PlayerCharacter>();
        carry = GetComponent<PlayerCarry>();
        abilities = GetComponent<PlayerAbilities>();
        interaction = GetComponent<PlayerController>();
        dash = GetComponent<DashHarass>();
        inventory = GetComponent<PlayerInventory>();

        modelSpawner = new CharacterModelSpawner(characterVisuals);
        itemPresenter = new ItemSlotPresenter(itemVisuals, itemAnchors, itemHighlightScale, itemHighlightOffset);
        publicProps = new MaterialPropertyBlock();

        var anchorCount = itemAnchors?.Length ?? 0;
        anchorHomes = new Transform[anchorCount];
        anchorHomePositions = new Vector3[anchorCount];
        anchorHomeRotations = new Quaternion[anchorCount];
        for (var i = 0; i < anchorCount; i++)
        {
            if (itemAnchors[i] == null) continue;
            anchorHomes[i] = itemAnchors[i].parent;
            anchorHomePositions[i] = itemAnchors[i].localPosition;
            anchorHomeRotations[i] = itemAnchors[i].localRotation;
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
        interaction.InteractionSucceeded += OnInteractionSucceeded;
        interaction.SoundPlayed += OnSound;
        dash.DashStarted += OnDashSound;
        dash.DashStarted += dashPresentation.OnDashStarted;
        dash.HitLanded += dashPresentation.OnHitLanded;
        dash.TookHit += dashPresentation.OnTookHit;
        carry.ContentsChanged += RefreshHand;
        carry.RecipeEffectPlayed += OnRecipeEffect;
        carry.ContentsChanged += RefreshPublicMarker;
        inventory.LoadChanged += RefreshBag;

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
        if (interaction != null) interaction.InteractionSucceeded -= OnInteractionSucceeded;
        if (interaction != null) interaction.SoundPlayed -= OnSound;
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
            carry.ContentsChanged -= RefreshPublicMarker;
        }
        if (inventory != null) inventory.LoadChanged -= RefreshBag;

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
        }
        Apply(playerTeam.Team);
    }

    void OnTeamChanged(int team)
    {
        Apply(team);
        ApplyItemTeamLayer(team);
        RefreshPublicMarker();
    }

    void Apply(int team) => Tint(TeamColors.Of(team));

    void Tint(Color color)
    {
        if (Model != null) Model.Tint(color, tintStrength);
    }

    // --- 액티브 연출 (예전 PlayerEffects) ---

    void OnEffect(EffectId id, Vector3 position, float scale) => EffectManager.Play(id, position, scale);

    void OnTeleportStarted(Vector3 origin)
    {
        dashPresentation.Cancel();
        if (trail != null)
        {
            trail.emitting = false;
            trail.Clear();
        }
        EffectManager.Play(EffectId.TeleportDeparture, origin);
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
            TeleportAfterimage.Play(Model.transform, transform, origin, rotation,
                teleportAfterimageMaterial, teleportAfterimageSeconds, teleportAfterimageRise);
    }

    /// 상호작용 성공 — 노란 오각별. `SuccessRpc`는 소유자에게만 오므로 이 핸들러는 다른
    /// 사람 화면에서는 그냥 불리지 않는다.
    void OnInteractionSucceeded(Vector3 position) => EffectManager.Play(EffectId.InteractionSuccess, position);

    void OnSound(SfxCue cue, Vector3 position, int team) =>
        SoundManager.Instance?.PlayCue(cue, position, team);

    void OnDashSound(float _) => OnSound(SfxCue.Dash, transform.position, playerTeam.Team);

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
            }
            else
            {
                anchor.SetParent(anchorHomes[i], false);
                anchor.SetLocalPositionAndRotation(anchorHomePositions[i], anchorHomeRotations[i]);
            }
        }
    }

    /// 손에 든 것도 팀 밖에서는 보이지 않아야 한다 (기획서 3.1). 손 앵커만 팀 레이어로
    /// 옮겨 막는다 — 플레이어 본체는 Default에 남아야 하기 때문이다.
    void ApplyItemTeamLayer(int myTeam)
    {
        if (itemAnchors == null) return;
        foreach (var anchor in itemAnchors)
            if (anchor != null) TeamVision.ApplyTeamLayer(anchor.gameObject, myTeam);
    }

    /// 묻으면 등의 가방이 꺼진다. 전원에게 보인다 — 적이 묻은 곳을 찾는 단서다 (기획서 6.7). 낮에도 꺼진다.
    void RefreshBag()
    {
        if (Model != null) Model.ShowBag(inventory.BagVisible);
    }

    // --- 공개 소지 표시 (예전 PublicCarryDisplay) ---

    void RefreshPublicMarker()
    {
        if (publicMarker == null || carry == null || playerTeam == null) return;

        publicMarker.enabled = carry.IsSpawned && playerTeam.Team != PlayerTeam.Local() && carry.PublicState != 0;
        publicProps.SetColor(BaseColor,
            carry.BloodGlow ? publicBloodColor : carry.PublicState == 2 ? publicDirtyColor : publicHeldColor);
        publicMarker.SetPropertyBlock(publicProps);
    }
}
