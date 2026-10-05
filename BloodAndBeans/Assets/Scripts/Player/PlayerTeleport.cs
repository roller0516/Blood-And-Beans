using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// 서버가 플레이어를 순간이동시키는 단 하나의 경로.
///
/// `transform.position` 대입만으로는 두 곳에서 되돌아온다. CharacterController는 자기
/// 내부 위치를 다시 써 넣고, NetworkTransform은 서버 권위 + 보간이라 클라이언트 화면에서
/// 목적지까지 미끄러져 온다. 귀환 페널티와 페이즈 시작 배치가 같은 처리를 쓰도록 모았다.
public static class PlayerTeleport
{
    /// 서버에서만 호출한다. 스케일은 유지하고, 회전은 `rotation`이 없으면 유지한다.
    public static void ToServer(GameObject player, Vector3 destination, bool notify = true, Quaternion? rotation = null)
    {
        if (player == null) return;

        var networkObject = player.GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned && !networkObject.NetworkManager.IsServer) return;

        if (notify && player.transform.position != destination)
            player.GetComponent<PlayerTeam>()?.NotifyTeleportServer(
                player.transform.position, destination, player.transform.rotation);

        var controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        var networkTransform = player.GetComponent<NetworkTransform>();
        var facing = rotation ?? player.transform.rotation;
        if (networkTransform != null)
            networkTransform.Teleport(destination, facing, player.transform.localScale);
        else
            player.transform.SetPositionAndRotation(destination, facing);

        if (controller != null) controller.enabled = true;

        // 접지 높이도 목적지에 맞춘다. 목적지 y는 캡슐 바닥이 지면에 닿는 높이라
        // (`MatchDirector.spawnHeight`) 그대로 넘긴다. 빠뜨리면 `PinToGround`가 옛
        // 높이로 도로 끌어내려 캡슐이 지면에 박히고 걷지 못한다.
        var move = player.GetComponent<PlayerController>();
        if (move != null) move.RebaseGroundServer(destination.y);
    }
}
