using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;

/// 연출 프리팹을 **한 표에 모아 두고 풀에서 꺼내 쓴다.**
///
/// 예전에는 연출이 캐릭터별 컴포넌트에 흩어져 있었다(`BatSkillVisuals`·`DokkaebiSkillVisuals`).
/// 그래서 ① 같은 연출을 두 캐릭터가 쓰면 프리팹을 두 번 꽂아야 했고 ② 캐릭터를 늘릴 때마다
/// 플레이어 프리팹의 배선이 늘었고 ③ 터질 때마다 `Instantiate`하고 끝나면 `Destroy`했다.
/// 표 하나로 모으면 셋이 같이 사라진다 — **무엇을 그릴지는 `EffectId`가, 어떻게 생겼는지는
/// 이 표가** 정한다.
///
/// 풀은 `UnityEngine.Pool.ObjectPool`이다. 직접 만들지 않는다 (AGENTS.md: 커스텀 풀은
/// 프로파일링 근거가 있을 때만).
///
/// **씬에 놓지 않는다. 어드레서블에서 스스로 불러와 선다** (`GameManager`와 같은 이유 —
/// 어느 씬에서 시작하든 서 있어야 하는데, 씬마다 놓으면 씬을 늘릴 때마다 빠뜨릴 수 있다).
/// Resources가 아니라 Addressables인 것은 이 프리팹이 파티클 전부를 물고 있어서다 —
/// Resources에 두면 쓰든 안 쓰든 빌드에 통째로 들어간다.
public class EffectManager : MonoBehaviour
{
    /// 어드레서블 주소. 프리팹 이름·타입 이름과 같게 맞춘다.
    const string Address = nameof(EffectManager);

    [System.Serializable]
    struct Entry
    {
        public EffectId id;
        public ParticleSystem prefab;

        /// 붙는 자리 보정. 연출마다 다르고(불씨는 설비 위, 메아리는 발치) **연출의 성질이라
        /// 표가 들고 있는다.** 부르는 쪽이 들면 같은 연출이 호출부마다 다른 높이에 뜬다.
        public Vector3 offset;
    }

    [SerializeField] Entry[] effects = System.Array.Empty<Entry>();

    /// 풀에 들어가 쉬는 인스턴스가 붙어 있을 자리. 비워 두면 이 오브젝트 밑에 둔다.
    [SerializeField] Transform pooledRoot;

    /// 풀 하나가 들고 있을 최대 개수. 넘치면 파괴한다.
    [SerializeField] int maxPerEffect = 32;

    /// 스폰된 플레이어 프리팹은 씬 오브젝트를 직렬화로 잡을 수 없다. `MatchDirector.Instance`와
    /// 같은 이유로 static 하나를 둔다 — 편의가 아니라 수명이 다른 두 오브젝트를 잇는 자리다.
    public static EffectManager Instance { get; private set; }

    readonly Dictionary<EffectId, ObjectPool<ParticleSystem>> pools = new();
    readonly Dictionary<EffectId, Vector3> baseScales = new();
    readonly Dictionary<EffectId, Vector3> offsets = new();

    /// 어떤 씬 오브젝트의 `Awake`보다 먼저 돈다. 불러오기는 비동기라 첫 연출보다 늦을 수는
    /// 있지만, 연출은 한 판이 시작된 뒤에야 터진다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateIfMissing() => CreateAsync().Forget();

    /// 프리팹 핸들은 놓지 않는다. 앱이 끝날 때까지 사는 오브젝트라 놓을 시점이 없다.
    static async UniTaskVoid CreateAsync()
    {
        var prefab = await ResourceManager.Instance.LoadAsync<GameObject>(Address);

        // 씬에 손으로 놓아 둔 것이 있으면 두 번 만들지 않는다.
        if (Instance != null) return;

        // "(Clone)"을 떼어 로그에서 프리팹과 같은 이름으로 보이게 한다.
        Instantiate(prefab).name = Address;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            CDebug.LogWarning($"{name}: 연출 표가 이미 서 있다. 이쪽을 버린다.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (pooledRoot == null) pooledRoot = transform;

        foreach (var entry in effects)
        {
            if (entry.prefab == null || entry.id == EffectId.None) continue;
            if (pools.ContainsKey(entry.id))
            {
                CDebug.LogError($"{name}: {entry.id} 연출이 표에 두 번 있다. 뒤엣것은 무시한다.", this);
                continue;
            }

            var id = entry.id;
            var prefab = entry.prefab;
            baseScales[id] = prefab.transform.localScale;
            offsets[id] = entry.offset;
            pools[id] = new ObjectPool<ParticleSystem>(
                createFunc: () => Create(id, prefab),
                actionOnGet: ps => ps.gameObject.SetActive(true),
                actionOnRelease: Park,
                actionOnDestroy: ps => { if (ps != null) Destroy(ps.gameObject); },
                collectionCheck: true, defaultCapacity: 4, maxSize: Mathf.Max(1, maxPerEffect));
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        foreach (var pool in pools.Values) pool.Clear();
        pools.Clear();
    }

    ParticleSystem Create(EffectId id, ParticleSystem prefab)
    {
        var made = Instantiate(prefab, pooledRoot);

        // 다 터지면 스스로 풀로 돌아온다. 이 콜백이 없으면 회수 시점을 부르는 쪽이
        // 알아야 하고, 그러면 파티클 수명이 코드에 박힌다.
        var main = made.main;
        main.stopAction = ParticleSystemStopAction.Callback;

        made.gameObject.AddComponent<PooledEffect>().Bind(this, id);
        made.gameObject.SetActive(false);
        return made;
    }

    void Park(ParticleSystem ps)
    {
        if (ps == null) return;
        ps.transform.SetParent(pooledRoot, false);
        ps.gameObject.SetActive(false);
    }

    /// 한 번 터지는 연출. 끝나면 스스로 돌아온다.
    public static void Play(EffectId id, Vector3 position, float scale = 1f)
    {
        var manager = Instance;
        if (manager == null) return;

        var effect = manager.Take(id, scale);
        if (effect == null) return;

        effect.transform.SetPositionAndRotation(position + manager.offsets[id], Quaternion.identity);
        effect.Play(true);
    }

    /// 대상에 붙어 지속되는 연출. `seconds`가 지나면 방출을 멈추고, 남은 입자가 수명을
    /// 다하면 돌아온다. 같은 자리에 다시 걸면 앞엣것을 먼저 끊는 것은 부르는 쪽 몫이다.
    public static ParticleSystem PlayAttached(EffectId id, Transform parent, float seconds, float scale = 1f)
    {
        var manager = Instance;
        if (manager == null || parent == null) return null;

        var effect = manager.Take(id, scale);
        if (effect == null) return null;

        effect.transform.SetParent(parent, false);
        effect.transform.localPosition = manager.offsets[id];
        effect.transform.localRotation = Quaternion.identity;
        effect.Play(true);

        if (seconds > 0f) manager.StopAfterAsync(effect, seconds).Forget();
        return effect;
    }

    /// 지속 연출을 지금 끊는다. 이미 돌아간 것에 불러도 안전하다.
    public static void Stop(ParticleSystem effect)
    {
        if (effect == null || !effect.gameObject.activeSelf) return;

        // 이미 생긴 입자는 수명 끝까지 흘려보낸다. 잘라내면 활공이 뚝 끊긴 것처럼 보인다.
        effect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    ParticleSystem Take(EffectId id, float scale)
    {
        if (!pools.TryGetValue(id, out var pool))
        {
            CDebug.LogWarning($"{name}: {id} 연출이 표에 없다. 아무것도 그리지 않는다.", this);
            return null;
        }

        var effect = pool.Get();
        effect.transform.localScale = baseScales[id] * Mathf.Max(0.0001f, scale);
        return effect;
    }

    async UniTaskVoid StopAfterAsync(ParticleSystem effect, float seconds)
    {
        var cancelled = await UniTask.Delay(System.TimeSpan.FromSeconds(seconds),
            cancellationToken: this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();
        if (!cancelled) Stop(effect);
    }

    /// 풀로 돌려보낸다. `PooledEffect`가 부른다.
    internal void Release(EffectId id, ParticleSystem effect)
    {
        if (effect == null) return;
        if (pools.TryGetValue(id, out var pool)) pool.Release(effect);
        else Park(effect);
    }
}
