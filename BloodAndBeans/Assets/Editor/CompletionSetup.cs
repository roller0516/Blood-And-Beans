using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CompletionSetup
{
    const string CharacterFolder = "Assets/Art/Character/Prefabs/";
    const string GroveScene = "Assets/Scenes/Battle_BerryGrove.unity";

    public static void Characters()
    {
        var config = Resources.Load<CharacterVisualConfig>(CharacterVisualConfig.AssetName);
        var data = new SerializedObject(config);
        var entries = data.FindProperty("entries");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterFolder + "PC_Ghost_001.prefab");
        entries.arraySize = CharacterCatalog.All.Length;
        for (var i = 0; i < entries.arraySize; i++)
        {
            var path = CharacterFolder + "Ghost_" + CharacterCatalog.All[i].Id + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                var root = new GameObject("Ghost_" + CharacterCatalog.All[i].Id);
                PrefabUtility.InstantiatePrefab(source, root.transform);
                // 임시 몬스터 실루엣. 최종 모델이 오면 표시 설정의 프리팹만 교체한다 (#37).
                var crown = GameObject.CreatePrimitive(i % 2 == 0 ? PrimitiveType.Capsule : PrimitiveType.Cube);
                crown.name = "MonsterCrest";
                Object.DestroyImmediate(crown.GetComponent<Collider>());
                crown.transform.SetParent(root.transform, false);
                crown.transform.localPosition = new Vector3(0, 1.65f, 0);
                crown.transform.localScale = new Vector3(.3f + i * .06f, .25f + (i % 3) * .2f, .25f);
                crown.transform.localRotation = Quaternion.Euler(0, 0, (i - 3) * 12f);
                prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
            }
            var entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("id").enumValueIndex = (int)CharacterCatalog.All[i].Id;
            entry.FindPropertyRelative("model").objectReferenceValue = prefab;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        var playerPath = CharacterFolder + "Player.prefab";
        var player = PrefabUtility.LoadPrefabContents(playerPath);
        try
        {
            var root = player.transform.Find("CharacterModel");
            if (root == null)
            {
                root = new GameObject("CharacterModel").transform;
                root.SetParent(player.transform, false);
                root.localPosition = new Vector3(0, -1, 0);
            }
            var look = new SerializedObject(player.GetComponent<PlayerLook>());
            look.FindProperty("visuals").objectReferenceValue = config;
            look.FindProperty("modelRoot").objectReferenceValue = root;
            look.FindProperty("defaultModel").objectReferenceValue = player.transform.Find("PC_Ghost_001").gameObject;
            look.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(player, playerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        AssetDatabase.SaveAssets();
    }

    public static void Grove()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GroveScene) == null)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Battle_01.unity");
            EditorSceneManager.SaveScene(scene, GroveScene);
            var director = new SerializedObject(Object.FindFirstObjectByType<MatchDirector>());
            director.FindProperty("mapId").stringValue = RegenTable.BerryGroveMapId;
            director.FindProperty("mapSeed").intValue = 20260911;
            director.FindProperty("forestDensity").floatValue = .8f;
            director.ApplyModifiedPropertiesWithoutUndo();
            ForestMapBuilder.Build();
            EditorSceneManager.SaveScene(scene);
        }
        if (!EditorBuildSettings.scenes.Any(s => s.path == GroveScene))
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(GroveScene, true) }).ToArray();
        var path = "Assets/Resources/GameManager.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var lobby = new SerializedObject(root.GetComponentInChildren<SteamLobby>(true));
            var maps = lobby.FindProperty("mapScenes");
            maps.arraySize = 2;
            maps.GetArrayElementAtIndex(0).stringValue = "Battle_01";
            maps.GetArrayElementAtIndex(1).stringValue = "Battle_BerryGrove";
            lobby.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }
}
