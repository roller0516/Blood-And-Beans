using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// 반복 스폰되는 네트워크 프리팹(상자·더미·손님·가방·관)을 어드레서블 참조로 들고, 불러와 `NetworkPool`에 건다.
/// `EffectManager`와 같은 이유로 씬에 놓지 않고 어드레서블에서 스스로 선다.
///
/// 프리팹은 NGO 네트워크 프리팹 목록에도 그대로 남아 있다 (`ForceSamePrefabs` — 목록은 접속 전에 확정돼야 한다).
public class PoolManager : MonoBehaviour
{
    /// 어드레서블 주소. 프리팹 이름·타입 이름과 같게 맞춘다.
    const string Address = nameof(PoolManager);

    [SerializeField] AssetReferenceGameObject[] prefabs = System.Array.Empty<AssetReferenceGameObject>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateIfMissing() => CreateAsync().Forget();

    /// 표 프리팹과 등록한 프리팹의 핸들은 놓지 않는다. 앱이 끝날 때까지 사는 오브젝트다.
    static async UniTaskVoid CreateAsync()
    {
        var prefab = await ResourceManager.Instance.LoadAsync<GameObject>(Address);
        Instantiate(prefab).name = Address;
    }

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        RegisterAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    async UniTaskVoid RegisterAsync(CancellationToken ct)
    {
        // NetworkManager는 GameManager가 BeforeSceneLoad에 동기로 세운다.
        var manager = NetworkManager.Singleton;
        if (manager == null)
        {
            CDebug.LogError($"{name}: NetworkManager가 없다. 네트워크 프리팹을 풀링하지 못한다.", this);
            return;
        }

        // 등록 전에 스폰된 인스턴스도 despawn 때는 풀로 돌아온다. 늦어도 깨지지 않는다.
        foreach (var reference in prefabs)
        {
            var prefab = await ResourceManager.Instance.LoadAsync<GameObject>(reference, ct);
            NetworkPool.Register(manager, prefab, transform);
        }
    }
}
