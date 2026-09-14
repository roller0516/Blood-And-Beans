using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// 카페 외관(`CafeDecor`)을 본체에서 떼어 공개 껍데기 프리팹으로 만든다.
///
/// 카페 본체는 NetworkObject가 루트 하나뿐이라 상대 팀에 복제하는 순간 재고·설비·손님·예산이
/// 함께 열린다 (기획서 3.4 · 5.4-18). 벽과 입구는 전원에게 보여야 하므로(5.7.6 · 5.4.3-2)
/// 복제가 필요 없는 외관만 떼어 내 피어마다 각자 세운다 (`MatchDirector.SpawnCafeShells`).
///
/// 외관에는 콜라이더가 없어서(벽 판넬은 순수 비주얼) 로컬 뷰로 옮겨도 서버 충돌이 바뀌지 않는다.
/// `Floor`는 콜라이더와 `Cafe.Floor` 참조가 걸려 있어 본체에 남긴다.
public static class CafeShellSetup
{
    const string CafePath = "Assets/Art/Environment/Prefabs/Cafe.prefab";
    const string ShellPath = "Assets/Art/Environment/Prefabs/CafeShell.prefab";
    const string DecorName = "CafeDecor";

    static readonly string[] Scenes =
    {
        "Assets/Scenes/Battle_01.unity",
        "Assets/Scenes/Battle_BerryGrove.unity",
    };

    /// 다시 돌려도 된다. `CafeLayoutSetup`이 장식을 본체 안에 새로 지으므로, 그것을 돌린
    /// 뒤에는 이것도 다시 돌려 껍데기를 갱신한다.
    [MenuItem("Tools/Blood & Beans/카페 외관 껍데기 분리")]
    public static string Apply()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("플레이를 종료한 뒤 적용한다.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new System.InvalidOperationException("저장되지 않은 씬 변경이 있다.");

        var shell = Extract();
        var wired = WireScenes(shell);
        AssetDatabase.SaveAssets();
        return $"{ShellPath} 생성 · 씬 {wired}곳에 연결";
    }

    /// 본체에서 장식을 떼어 껍데기 프리팹으로 저장한다. 이미 떼어 낸 뒤면 기존 껍데기를 쓴다.
    /// 모든 카메라가 그리는 Unity 기본 레이어(Default). 광장(`SharedPlaza`)과 같다.
    const int PublicLayer = 0;

    static GameObject Extract()
    {
        var cafe = PrefabUtility.LoadPrefabContents(CafePath);
        try
        {
            var decor = cafe.transform.Find(DecorName);
            if (decor == null)
            {
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ShellPath);
                if (existing == null)
                    throw new System.InvalidOperationException(
                        $"{CafePath}에 {DecorName}이 없고 {ShellPath}도 없다. CafeLayoutSetup을 먼저 돌린다.");
                return existing;
            }

            // 껍데기 루트의 로컬 좌표가 카페 로컬 좌표와 같아야 본체와 겹쳐 선다.
            decor.localPosition = Vector3.zero;
            decor.localRotation = Quaternion.identity;
            decor.localScale = Vector3.one;

            // 본체는 팀 전용 레이어라 그대로 떼면 그 팀 카메라에만 보인다. 외관은 전원 공개다 (5.4.3-2).
            foreach (var t in decor.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = PublicLayer;

            var shell = PrefabUtility.SaveAsPrefabAsset(decor.gameObject, ShellPath);

            // 본체에서 지운다. 남겨 두면 자기 팀 카페에서 껍데기와 두 겹으로 겹쳐 z-파이팅이 난다.
            Object.DestroyImmediate(decor.gameObject);
            PrefabUtility.SaveAsPrefabAsset(cafe, CafePath);
            return shell;
        }
        finally { PrefabUtility.UnloadPrefabContents(cafe); }
    }

    static int WireScenes(GameObject shell)
    {
        var wired = 0;
        foreach (var path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path);
            var director = Object.FindFirstObjectByType<MatchDirector>();
            if (director == null) continue;

            var data = new SerializedObject(director);
            data.FindProperty("cafeShellPrefab").objectReferenceValue = shell;
            data.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            wired++;
        }
        return wired;
    }
}
