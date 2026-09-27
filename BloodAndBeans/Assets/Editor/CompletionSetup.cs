using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
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
            entry.FindPropertyRelative("model").FindPropertyRelative("m_AssetGUID").stringValue =
                AssetDatabase.AssetPathToGUID(path);
            EnsureAddressable(path, "Character_" + CharacterCatalog.All[i].Id);
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
            var look = new SerializedObject(player.GetComponent<PlayerVisuals>());
            look.FindProperty("characterVisuals").objectReferenceValue = config;
            look.FindProperty("modelRoot").objectReferenceValue = root;
            look.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(player, playerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        AssetDatabase.SaveAssets();
    }

    /// 이 프리팹이 어드레서블 표에 없으면 등록한다. 무거운 캐릭터 모델을 `AssetReference`로
    /// 물기만 하고 실제 그룹에 없으면 런타임 로드가 "unknown key"로 실패한다.
    static void EnsureAddressable(string path, string address)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) return;
        var guid = AssetDatabase.AssetPathToGUID(path);
        if (string.IsNullOrEmpty(guid)) return;
        var group = settings.FindGroup("Default Local Group") ?? settings.DefaultGroup;
        var entry = settings.CreateOrMoveEntry(guid, group, false, false);
        entry.address = address;
        EditorUtility.SetDirty(settings);
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
        AssetDatabase.SaveAssets();
    }
}
