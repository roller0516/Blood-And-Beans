using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Netcode;

/// 기존 카페 모델을 재사용해 v5.0 광장과 재료 마무리 칸을 연결한다.
public static class DayV5Setup
{
    const string CafePath = "Assets/Art/Environment/Prefabs/Cafe.prefab";
    const string PlayerPath = "Assets/Art/Character/Prefabs/Player.prefab";
    const string BattlePath = "Assets/Scenes/Battle_01.unity";
    const string PlazaName = "SharedPlaza";

    public static string Apply()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("플레이를 종료한 뒤 적용한다.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new System.InvalidOperationException("저장되지 않은 씬 변경이 있다.");
        EditorSceneManager.OpenScene(BattlePath);
        var director = Object.FindFirstObjectByType<MatchDirector>();
        if (director == null) throw new System.InvalidOperationException("매치 디렉터가 없다.");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(CafePath);
        var existing = GameObject.Find(PlazaName);
        if (existing == null)
        {
            var plaza = new GameObject(PlazaName);
            plaza.transform.position = director.PlazaCenter;
            plaza.AddComponent<NetworkObject>();
            var coffee = source.GetComponentInChildren<CoffeeMachine>(true).transform.Find("Model");
            var oven = source.GetComponentInChildren<Oven>(true).transform.Find("Model");
            var sink = source.GetComponentInChildren<Sink>(true).transform.Find("Model");
            // ponytail: 광장 실치수·오븐 대수는 14장 #4 미결. 간격 4m·오븐 2대로 검증 후 조정한다.
            for (var i = 0; i < DayBalance.Machines; i++) Facility(plaza.transform, FacilityKind.Coffee, new Vector3((i - 1) * 4, 0.5f, 4), coffee);
            for (var i = 0; i < 2; i++) Facility(plaza.transform, FacilityKind.Oven, new Vector3((i * 2 - 1) * 4, 0.5f, -4), oven);
            for (var i = 0; i < DayBalance.Sinks; i++) Facility(plaza.transform, FacilityKind.Sink, new Vector3(10, 0.5f, (i - 1) * 4), sink);
            Facility(plaza.transform, FacilityKind.Beans, new Vector3(-10, 0.5f, 3), null);
            Facility(plaza.transform, FacilityKind.Bread, new Vector3(-10, 0.5f, -3), null);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "PlazaFloor";
            floor.transform.SetParent(plaza.transform, false);
            floor.transform.localPosition = new Vector3(0, -0.15f, 0);
            floor.transform.localScale = new Vector3(84, 0.2f, 72);
            floor.GetComponent<Renderer>().sharedMaterial = source.transform.Find("Floor").GetComponent<Renderer>().sharedMaterial;
        }
        var cafe = PrefabUtility.LoadPrefabContents(CafePath);
        try
        {
            foreach (var station in cafe.GetComponentsInChildren<Station>(true)) Hide(station.gameObject);
            foreach (var sink in cafe.GetComponentsInChildren<Sink>(true)) Hide(sink.gameObject);
            var shelves = cafe.GetComponentsInChildren<IngredientShelf>(true);
            var shelf = shelves.First(x => x.name == "Shelf");
            var ingredients = new[] { Ingredient.Milk, Ingredient.Cream, Ingredient.Chocolate, Ingredient.Almond,
                Ingredient.Berry, Ingredient.Ice, Ingredient.BloodBean };
            for (var i = 0; i < ingredients.Length; i++)
            {
                var name = "Finish_" + ingredients[i];
                var target = cafe.transform.Find(name);
                if (target == null) target = Object.Instantiate(shelf.gameObject, cafe.transform).transform;
                target.name = name;
                target.localPosition = new Vector3(-9 + i * 3, 0.5f, -5);
                var serialized = new SerializedObject(target.GetComponent<IngredientShelf>());
                var offer = serialized.FindProperty("offer");
                offer.arraySize = 1; offer.GetArrayElementAtIndex(0).intValue = (int)ingredients[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                target.gameObject.SetActive(true);
            }
            foreach (var old in shelves.Where(x => !x.name.StartsWith("Finish_"))) old.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(cafe, CafePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(cafe); }
        SetupPlayer();
        SetupGemIcons();
        EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        EditorSceneManager.SaveScene(director.gameObject.scene);
        AssetDatabase.SaveAssets();
        return "공용 머신 3 / 오븐 2 / 개수대 3 / 재료함 2, 카페 마무리 칸 7 연결";
    }

    static void Hide(GameObject root)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        foreach (var collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var display in root.GetComponentsInChildren<ItemDisplay>(true)) display.enabled = false;
    }
    static void Facility(Transform parent, FacilityKind kind, Vector3 at, Transform model)
    {
        var root = new GameObject(kind.ToString());
        root.transform.SetParent(parent, false); root.transform.localPosition = at;
        var collider = root.AddComponent<BoxCollider>(); collider.size = new Vector3(2, 2, 2);
        var facility = root.AddComponent<SharedFacility>();
        if (model != null) Object.Instantiate(model.gameObject, root.transform, false);
        else
        {
            var shape = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shape.transform.SetParent(root.transform, false);
            Object.DestroyImmediate(shape.GetComponent<Collider>());
        }
        var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        lamp.name = "Occupancy"; lamp.transform.SetParent(root.transform, false);
        lamp.transform.localPosition = Vector3.up * 2; lamp.transform.localScale = Vector3.one * 0.35f;
        Object.DestroyImmediate(lamp.GetComponent<Collider>());
        var serialized = new SerializedObject(facility);
        serialized.FindProperty("kind").enumValueIndex = (int)kind;
        serialized.FindProperty("statusRenderer").objectReferenceValue = lamp.GetComponent<Renderer>();
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    static void SetupPlayer()
    {
        var player = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            if (player.GetComponentInChildren<PublicCarryDisplay>(true) == null)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "PublicCarry"; marker.transform.SetParent(player.transform, false);
                marker.transform.localPosition = new Vector3(0.55f, 0.2f, 0.4f);
                marker.transform.localScale = Vector3.one * 0.25f;
                Object.DestroyImmediate(marker.GetComponent<Collider>());
                var display = marker.AddComponent<PublicCarryDisplay>();
                var serialized = new SerializedObject(display);
                serialized.FindProperty("carry").objectReferenceValue = player.GetComponent<PlayerCarry>();
                serialized.FindProperty("team").objectReferenceValue = player.GetComponent<PlayerTeam>();
                serialized.FindProperty("marker").objectReferenceValue = marker.GetComponent<Renderer>();
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
    }
    static void SetupGemIcons()
    {
        const string path = "Assets/Art/UI/Prefabs/Popup/UIBoxLootPopup.prefab";
        var popup = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var serialized = new SerializedObject(popup.GetComponent<UIBoxLootPopup>());
            var icons = serialized.FindProperty("icons");
            for (var i = 0; i < TeamBuffs.Materials.Length; i++)
            {
                var imagePath = "Assets/Art/UI/Sprites/BuffGem_" + (TeamBuff)i + ".png";
                var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                var color = Color.HSVToRGB(i / 8f, 0.65f, 1f);
                for (var y = 0; y < 32; y++)
                    for (var x = 0; x < 32; x++)
                    {
                        var radius = Mathf.Abs(x - 15.5f) + Mathf.Abs(y - 15.5f);
                        texture.SetPixel(x, y, radius > 14 ? Color.clear : radius > 11 ? color * 0.65f : color);
                    }
                texture.Apply();
                System.IO.File.WriteAllBytes(imagePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(imagePath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(imagePath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
                var index = -1;
                for (var j = 0; j < icons.arraySize; j++)
                    if (icons.GetArrayElementAtIndex(j).FindPropertyRelative("Item").intValue == (int)TeamBuffs.Materials[i]) index = j;
                if (index < 0) { index = icons.arraySize; icons.arraySize++; }
                var entry = icons.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("Item").intValue = (int)TeamBuffs.Materials[i];
                entry.FindPropertyRelative("Sprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(popup, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(popup); }
    }
}
