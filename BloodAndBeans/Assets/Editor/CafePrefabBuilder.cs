using System.Linq;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// 씬에 손으로 배치해 둔 카페를 런타임 스폰용 프리팹으로 굽는 일회성 도구.
///
/// 손으로 하면 빠뜨리기 쉬운 단계가 하나 있다. 카페 아래 설비 9개가 각자
/// NetworkObject를 들고 있는데, NGO 2.13은 동적으로 스폰한 프리팹의 자식
/// NetworkObject를 복제하지 않는다 (NetworkSpawnManager.cs의 경고 참조). 그래서
/// 루트에 NetworkObject 하나를 두고 자식 것들은 전부 떼어 낸다. 설비 스크립트는
/// NetworkBehaviour 그대로 루트의 NetworkObject에 붙는다.
public static class CafePrefabBuilder
{
    const string SourceCafeName = "Cafe_Team0";
    const string SourceFogName = "FogPlane";
    const string CafePrefabPath = "Assets/Prefabs/Cafe.prefab";
    const string FogPrefabPath = "Assets/Prefabs/FogPlane.prefab";
    const string NetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";

    [MenuItem("Blood & Beans/카페·안개 프리팹 굽기")]
    public static void Build()
    {
        var cafePrefab = BuildCafe();
        if (cafePrefab == null) return;

        var fogPrefab = BuildFogPlane();
        RegisterNetworkPrefab(cafePrefab);
        WireDirector(cafePrefab, fogPrefab);

        AssetDatabase.SaveAssets();
        Debug.Log("[CafePrefabBuilder] 완료. 이제 씬에서 Cafe_Team0, Cafe_Team1, FogPlane을 "
                + "직접 삭제해야 한다. 남겨 두면 런타임 스폰본과 겹친다.");
    }

    static GameObject BuildCafe()
    {
        var source = FindInScene(SourceCafeName);
        if (source == null) return null;

        var copy = Object.Instantiate(source);
        copy.name = "Cafe";
        copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        // 자식 NetworkObject 제거. 루트 것은 남긴다.
        var stripped = 0;
        foreach (var child in copy.GetComponentsInChildren<NetworkObject>(true))
        {
            if (child.gameObject == copy) continue;
            Object.DestroyImmediate(child, true);
            stripped++;
        }

        if (copy.GetComponent<NetworkObject>() == null) copy.AddComponent<NetworkObject>();
        if (copy.GetComponent<Cafe>() == null) copy.AddComponent<Cafe>();

        var behaviours = copy.GetComponentsInChildren<NetworkBehaviour>(true).Length;
        EnsureFolder("Assets/Prefabs");
        var prefab = PrefabUtility.SaveAsPrefabAsset(copy, CafePrefabPath);
        Object.DestroyImmediate(copy);

        Debug.Log($"[CafePrefabBuilder] {CafePrefabPath} 생성. 자식 NetworkObject {stripped}개 제거, "
                + $"루트 NetworkObject 하나 아래 NetworkBehaviour {behaviours}개.");
        return prefab;
    }

    static GameObject BuildFogPlane()
    {
        var source = FindInScene(SourceFogName);
        if (source == null) return null;

        var copy = Object.Instantiate(source);
        copy.name = "FogPlane";

        // 안개 평면은 NetworkObject가 없는 순수 표시용이다. 네트워크에 올릴 것이 있다면
        // 구조를 잘못 읽은 것이므로 알려 준다.
        if (copy.GetComponentsInChildren<NetworkObject>(true).Length > 0)
            Debug.LogError("[CafePrefabBuilder] FogPlane 아래에 NetworkObject가 있다. "
                         + "안개 표시는 복제 대상이 아니다.");

        EnsureFolder("Assets/Prefabs");
        var prefab = PrefabUtility.SaveAsPrefabAsset(copy, FogPrefabPath);
        Object.DestroyImmediate(copy);

        Debug.Log($"[CafePrefabBuilder] {FogPrefabPath} 생성.");
        return prefab;
    }

    /// 등록하지 않으면 서버는 스폰하는데 클라이언트가 프리팹을 못 찾아 조용히 빈다.
    static void RegisterNetworkPrefab(GameObject prefab)
    {
        var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsPath);
        if (list == null)
        {
            Debug.LogError($"[CafePrefabBuilder] {NetworkPrefabsPath}를 못 찾았다. "
                         + "Cafe 프리팹을 네트워크 프리팹 목록에 직접 추가해야 한다.");
            return;
        }

        if (list.Contains(prefab))
        {
            Debug.Log("[CafePrefabBuilder] Cafe 프리팹은 이미 네트워크 프리팹 목록에 있다.");
            return;
        }

        list.Add(new NetworkPrefab { Prefab = prefab });
        EditorUtility.SetDirty(list);
        Debug.Log("[CafePrefabBuilder] Cafe 프리팹을 네트워크 프리팹 목록에 등록했다.");
    }

    /// 씬의 MatchDirector에 방금 구운 프리팹을 꽂아 준다. 직렬화 필드라 SerializedObject로
    /// 쓴다 — private [SerializeField]에 코드로 접근하는 정식 경로다.
    static void WireDirector(GameObject cafePrefab, GameObject fogPrefab)
    {
        var director = Object.FindFirstObjectByType<MatchDirector>();
        if (director == null)
        {
            Debug.LogWarning("[CafePrefabBuilder] 열린 씬에 MatchDirector가 없다. "
                           + "cafePrefab / fogPlanePrefab을 직접 연결해야 한다.");
            return;
        }

        var so = new SerializedObject(director);
        var cafeField = so.FindProperty("cafePrefab");
        var fogField = so.FindProperty("fogPlanePrefab");
        if (cafeField == null)
        {
            Debug.LogError("[CafePrefabBuilder] MatchDirector에 cafePrefab 필드가 없다. "
                         + "필드 이름이 바뀌었으면 이 도구도 같이 고쳐야 한다.");
            return;
        }

        cafeField.objectReferenceValue = cafePrefab.GetComponent<Cafe>();
        if (fogField != null && fogPrefab != null) fogField.objectReferenceValue = fogPrefab;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        Debug.Log("[CafePrefabBuilder] MatchDirector에 프리팹을 연결했다. 씬을 저장하라.");
    }

    static GameObject FindInScene(string name)
    {
        var found = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(t => t.parent == null && t.name == name);

        if (found == null)
            Debug.LogError($"[CafePrefabBuilder] 열린 씬에서 루트 오브젝트 '{name}'을 못 찾았다.");

        return found != null ? found.gameObject : null;
    }

    static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
    }
}
