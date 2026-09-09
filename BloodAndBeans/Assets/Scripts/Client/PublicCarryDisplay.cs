using UnityEngine;

/// 상대 손은 내용물 없이 소지·오염·블러드 빈 빛만 표시한다 (기획서 5.4.1).
public sealed class PublicCarryDisplay : MonoBehaviour
{
    [SerializeField] PlayerCarry carry;
    [SerializeField] PlayerTeam team;
    [SerializeField] Renderer marker;
    [SerializeField] Color heldColor = Color.white;
    [SerializeField] Color dirtyColor = new(0.35f, 0.2f, 0.1f);
    [SerializeField] Color bloodColor = Color.red;
    readonly MaterialPropertyBlock properties = new();
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    void OnEnable()
    {
        carry.ContentsChanged += Refresh;
        team.TeamChanged += OnTeam;
        Refresh();
    }
    void OnDisable() { carry.ContentsChanged -= Refresh; team.TeamChanged -= OnTeam; }
    void OnTeam(int _) => Refresh();
    void Refresh()
    {
        marker.enabled = carry.IsSpawned && team.Team != PlayerTeam.Local() && carry.PublicState != 0;
        properties.SetColor(BaseColor, carry.BloodGlow ? bloodColor : carry.PublicState == 2 ? dirtyColor : heldColor);
        marker.SetPropertyBlock(properties);
    }
}
