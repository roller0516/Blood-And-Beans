using UnityEngine;

/// 손에 든 것을 팀 밖에서 숨긴다 (기획서 5.4.1: 다른 팀에는 3단계로만 보인다).
/// 카페는 모두에게 보이므로 이 레이어로 옮기지 않는다 (`Cafe.OnNetworkSpawn`).
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

    /// 오브젝트 하나를 통째로 그 팀의 레이어로 옮긴다. 플레이어의 손 앵커가 쓴다
    /// (`ItemDisplay`, `PlayerVisuals`) — 손에 든 것은 숨기되 플레이어 본체는 Default에 남아야 한다.
    public static void ApplyTeamLayer(GameObject root, int team)
    {
        var layer = LayerMask.NameToLayer(LayerPrefix + team);
        if (root == null || layer < 0) return;       // 레이어가 없으면 컬링도 없다

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }
}
