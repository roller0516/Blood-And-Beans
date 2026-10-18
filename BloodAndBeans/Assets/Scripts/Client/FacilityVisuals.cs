using UnityEngine;

/// 공용 설비의 점유 상태를 김·불·물거품과 모델 애니메이션으로 그린다. 완료는 서버의 세척 사건만 쓴다.
[RequireComponent(typeof(SharedFacility))]
public sealed class FacilityVisuals : MonoBehaviour
{
    [SerializeField] SharedFacility facility;
    [SerializeField] Transform outlet;
    [SerializeField] EffectId workingEffect;
    [SerializeField] EffectId completedEffect;

    /// 설비 모델. 움직이는 부품이 없는 설비는 비워 둔다.
    [SerializeField] FacilityModel model;

    /// 쓰는 동안 점유 팀 색으로 물드는 렌더러. 물드는 범위는 머티리얼의 팀 마스크가 정한다.
    [SerializeField] Renderer[] teamAreas;
    ParticleSystem working;
    MaterialPropertyBlock teamBlock;

    void OnEnable()
    {
        facility.BusyChanged += SetBusy;
        facility.Washed += OnWashed;
        if (facility.IsSpawned) SetBusy(facility.Busy);
    }
    void OnDisable()
    {
        facility.BusyChanged -= SetBusy;
        facility.Washed -= OnWashed;
        StopWorking();
    }
    void SetBusy(bool busy)
    {
        if (model != null) model.SetWorking(busy);
        PaintTeam(busy ? TeamColors.Of(facility.OccupyingTeam) : Color.white);
        if (!busy) { StopWorking(); return; }
        if (facility.IsClient && working == null && workingEffect != EffectId.None)
            working = EffectManager.PlayAttached(workingEffect, outlet, 0f);
    }
    /// 머티리얼을 진열대·조리대와 같이 쓰므로 사본 대신 블록으로 이 설비에만 칠한다.
    void PaintTeam(Color color)
    {
        teamBlock ??= new MaterialPropertyBlock();
        foreach (var area in teamAreas)
        {
            var materials = area.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null || !materials[i].HasProperty(TeamColors.TeamColorId)) continue;
                area.GetPropertyBlock(teamBlock, i);
                teamBlock.SetColor(TeamColors.TeamColorId, color);
                area.SetPropertyBlock(teamBlock, i);
            }
        }
    }
    void OnWashed()
    {
        StopWorking();
        if (facility.IsClient && completedEffect != EffectId.None)
            EffectManager.Play(completedEffect, outlet.position);
    }
    void StopWorking()
    {
        if (working == null) return;
        // 설비가 사라져도 남은 입자와 풀 인스턴스는 매니저 밑에서 수명을 마친다.
        working.transform.SetParent(EffectManager.Instance != null ? EffectManager.Instance.transform : null, true);
        EffectManager.Stop(working);
        working = null;
    }
}
