using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// 반복 스폰되는 네트워크 프리팹의 풀. NGO `PrefabHandler`에 걸려 서버·클라이언트 양쪽의 생성과 파괴를 받는다.
/// 어드레서블은 모른다 — 프리팹을 불러와 등록하는 것은 BB.Client의 `PoolManager`다 (BB.Game은 Addressables를 참조하지 않는다).
///
/// NGO는 재사용 인스턴스의 NetworkVariable·필드를 되돌리지 않는다. 풀에 넣는 컴포넌트는 스폰마다 스스로 초기화해야 한다.
public sealed class NetworkPool : INetworkPrefabInstanceHandler
{
    // PrefabIdHash(GlobalObjectIdHash) → 풀. 등록처(NetworkManager.PrefabHandler)가 앱 수명 전역이라 같은 수명으로 둔다.
    static readonly Dictionary<uint, NetworkPool> pools = new();

    readonly GameObject prefab;
    readonly Stack<NetworkObject> idle = new();

    NetworkPool(GameObject prefab) => this.prefab = prefab;

    public static void Register(NetworkManager manager, GameObject prefab)
    {
        var hash = prefab.GetComponent<NetworkObject>().PrefabIdHash;
        if (pools.ContainsKey(hash)) return;

        var pool = new NetworkPool(prefab);
        if (manager.PrefabHandler.AddHandler(hash, pool)) pools[hash] = pool;
        else CDebug.LogError($"{nameof(NetworkPool)}: {prefab.name} 핸들러 등록에 실패했다. 풀 없이 생성·파괴한다.", prefab);
    }

    /// 서버 전용 `Instantiate` 대체. 스폰 전 인스턴스를 준다 — 값을 심고 `Spawn()`하는 것은 호출자다.
    /// 아직 등록되지 않았으면(어드레서블 로드 전) 그냥 생성한다.
    public static T Take<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
    {
        var hash = prefab.GetComponent<NetworkObject>().PrefabIdHash;
        if (!pools.TryGetValue(hash, out var pool)) return Object.Instantiate(prefab, position, rotation);
        return pool.Instantiate(NetworkManager.ServerClientId, position, rotation).GetComponent<T>();
    }

    public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
    {
        while (idle.Count > 0)
        {
            var reused = idle.Pop();
            if (reused == null) continue;   // 씬 언로드 등으로 Unity가 이미 파괴했다

            // 새로 만든 것과 같은 자리(활성 씬)에 둔다. 위치는 켜기 전에 잡아야 콜라이더 경계가 맞는다.
            var go = reused.gameObject;
            SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
            go.transform.SetPositionAndRotation(position, rotation);
            go.SetActive(true);
            return reused;
        }

        return Object.Instantiate(prefab, position, rotation).GetComponent<NetworkObject>();
    }

    /// 쉬는 인스턴스는 씬 언로드에 휩쓸리지 않게 DontDestroyOnLoad로 뺀다.
    /// 부모로 넣지 않는 이유: despawn된 NetworkObject는 부모 변경을 거부한다 (NetworkObject.OnTransformParentChanged).
    public void Destroy(NetworkObject networkObject)
    {
        var go = networkObject.gameObject;
        go.SetActive(false);
        Object.DontDestroyOnLoad(go);
        idle.Push(networkObject);
    }
}
