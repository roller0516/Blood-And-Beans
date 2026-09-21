using DG.Tweening;
using Unity.Netcode;
using UnityEngine;

/// 캐릭터를 팀 색으로 물들인다. 밤에는 두 팀이 같은 숲에 서고 서로를 방해할 수 있으므로
/// (기획서 6.6), 누가 아군인지는 표시가 아니라 게임플레이 정보다.
///
/// PlayerTeam과 나눈 것은 의도다. 자리 배정은 서버 권위이고 색은 보는 쪽 일이라
/// 바뀌는 이유가 다르다 — CustomerLook이 Customer와 나뉜 것과 같다.
[RequireComponent(typeof(PlayerTeam))]
public class PlayerLook : NetworkBehaviour
{
    /// 캐릭터는 팀을 한눈에 알아봐야 하므로 카페와 달리 그대로 물들인다.
    [SerializeField, Range(0f, 1f)] float tintStrength = 1f;

    [SerializeField] CharacterVisualConfig visuals;
    [SerializeField] Transform modelRoot;
    [SerializeField] ParticleSystem interactionSuccessPrefab;
    [SerializeField] Vector3 interactionSuccessOffset = new(0f, 0.25f, 0f);
    PlayerInteractor interaction;
    PlayerCharacter character;
    GameObject model;
    CharacterModel appearance;
    PlayerTeam playerTeam;
    Tween flash;

    /// 지금 서 있는 모델. 캐릭터를 고르지 않았으면 null이다.
    public CharacterModel Model => appearance;

    void Awake() { playerTeam = GetComponent<PlayerTeam>(); character = GetComponent<PlayerCharacter>(); }

    public override void OnNetworkSpawn()
    {
        interaction = GetComponent<PlayerInteractor>();
        if (IsOwner && interaction != null) interaction.InteractionSucceeded += OnInteractionSucceeded;
        playerTeam.TeamChanged += Apply;
        character.CharacterChanged += SetCharacter;
        SetCharacter(character.Index);
        Apply(playerTeam.Team);
    }

    public override void OnNetworkDespawn()
    {
        if (interaction != null) interaction.InteractionSucceeded -= OnInteractionSucceeded;
        flash?.Kill();
        flash = null;
        if (playerTeam != null) playerTeam.TeamChanged -= Apply;
        if (character != null) character.CharacterChanged -= SetCharacter;
    }

    void OnInteractionSucceeded(Vector3 position)
    {
        if (interactionSuccessPrefab == null) return;
        var effect = Instantiate(interactionSuccessPrefab, position + interactionSuccessOffset, Quaternion.identity);
        effect.Play(true);
    }

    /// 잠깐 다른 색으로 물들였다가 팀 색으로 되돌린다. 대시에 맞은 순간을 알리는 데 쓴다.
    ///
    /// 연출을 시키는 것은 `DashVisuals`지만 이 메서드는 여기 있다. 되돌릴 색을 아는 것은
    /// 팀 색을 소유한 이쪽뿐이고, 밖에서 되돌리게 하면 팀 배정이 바뀌는 순간 어긋난다.
    public void FlashClient(Color color, float seconds)
    {
        flash?.Kill();

        var team = TeamColors.Of(playerTeam.Team);
        flash = DOVirtual.Color(color, team, Mathf.Max(0.01f, seconds),
                                Tint)
                         .SetLink(gameObject)
                         .OnKill(() => Apply(playerTeam.Team));
    }

    void SetCharacter(int index)
    {
        if (visuals == null || modelRoot == null) return;
        if (model != null) { model.SetActive(false); Destroy(model); }
        model = CharacterCatalog.IsValid(index)
            ? visuals.SpawnModel(CharacterCatalog.All[index].Id, modelRoot, gameObject.layer) : null;
        appearance = model != null ? model.GetComponent<CharacterModel>() : null;
        Apply(playerTeam.Team);
    }

    void Apply(int team) => Tint(TeamColors.Of(team));

    void Tint(Color color)
    {
        if (appearance != null) appearance.Tint(color, tintStrength);
    }
}
