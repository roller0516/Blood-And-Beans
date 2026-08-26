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
    /// 서버에서만 호출한다. 회전과 스케일은 유지한다.
    public static void ToServer(GameObject player, Vector3 destination)
    {
        if (player == null) return;

        var controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        var networkTransform = player.GetComponent<NetworkTransform>();
        if (networkTransform != null)
            networkTransform.Teleport(destination, player.transform.rotation, player.transform.localScale);
        else
            player.transform.position = destination;

        if (controller != null) controller.enabled = true;
    }
}
