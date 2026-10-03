using UnityEngine;

/// 공용 설비의 점유 상태를 김·불·물거품으로 그린다. 완료는 서버의 세척 사건만 쓴다.
[RequireComponent(typeof(SharedFacility))]
public sealed class FacilityVisuals : MonoBehaviour
{
    [SerializeField] SharedFacility facility;
    [SerializeField] Transform outlet;
    [SerializeField] EffectId workingEffect;
    [SerializeField] EffectId completedEffect;
    ParticleSystem working;

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
        if (!busy) { StopWorking(); return; }
        if (facility.IsClient && working == null && workingEffect != EffectId.None)
            working = EffectManager.PlayAttached(workingEffect, outlet, 0f);
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
