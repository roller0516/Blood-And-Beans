using TMPro;
using UnityEngine;

/// 플레이어 머리 위 이름표 — 팀 색 배경 + 닉네임 (기획서 5.7.6-a). 자기 캐릭터에도 띄우고 전원에게 보인다.
/// 월드 공간 캔버스라 원근대로 작아지고 물체 뒤에서 가려진다. 카메라를 향해 돌리고,
/// 멀어져 최소 크기 아래로 내려가면 그만큼 키워 되돌린다.
public sealed class UIPlayerNameplate : MonoBehaviour
{
    [SerializeField] PlayerTeam team;
    [SerializeField] Canvas canvas;
    /// 팀 색이 본체다. 멀리서는 이 색 덩어리만 읽힌다.
    [SerializeField] UnityEngine.UI.Graphic background;
    [SerializeField] TMP_Text nameLabel;

    [Tooltip("닉네임이 없을 때(로비를 거치지 않은 개발용 시작) 쓰는 이름. {0}은 클라이언트 번호다.")]
    [SerializeField] string fallbackName = "P{0}";

    [Tooltip("내 캐릭터의 이름표에는 닉네임 대신 이 글자를 쓴다.")]
    [SerializeField] string selfName = "YOU";

    [Header("크기")]
    [Tooltip("캔버스 1픽셀이 차지하는 월드 길이.")]
    [SerializeField, Min(0.0001f)] float unitsPerPixel = 0.006f;
    [Tooltip("이 카메라 깊이에서 원래 크기(배율 1)로 보인다.")]
    [SerializeField, Min(0.01f)] float referenceDepth = 5f;
    [Tooltip("멀어져도 이 배율 아래로는 줄지 않는다 (기획서 5.7.6-a).")]
    [SerializeField, Range(0.05f, 1f)] float minScreenScale = 0.35f;

    Camera cameraView;

    void Awake() => canvas.enabled = false;

    void OnEnable()
    {
        team.TeamChanged += OnTeamChanged;
        team.NicknameChanged += OnNicknameChanged;
        Render();
    }

    void OnDisable()
    {
        team.TeamChanged -= OnTeamChanged;
        team.NicknameChanged -= OnNicknameChanged;
    }

    void OnTeamChanged(int _) => Render();
    void OnNicknameChanged(string _) => Render();

    void Render()
    {
        background.color = TeamColors.Of(team.Team);
        // 소유자 여부는 스폰 뒤에야 맞다. 스폰 때 오는 TeamChanged가 다시 그린다.
        var nickname = team.Nickname;
        nameLabel.text = team.IsOwner ? selfName
            : string.IsNullOrEmpty(nickname) ? string.Format(fallbackName, team.OwnerClientId) : nickname;
    }

    void LateUpdate()
    {
        if (cameraView == null) cameraView = Camera.main;

        var visible = team.IsSpawned && team.Team >= 0 && cameraView != null && !InFog();
        if (canvas.enabled != visible) canvas.enabled = visible;
        if (!visible) return;

        var view = cameraView.transform;
        transform.rotation = view.rotation;

        // 화면에서의 배율은 referenceDepth / depth다. 최소 배율 밑으로 내려가는 만큼만 키운다.
        var depth = Vector3.Dot(transform.position - view.position, view.forward);
        var scale = unitsPerPixel * Mathf.Max(1f, minScreenScale * depth / referenceDepth);
        if (!Mathf.Approximately(transform.localScale.x, scale)) transform.localScale = Vector3.one * scale;
    }

    /// 밤에는 안개 안에서 가려진다 (기획서 6.1). 안개는 불투명만 덮으므로 투명한 UI는 직접 끈다.
    bool InFog()
    {
        var director = MatchDirector.Instance;
        return director != null && director.Phase.Current == Phase.Night && !FogOfWar.IsRevealedShared(transform.position);
    }
}
