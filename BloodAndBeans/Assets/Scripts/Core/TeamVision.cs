using UnityEngine;

/// 플레이어는 자기 카페만 본다 (기획서 3.1: 상대의 재료·설비·캐릭터는 비공개이고
/// 매출만 공개다).
///
/// 카메라 컬링은 표시용 2차 방어일 뿐이다. 실제 은닉은 카페 NetworkObject를 상대 팀에
/// 복제하지 않는 것으로 이뤄진다 (`MatchDirector.ApplyTeamVisibilityServer`).
public class TeamVision : MonoBehaviour
{
    public const string LayerPrefix = "CafeTeam";

    /// 로컬 플레이어의 카메라가 자기 팀을 알게 된 뒤에 적용한다.
    public static void ApplyServer(Camera cam, int myTeam, int teamCount)
    {
        if (cam == null) return;

        var mask = cam.cullingMask;
        for (var t = 0; t < teamCount; t++)
        {
            var layer = LayerMask.NameToLayer(LayerPrefix + t);
            if (layer < 0) continue;                 // 레이어가 정의되지 않았다 — 컬링할 대상이 없다
            if (t == myTeam) mask |= 1 << layer;
            else mask &= ~(1 << layer);
        }
        cam.cullingMask = mask;
    }

    /// 카페 하나를 통째로 그 팀의 레이어로 옮긴다. 카페가 런타임 스폰이 되면서 레이어를
    /// 프리팹에 구워 둘 수 없게 됐다 — 프리팹 하나를 모든 팀이 공유하기 때문이다.
    public static void ApplyCafeLayer(GameObject root, int team)
    {
        var layer = LayerMask.NameToLayer(LayerPrefix + team);
        if (root == null || layer < 0) return;       // 레이어가 없으면 컬링도 없다

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }
}
