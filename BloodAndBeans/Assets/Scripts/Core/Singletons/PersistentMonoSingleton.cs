using UnityEngine;

/// <summary>
/// UnityCommunity/UnitySingleton 기반의 전역 영구 유지(DontDestroyOnLoad) MonoBehaviour 싱글톤.
/// 게임 내내 한 번만 생성되며 씬 전환 시에도 파괴되지 않습니다.
/// </summary>
public abstract class PersistentMonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T instance;
    private static bool isQuitting;

    public static T Instance
    {
        get
        {
            if (isQuitting) return null;

            if (instance == null)
            {
                // 없으면 만들지 않는다. 만들어 주면 "아직 그 씬이 아니다"라는 정상 상태와
                // "씬에 놓는 걸 잊었다"라는 결함이 똑같이 빈 오브젝트로 둔갑한다.
                // 실제로 그래서 매치 씬의 진짜 오브젝트가 파괴됐다 — 로비에서 먼저 호출된
                // Instance가 가짜를 만들어 자리를 차지했고, 나중에 로드된 진짜가 중복으로
                // 몰려 Destroy됐다.
                instance = FindAnyObjectByType<T>();
            }
            return instance;
        }
    }

    protected virtual void Awake()
    {
        if (instance == null)
        {
            instance = this as T;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Debug.LogWarning($"[{typeof(T).Name}] PersistentMonoSingleton은 최상위(Root) 오브젝트여야 DontDestroyOnLoad가 정상 동작합니다.");
            }
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    protected virtual void OnApplicationQuit()
    {
        isQuitting = true;
    }
}
