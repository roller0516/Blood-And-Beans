using System;
using UnityEditor;
using UnityEngine;

/// 숲 생성 도구가 사용하는 후각 경로 내비메시를 굽는다.
public static class CharacterSkillVfxBuilder
{
    /// 숲이 다시 생성되면 같은 씬의 경로 데이터도 함께 갱신한다.
    public static void BakeTrackNavigation(UnityEngine.SceneManagement.Scene scene)
    {
        var surfaceType = Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
        if (surfaceType == null) throw new InvalidOperationException("설치된 AI Navigation을 찾을 수 없다.");
        foreach (var root in scene.GetRootGameObjects())
        foreach (var terrain in root.GetComponentsInChildren<Terrain>())
        {
            var surface = terrain.GetComponent(surfaceType) ?? terrain.gameObject.AddComponent(surfaceType);
            var so = new SerializedObject(surface);
            so.FindProperty("m_CollectObjects").intValue = 2; // 설치된 CollectObjects.Children
            so.FindProperty("m_UseGeometry").intValue = (int)UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            so.FindProperty("m_AgentTypeID").intValue = UnityEngine.AI.NavMesh.GetSettingsByIndex(0).agentTypeID;
            so.ApplyModifiedPropertiesWithoutUndo();
            surfaceType.GetMethod("BuildNavMesh").Invoke(surface, null);
            var dataProperty = surfaceType.GetProperty("navMeshData");
            var data = dataProperty.GetValue(surface) as UnityEngine.AI.NavMeshData;
            if (data == null) throw new InvalidOperationException("숲 경로 데이터를 만들지 못했다.");
            var path = "Assets/Settings/" + scene.name + "_TrackScentNavigation.asset";
            var saved = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(path);
            if (saved == null) AssetDatabase.CreateAsset(data, path);
            else
            {
                surfaceType.GetMethod("RemoveData").Invoke(surface, null);
                EditorUtility.CopySerialized(data, saved);
                dataProperty.SetValue(surface, saved);
                surfaceType.GetMethod("AddData").Invoke(surface, null);
                UnityEngine.Object.DestroyImmediate(data);
                EditorUtility.SetDirty(saved);
            }
            AssetDatabase.SaveAssetIfDirty(saved != null ? saved : data);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
