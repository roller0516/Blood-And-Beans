using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
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
/// **프리팹 참조는 어드레서블이다.** 표 자체(`EffectManager.prefab`)는 예전처럼 앱 시작과
/// 함께 서지만, 각 연출의 파티클 프리팹은 실제로 필요해질 때만 불러온다 — 공통 연출
/// (대시 히트·상호작용 성공)은 매치 준비 때, 캐릭터 전용 연출(활공·불붙이기·메아리·
/// 도깨비불)은 그 능력을 가진 참가자가 있을 때만. 예전에는 이 표 하나가 6종 파티클을
/// 전부 직접 참조하고 있어서, 표가 로드되는 순간(앱 시작) 아무도 안 쓰는 캐릭터 전용
/// 연출까지 전부 같이 로드됐다.
///
/// **씬에 놓지 않는다. 어드레서블에서 스스로 불러와 선다** (`GameManager`와 같은 이유 —
/// 어느 씬에서 시작하든 서 있어야 하는데, 씬마다 놓으면 씬을 늘릴 때마다 빠뜨릴 수 있다).
public class EffectManager : MonoBehaviour
{
    /// 어드레서블 주소. 프리팹 이름·타입 이름과 같게 맞춘다.
    const string Address = nameof(EffectManager);

    [System.Serializable]
    struct Entry
    {
        public EffectId id;
        public AssetReference prefab;

        /// 붙는 자리 보정. 연출마다 다르고(불씨는 설비 위, 메아리는 발치) **연출의 성질이라
        /// 표가 들고 있는다.** 부르는 쪽이 들면 같은 연출이 호출부마다 다른 높이에 뜬다.
        public Vector3 offset;

        /// 전원이 쓰는 공통 연출인가(대시 히트·상호작용 성공). 참이면 매치 준비 때 곧바로
        /// 불러온다. 아니면 그 연출을 쓰는 능력의 캐릭터가 참가했을 때만 불러온다
        /// (`NoteCharacterAsync`).
        public bool common;
    }

    [SerializeField] Entry[] effects = System.Array.Empty<Entry>();

    /// 풀에 들어가 쉬는 인스턴스가 붙어 있을 자리. 비워 두면 이 오브젝트 밑에 둔다.
    [SerializeField] Transform pooledRoot;

    /// 풀 하나가 들고 있을 최대 개수. 넘치면 파괴한다.
    [SerializeField] int maxPerEffect = 32;

    /// 스폰된 플레이어 프리팹은 씬 오브젝트를 직렬화로 잡을 수 없다. `MatchDirector.Instance`와
    /// 같은 이유로 static 하나를 둔다 — 편의가 아니라 수명이 다른 두 오브젝트를 잇는 자리다.
    public static EffectManager Instance { get; private set; }

    /// 표 원본. id로 찾는다 — 로드 여부와 무관하게 항상 채워져 있다.
    readonly Dictionary<EffectId, Entry> table = new();

    readonly Dictionary<EffectId, ObjectPool<ParticleSystem>> pools = new();
    readonly Dictionary<EffectId, Vector3> baseScales = new();

    /// 지금 불러오는 중인 연출. 같은 연출을 여러 참가자가 동시에 필요로 해도 한 번만 불러온다.
    readonly Dictionary<EffectId, UniTask> loading = new();

    /// 이번 매치에서 새로 불러온(공통이 아닌) 연출. 매치가 끝나면 이 몫만 놓는다 —
    /// 공통 연출은 앱이 사는 동안 계속 쓰이므로 매치가 바뀌어도 놓지 않는다.
    readonly HashSet<EffectId> matchOwned = new();

    /// 인스턴스별 재생 세대. 풀에서 다시 꺼낼 때마다 올린다. `StopAfterAsync`는 자기가
    /// 걸릴 때의 세대를 들고 있다가, 시간이 다 됐을 때 세대가 그대로인 경우에만 끊는다 —
    /// 이 표가 없으면 조기 반환 뒤 같은 인스턴스가 재생을 다시 시작해도 옛 타이머가
    /// 새 재생을 멈춰 버린다. `this.GetCancellationTokenOnDestroy()`는 매니저가
    /// 파괴될 때만 취소되므로(사실상 앱 종료 시점) 인스턴스 재사용까지는 막지 못한다.
    readonly Dictionary<ParticleSystem, int> generation = new();

    /// 어떤 씬 오브젝트의 `Awake`보다 먼저 돈다. 불러오기는 비동기라 첫 연출보다 늦을 수는
    /// 있지만, 연출은 한 판이 시작된 뒤에야 터진다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateIfMissing() => CreateAsync().Forget();

    /// 표 프리팹 자체의 핸들은 놓지 않는다. 앱이 끝날 때까지 사는 오브젝트라 놓을 시점이 없다.
    /// 표 안의 개별 연출 프리팹은 별개로 관리한다(`loading`/`matchOwned`).
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
            if (entry.id == EffectId.None) continue;
            if (table.ContainsKey(entry.id))
            {
                CDebug.LogError($"{name}: {entry.id} 연출이 표에 두 번 있다. 뒤엣것은 무시한다.", this);
                continue;
            }
            table[entry.id] = entry;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        foreach (var pool in pools.Values) pool.Clear();
        pools.Clear();
    }

    /// 전원이 쓰는 공통 연출(대시 히트·쏟김·상호작용 성공)을 불러온다. 매치 준비 단계에서
    /// 한 번 기다린다 — 첫 대시가 터지는 순간에야 불러오면 그 한 번만 늦게 보인다.
    public static UniTask PrepareCommonAsync(System.Threading.CancellationToken ct = default)
    {
        var manager = Instance;
        if (manager == null) return UniTask.CompletedTask;

        var loads = new List<UniTask>();
        foreach (var entry in manager.table.Values)
            if (entry.common) loads.Add(manager.EnsureLoadedAsync(entry.id, ct));
        return UniTask.WhenAll(loads);
    }

    /// 이 캐릭터가 이번 매치에 있다. 그 캐릭터의 낮·밤 액티브가 쓰는 연출만 골라 불러온다.
    /// 같은 캐릭터를 여러 참가자가 골랐어도, 이미 불러왔거나 불러오는 중인 연출은
    /// 다시 요청하지 않는다(`EnsureLoadedAsync`).
    public static UniTask NoteCharacterAsync(CharacterId characterId, System.Threading.CancellationToken ct = default)
    {
        var manager = Instance;
        if (manager == null) return UniTask.CompletedTask;

        var loads = new List<UniTask>();
        foreach (var id in CharacterEffectIds(characterId))
        {
            if (!manager.table.TryGetValue(id, out var entry) || entry.common) continue;
            loads.Add(manager.EnsureLoadedAsync(id, ct));
        }
        return UniTask.WhenAll(loads);
    }

    /// 이 캐릭터의 낮 액티브·밤 액티브가 그리는 연출 id. `AbilityFactory`가 스킬 종류를
    /// 능력 객체로 바꾸는 것과 같은 표를 본다 — 능력이 늘면 여기도 한 줄 늘어난다.
    static IEnumerable<EffectId> CharacterEffectIds(CharacterId characterId)
    {
        CharacterDef def = default;
        var found = false;
        foreach (var d in CharacterCatalog.All)
        {
            if (d.Id != characterId) continue;
            def = d;
            found = true;
            break;
        }
        if (!found) yield break;

        switch (def.Night)
        {
            case NightSkill.WillOWisp: yield return EffectId.Wisp; break;
            case NightSkill.Echo: yield return EffectId.Echo; break;
        }
        switch (def.Day)
        {
            case DaySkill.Ignite: yield return EffectId.Ignite; break;
            case DaySkill.Glide: yield return EffectId.Glide; break;
        }
    }

    /// 매치가 끝났다. 이번 매치에서 새로 불러온(공통이 아닌) 연출의 풀과 로드 몫을 놓는다.
    /// 공통 연출은 다음 매치도 쓰므로 놓지 않는다.
    public static void EndMatch()
    {
        var manager = Instance;
        if (manager == null) return;

        foreach (var id in manager.matchOwned)
            manager.Unload(id);
        manager.matchOwned.Clear();
    }

    /// 이 연출을 지금 재생할 수 있는 상태로 만든다. 이미 됐으면 곧바로 끝나고, 불러오는
    /// 중이면 그 로드에 합류한다 — 같은 연출을 여러 참가자가 동시에 필요로 해도 한 번만 뜬다.
    UniTask EnsureLoadedAsync(EffectId id, System.Threading.CancellationToken ct)
    {
        if (pools.ContainsKey(id)) return UniTask.CompletedTask;
        if (loading.TryGetValue(id, out var inFlight)) return inFlight;

        // 합류자가 같은 로드를 함께 기다린다. UniTask는 대기자를 하나만 받아(Preserve도 대기 중엔 못 막는다)
        // 두 번째 대기가 예외를 던지므로, 대기자를 여럿 받는 완료 소스를 나눠 준다.
        var done = new UniTaskCompletionSource();
        loading[id] = done.Task;
        LoadAndPoolAsync(id, ct, done).Forget();
        return done.Task;
    }

    async UniTaskVoid LoadAndPoolAsync(EffectId id, System.Threading.CancellationToken ct, UniTaskCompletionSource done)
    {
        try
        {
            if (!table.TryGetValue(id, out var entry))
            {
                CDebug.LogError($"{name}: {id} 연출이 표에 없다. 불러올 것이 없다.", this);
                return;
            }
            if (entry.prefab == null || !entry.prefab.RuntimeKeyIsValid())
            {
                CDebug.LogError($"{name}: {id} 연출의 프리팹 참조가 비어 있다.", this);
                return;
            }

            GameObject prefab;
            try
            {
                prefab = await ResourceManager.Instance.LoadAsync<GameObject>(entry.prefab, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                CDebug.LogError($"{name}: {id} 연출을 불러오지 못했다. {e.Message}", this);
                return;
            }

            var prefabPs = prefab.GetComponent<ParticleSystem>();
            if (prefabPs == null)
            {
                CDebug.LogError($"{name}: {id} 연출 프리팹에 ParticleSystem이 없다.", this);
                ResourceManager.Instance.Release<GameObject>(entry.prefab);
                return;
            }

            baseScales[id] = prefabPs.transform.localScale;
            pools[id] = new ObjectPool<ParticleSystem>(
                createFunc: () => Create(id, prefabPs),
                actionOnGet: ps => ps.gameObject.SetActive(true),
                actionOnRelease: Park,
                actionOnDestroy: ps => { if (ps != null) Destroy(ps.gameObject); },
                collectionCheck: true, defaultCapacity: 4, maxSize: Mathf.Max(1, maxPerEffect));

            if (!entry.common) matchOwned.Add(id);
        }
        finally
        {
            loading.Remove(id);
            done.TrySetResult();
        }
    }

    /// 불러온 연출을 완전히 내린다. 풀에 쉬던 인스턴스를 지우고 로드 몫을 놓는다.
    void Unload(EffectId id)
    {
        if (pools.TryGetValue(id, out var pool))
        {
            pool.Clear();
            pools.Remove(id);
        }
        baseScales.Remove(id);

        if (table.TryGetValue(id, out var entry) && entry.prefab != null && entry.prefab.RuntimeKeyIsValid())
            ResourceManager.Instance.Release<GameObject>(entry.prefab);
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

    /// 한 번 터지는 연출. 끝나면 스스로 돌아온다. 아직 준비되지 않았으면(불러오는 중이거나
    /// 요청된 적이 없으면) 그냥 넘어가지 않고 알린다 — 조용히 무시하면 연출이 빠진 원인을
    /// 찾을 수 없다.
    public static void Play(EffectId id, Vector3 position, float scale = 1f)
    {
        var manager = Instance;
        if (manager == null) return;

        var effect = manager.Take(id, scale);
        if (effect == null) return;

        effect.transform.SetPositionAndRotation(
            position + manager.table[id].offset, Quaternion.identity);
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
        effect.transform.localPosition = manager.table[id].offset;
        effect.transform.localRotation = Quaternion.identity;
        effect.Play(true);

        if (seconds > 0f) manager.StopAfterAsync(effect, seconds, manager.generation[effect]).Forget();
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
        if (!table.ContainsKey(id))
        {
            CDebug.LogWarning($"{name}: {id} 연출이 표에 없다. 아무것도 그리지 않는다.", this);
            return null;
        }
        if (!pools.TryGetValue(id, out var pool))
        {
            CDebug.LogWarning($"{name}: {id} 연출이 아직 준비되지 않았다"
                + (loading.ContainsKey(id) ? " (불러오는 중)." : " (준비를 요청한 적이 없다).")
                + " 아무것도 그리지 않는다.", this);
            return null;
        }

        var effect = pool.Get();
        effect.transform.localScale = baseScales[id] * Mathf.Max(0.0001f, scale);
        generation[effect] = generation.GetValueOrDefault(effect) + 1;
        return effect;
    }

    /// `myGeneration`은 이 타이머를 건 시점의 세대다. 시간이 다 됐을 때 세대가 이미
    /// 올라가 있으면(같은 인스턴스가 그새 반환됐다가 다시 걸렸으면) 끊지 않는다.
    async UniTaskVoid StopAfterAsync(ParticleSystem effect, float seconds, int myGeneration)
    {
        var cancelled = await UniTask.Delay(System.TimeSpan.FromSeconds(seconds),
            cancellationToken: this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();
        if (!cancelled && generation.GetValueOrDefault(effect) == myGeneration) Stop(effect);
    }

    /// 풀로 돌려보낸다. `PooledEffect`가 부른다.
    internal void Release(EffectId id, ParticleSystem effect)
    {
        if (effect == null) return;
        if (pools.TryGetValue(id, out var pool)) pool.Release(effect);
        else Park(effect);
    }
}
