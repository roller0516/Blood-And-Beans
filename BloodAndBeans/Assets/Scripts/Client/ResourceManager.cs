using UnityEngine;

/// 리소스의 단일 창구. 원본은 `Resources/ResourceManager` 애셋 하나다.
/// 역할별로 파셜을 나눈다 — Addressables 로드·해제는 `ResourceManager.Addressables.cs`,
/// UI 스프라이트 조회는 `ResourceManager.Sprites.cs`.
[CreateAssetMenu(menuName = "Blood & Beans/리소스 매니저", fileName = AssetName)]
public partial class ResourceManager : ScriptableObject
{
    public const string AssetName = nameof(ResourceManager);

    static ResourceManager instance;

    public static ResourceManager Instance
    {
        get
        {
            if (instance != null) return instance;
            instance = Resources.Load<ResourceManager>(AssetName);
            if (instance == null) CDebug.LogError($"Resources/{AssetName} 애셋이 없다. 리소스를 불러오지 못한다.");
            return instance;
        }
    }

#if UNITY_EDITOR
    // 애셋은 재생이 끝나도 메모리에 남는다. 재생 중 만든 핸들과 복제 스프라이트는 그때 무효가 되므로,
    // 에디터 도구가 죽은 표를 읽지 않게 에디터로 돌아올 때 비운다.
    void OnEnable() => UnityEditor.EditorApplication.playModeStateChanged += ForgetPlayModeState;
    void OnDisable() => UnityEditor.EditorApplication.playModeStateChanged -= ForgetPlayModeState;

    void ForgetPlayModeState(UnityEditor.PlayModeStateChange change)
    {
        if (change != UnityEditor.PlayModeStateChange.EnteredEditMode) return;
        sprites = new();
        ingredients = new();
        crews = new();
        menus = new();
        loaded = null;
    }
#endif
}
