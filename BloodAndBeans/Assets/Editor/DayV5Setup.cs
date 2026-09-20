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
            // 「Shelf」는 폐기된 그리드 선반이다 (기획서 14장 #35). 없으면 이미 만들어진 칸을 템플릿으로 쓴다.
            var shelf = shelves.FirstOrDefault(x => x.name == "Shelf") ?? shelves.First();
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
        EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        EditorSceneManager.SaveScene(director.gameObject.scene);
        AssetDatabase.SaveAssets();
        return "공용 머신 3 / 오븐 2 / 개수대 3 / 재료함 2, 카페 마무리 칸 7 연결";
    }

    public static string SetupDishRacks()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("플레이를 종료한 뒤 적용한다.");
        var cafe = PrefabUtility.LoadPrefabContents(CafePath);
        try
        {
            var source = cafe.GetComponentInChildren<PrepIsland>(true);
            for (var i = 0; i < 2; i++)
            {
                var rackName = i == 0 ? "CupRack" : "PlateRack";
                if (cafe.transform.Find(rackName) != null) continue;
                var root = Object.Instantiate(source.gameObject, cafe.transform);
                root.name = rackName;
                // ponytail: 카페 실치수는 14장 #4 미결. 기존 보관대 옆에 배치하고 레벨 확정 시 조정한다.
                root.transform.localPosition = new Vector3(i == 0 ? -7 : 7, 0.5f, 4);
                Object.DestroyImmediate(root.GetComponent<PrepIsland>());
                var rack = root.AddComponent<DishRack>();
                var serialized = new SerializedObject(rack);
                serialized.FindProperty("plate").boolValue = i == 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(cafe, CafePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(cafe); }
        return "카페 잔·접시 수령대 연결";
    }
    public static string SetupWorldGauges()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("플레이를 종료한 뒤 적용한다.");
        const string gaugePath = "Assets/Art/UI/Prefabs/Parts/UICompletionGauge.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(gaugePath) == null)
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/UI/Prefabs/Screen/UIMatchHudScreen.prefab");
            var fields = new SerializedObject(hud.GetComponent<UIMatchHudScreen>());
            var source = (RectTransform)fields.FindProperty("completionBar").objectReferenceValue;
            var root = new GameObject("UICompletionGauge", typeof(RectTransform), typeof(Canvas), typeof(UICompletionGauge));
            try
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(420, 80);
                rect.localScale = Vector3.one * 0.007f;
                var bar = Object.Instantiate(source, root.transform, false);
                bar.gameObject.SetActive(true);
                bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0.5f, 0.5f);
                bar.anchoredPosition = Vector2.zero;
                var serialized = new SerializedObject(root.GetComponent<UICompletionGauge>());
                serialized.FindProperty("canvas").objectReferenceValue = canvas;
                serialized.FindProperty("bar").objectReferenceValue = bar;
                serialized.FindProperty("good").objectReferenceValue = bar.Find("GoodZone");
                serialized.FindProperty("perfect").objectReferenceValue = bar.Find("PerfectZone");
                serialized.FindProperty("needle").objectReferenceValue = bar.Find("Needle");
                serialized.FindProperty("label").objectReferenceValue = bar.GetComponentInChildren<TMPro.TMP_Text>(true);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                foreach (var graphic in root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.raycastTarget = false;
                PrefabUtility.SaveAsPrefabAsset(root, gaugePath);
            }
            finally { Object.DestroyImmediate(root); }
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(gaugePath);
        var cafe = PrefabUtility.LoadPrefabContents(CafePath);
        try
        {
            foreach (var gauge in cafe.GetComponentsInChildren<CompletionGauge>(true))
                if (gauge.GetComponentInChildren<UICompletionGauge>(true) == null)
                    PrefabUtility.InstantiatePrefab(prefab, gauge.transform);
            PrefabUtility.SaveAsPrefabAsset(cafe, CafePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(cafe); }
        return "팀 전용 설비별 월드 게이지 연결";
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
}
