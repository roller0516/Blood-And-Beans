using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// 아이템을 화면에 세우는 데 필요한 애셋과 배선을 한 번에 만든다 — 아이템 프리팹,
/// 표시 설정 애셋(`ItemVisualConfig`), 그리고 프리팹 안의 앵커와 `ItemDisplay` 배선.
///
/// 손으로 하지 않는 이유는 자리가 많고 규칙이 하나여서다. 카페 하나에만 조리대 1칸,
/// 커피 머신 2대 × 4칸, 오븐 4칸, 재료 칸 10칸이 있고 팀 수만큼 복제된다. 앵커를 손으로
/// 놓으면 설비가 하나 늘 때마다 다시 놓아야 하고, 하나를 빠뜨리면 그 칸만 조용히
/// 안 보인다.
///
/// **여러 번 돌려도 안전하다.** 앵커는 지우고 다시 만들고, 프리팹·애셋은 있으면 덮어쓴다.
/// 자리 수는 `IItemHolder.SlotCount`가 정하므로 설비의 적재 칸을 늘리고 다시 돌리면
/// 앵커도 따라 늘어난다.
///
/// ponytail: 재료 9종은 메시가 없어서 프리미티브로 세운다. 아트가 들어오면
/// `ItemVisualConfig`의 프리팹 칸만 갈아 끼우면 되고 이 도구는 다시 돌리지 않아도 된다.
public static class ItemVisualBuilder
{
    const string MenuPath = "Tools/Blood & Beans/아이템 표시 세우기";

    const string ItemFolder = "Assets/Prefabs/Items";
    const string MaterialFolder = "Assets/Art/Materials/Items";
    const string ConfigPath = "Assets/Resources/" + ItemVisualConfig.AssetName + ".asset";
    const string KenneyFolder = "Assets/AssetStore/Kenney/cafe-selection/Models";
    const string PlayerPrefab = "Assets/Prefabs/Player.prefab";
    const string CafePrefab = "Assets/Prefabs/Cafe.prefab";

    /// 앵커를 모아 두는 자식 이름. 다시 돌릴 때 통째로 지우고 새로 만든다.
    const string AnchorRoot = "ItemAnchors";

    /// 아이템의 가장 긴 변. 캐릭터가 2유닛(캡슐)이라 이 크기면 멀리서도 무엇인지 읽힌다.
    /// 실물 비례보다 크다 — 오버쿡드 계열이 전부 그렇게 한다.
    /// ponytail: 눈으로 보고 정한 값이다. 카메라 거리가 확정되면 다시 본다.
    const float ItemSize = 0.25f;

    /// 설비 윗면과 아이템 사이의 틈. 0이면 메시 오차로 파고들어 보인다.
    const float SurfaceGap = 0.02f;

    /// 앵커를 설비 윗면 가로·세로의 몇 할에 펼칠지.
    const float SpreadX = 0.7f;
    const float SpreadZ = 0.4f;

    /// 한 줄에 놓는 최대 칸 수. 이보다 많으면 두 줄로 접는다 (재료 칸 10칸).
    const int MaxPerRow = 5;

    /// 손의 자리. 캐릭터가 캡슐(높이 2, 반지름 0.5, 중심이 원점)이라 가슴 높이 앞이다.
    /// ponytail: 진짜 캐릭터 메시가 들어오면 손 본에 붙이고 이 값을 버린다.
    static readonly Vector3 HandOffset = new(0f, 0.35f, 0.55f);

    /// 재료 10종. 순서는 기획서 7.1 재료 표(`Ingredient` 열거자)와 같다.
    /// model이 있으면 그 메시를 쓰고, 없으면 프리미티브를 그 색·비율로 눌러 세운다.
    static readonly (Ingredient id, string model, PrimitiveType shape, uint colour, Vector3 squash)[]
        IngredientItems =
    {
        (Ingredient.Milk,        null, PrimitiveType.Cylinder, 0xF2F0E6, new(0.7f, 1f,    0.7f)),
        (Ingredient.Cream,       null, PrimitiveType.Sphere,   0xF7EBD2, new(1f,   0.8f,  1f)),
        (Ingredient.Chocolate,   null, PrimitiveType.Cube,     0x4A2C1A, new(1f,   0.35f, 0.7f)),
        (Ingredient.Almond,      null, PrimitiveType.Sphere,   0xD9B98C, new(0.7f, 0.5f,  1f)),
        (Ingredient.Berry,       null, PrimitiveType.Sphere,   0xC0304A, new(1f,   0.9f,  1f)),
        (Ingredient.Ice,         null, PrimitiveType.Cube,     0xBFE3F0, new(1f,   0.9f,  1f)),
        (Ingredient.BloodBean,   null, PrimitiveType.Sphere,   0x6E1520, new(1f,   0.6f,  0.75f)),
        (Ingredient.UpgradePart, null, PrimitiveType.Cube,     0x8C8C96, new(1f,   0.45f, 1f)),
        (Ingredient.Bean,        null, PrimitiveType.Sphere,   0x5A3A22, new(1f,   0.6f,  0.75f)),

        // 빵 베이스만 Kenney 키트에 실물이 있다.
        (Ingredient.BreadBase,   "bread", PrimitiveType.Cube,  0xFFFFFF, Vector3.one),
    };

    /// 메뉴 10종. 전부 Kenney 카페 키트의 메시다 — 커피는 잔 둘로 뜨거운 것과 찬 것을
    /// 가르고, 디저트는 종류별로 다른 메시를 준다.
    /// ponytail: 브라우니를 빵 덩어리로, 타르트를 도넛으로 대신한다. 키트에 그 둘이 없다.
    static readonly (MenuId id, string model)[] MenuItems =
    {
        (MenuId.HotAmericano,  "mug"),
        (MenuId.IcedAmericano, "cup-coffee"),
        (MenuId.CafeLatte,     "mug"),
        (MenuId.Einspanner,    "mug"),
        (MenuId.CafeMocha,     "mug"),
        (MenuId.IcedLatte,     "cup-coffee"),
        (MenuId.ChocoBrownie,  "bread"),
        (MenuId.AlmondCookie,  "cookie"),
        (MenuId.CreamCake,     "cake"),
        (MenuId.BerryTart,     "donut"),
    };

    /// 메뉴 표에 없는 조합의 완성품 (`CarryView`의 「정체불명」). 무엇인지 모르겠다는
    /// 것이 보여야 하므로 일부러 아무것도 닮지 않은 회색 덩어리다.
    const uint UnknownColour = 0x6B6B73;

    /// 탄 것. 메시는 그대로 두고 이 재질로 덮는다.
    const uint BurntColour = 0x1E1712;

    [MenuItem(MenuPath)]
    public static void Build()
    {
        EnsureFolder("Assets/Prefabs", "Items");
        EnsureFolder("Assets/Art/Materials", "Items");

        var config = BuildConfig();
        if (config == null) return;

        var wired = new List<string>();
        Wire(PlayerPrefab, config, wired);
        Wire(CafePrefab, config, wired);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        CDebug.Log($"[ItemVisualBuilder] 아이템 {IngredientItems.Length + MenuItems.Length + 1}종 · "
                 + $"표시 자리 {wired.Count}곳");
        foreach (var line in wired) CDebug.Log($"  + {line}");
    }

    /// 세워 둔 것이 실제로 이어져 있는지 다시 확인한다. 배선은 사람이 눈으로 세기에는
    /// 많고(자리 6곳 · 칸 24개 · 아이템 21종) 하나가 빠져도 그 칸만 조용히 비어 있는다.
    /// 빌더를 돌린 뒤와 프리팹을 손댄 뒤에 돌린다.
    [MenuItem(MenuPath + " 점검")]
    public static void Verify()
    {
        var failures = 0;
        var config = AssetDatabase.LoadAssetAtPath<ItemVisualConfig>(ConfigPath);
        if (config == null)
        {
            CDebug.LogError($"[ItemVisualBuilder] 표시 설정이 없다: {ConfigPath}");
            return;
        }

        foreach (Ingredient id in System.Enum.GetValues(typeof(Ingredient)))
        {
            if (id == Ingredient.None) continue;
            if (config.PrefabFor(CarryView.Of(id)) != null) continue;

            CDebug.LogError($"[ItemVisualBuilder] 재료 {id}에 프리팹이 없다.");
            failures++;
        }

        foreach (MenuId id in System.Enum.GetValues(typeof(MenuId)))
        {
            if (id == MenuId.None) continue;
            var product = CarryView.Of(new HeldItem { IsProduct = true, Menu = id });
            if (config.PrefabFor(product) != null) continue;

            CDebug.LogError($"[ItemVisualBuilder] 메뉴 {id}에 프리팹이 없다.");
            failures++;
        }

        // 메뉴 표에 없는 조합도 완성품이 된다 (`Menus.Match`). 그것이 화면에서 사라지면
        // 기계 위에 아무것도 없는 것처럼 보인다.
        var unknown = CarryView.Of(new HeldItem { IsProduct = true, Menu = MenuId.None });
        if (config.PrefabFor(unknown) == null)
        {
            CDebug.LogError("[ItemVisualBuilder] 정체불명 완성품에 프리팹이 없다.");
            failures++;
        }

        if (config.Burnt == null)
        {
            CDebug.LogError("[ItemVisualBuilder] 탄 것 재질이 없다.");
            failures++;
        }

        failures += VerifyPrefab(PlayerPrefab, config);
        failures += VerifyPrefab(CafePrefab, config);

        if (failures == 0) CDebug.Log("[ItemVisualBuilder] 점검 통과.");
        else CDebug.LogError($"[ItemVisualBuilder] 점검 실패 {failures}건.");
    }

    static int VerifyPrefab(string prefabPath, ItemVisualConfig config)
    {
        var failures = 0;
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is not IItemHolder holder) continue;

                var display = behaviour.GetComponent<ItemDisplay>();
                if (display == null)
                {
                    CDebug.LogError($"[ItemVisualBuilder] {prefabPath}의 {behaviour.name}에 "
                                  + "ItemDisplay가 없다.");
                    failures++;
                    continue;
                }

                var so = new SerializedObject(display);
                if (so.FindProperty("config").objectReferenceValue != config)
                {
                    CDebug.LogError($"[ItemVisualBuilder] {behaviour.name}의 표시 설정이 다르다.");
                    failures++;
                }

                var list = so.FindProperty("anchors");
                if (list.arraySize != holder.SlotCount)
                {
                    CDebug.LogError($"[ItemVisualBuilder] {behaviour.name}의 앵커가 "
                                  + $"{list.arraySize}개인데 칸은 {holder.SlotCount}개다.");
                    failures++;
                }

                for (var i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue != null) continue;

                    CDebug.LogError($"[ItemVisualBuilder] {behaviour.name}의 앵커 {i}가 비었다.");
                    failures++;
                }
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        return failures;
    }

    /// 아이템 프리팹을 전부 세우고 표시 설정에 꽂는다.
    static ItemVisualConfig BuildConfig()
    {
        var config = AssetDatabase.LoadAssetAtPath<ItemVisualConfig>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<ItemVisualConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
        }

        var so = new SerializedObject(config);

        var ingredients = so.FindProperty("ingredients");
        ingredients.arraySize = IngredientItems.Length;
        for (var i = 0; i < IngredientItems.Length; i++)
        {
            var item = IngredientItems[i];
            var prefab = EnsureItem(
                "Item" + item.id, item.model, item.shape, Hex(item.colour), item.squash);
            if (prefab == null) return null;

            var entry = ingredients.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("id").intValue = (int)item.id;
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        }

        var menus = so.FindProperty("menus");
        menus.arraySize = MenuItems.Length;
        for (var i = 0; i < MenuItems.Length; i++)
        {
            var item = MenuItems[i];
            var prefab = EnsureItem("Menu" + item.id, item.model, PrimitiveType.Cube,
                                    Color.white, Vector3.one);
            if (prefab == null) return null;

            var entry = menus.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("id").intValue = (int)item.id;
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        }

        so.FindProperty("unknownProduct").objectReferenceValue = EnsureItem(
            "ItemUnknown", null, PrimitiveType.Cube, Hex(UnknownColour), new(1f, 0.8f, 1f));
        so.FindProperty("burnt").objectReferenceValue =
            EnsureMaterial("ItemBurnt", Hex(BurntColour));

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
        return config;
    }

    /// 아이템 프리팹 하나. 어떤 메시가 들어오든 크기를 맞추고 바닥이 원점에 오게 세운다 —
    /// 앵커에 그대로 얹으면 설비 윗면에 놓인 것으로 보이게 하기 위해서다.
    static GameObject EnsureItem(
        string name, string model, PrimitiveType shape, Color colour, Vector3 squash)
    {
        var path = $"{ItemFolder}/{name}.prefab";
        var root = new GameObject(name);
        try
        {
            GameObject visual;
            if (!string.IsNullOrEmpty(model))
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{KenneyFolder}/{model}.fbx");
                if (source == null)
                {
                    CDebug.LogError($"[ItemVisualBuilder] {model}.fbx가 없다. {name}을 만들지 못했다.");
                    return null;
                }

                visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
                visual.transform.SetParent(root.transform, false);
            }
            else
            {
                visual = GameObject.CreatePrimitive(shape);
                visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = squash;
                visual.GetComponent<Renderer>().sharedMaterial = EnsureMaterial(name, colour);
            }

            // 아이템은 부딪히는 것이 아니라 보이는 것이다. 콜라이더가 남으면 손에 든 컵이
            // 플레이어를 밀어낸다.
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);

            Fit(root);
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// 가장 긴 변을 ItemSize에 맞추고 바닥면을 원점으로 올린다. 원본 메시의 크기와 피벗이
    /// 제각각이라(Kenney 키트와 프리미티브가 섞인다) 여기서 한 번에 맞춘다.
    static void Fit(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        var longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (longest <= 0f) return;

        var scale = ItemSize / longest;
        var lift = Vector3.up * (bounds.size.y * scale * 0.5f);

        // 루트는 원점에 있고 회전이 없다. 그래서 월드 경계가 곧 로컬 경계다.
        foreach (Transform child in root.transform)
        {
            child.localScale *= scale;
            child.localPosition = (child.localPosition - bounds.center) * scale + lift;
        }
    }

    static Material EnsureMaterial(string name, Color colour)
    {
        var path = $"{MaterialFolder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }

        // 색은 매번 다시 넣는다. 표를 고치고 다시 돌리면 반영되어야 한다.
        material.SetColor("_BaseColor", colour);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.1f);
        EditorUtility.SetDirty(material);
        return material;
    }

    /// 프리팹 안의 모든 아이템 자리에 앵커를 놓고 `ItemDisplay`를 붙인다. 자리를 코드에
    /// 나열하지 않는다 — `IItemHolder`를 구현한 것이 곧 자리다.
    static void Wire(string prefabPath, ItemVisualConfig config, List<string> log)
    {
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var found = 0;
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is not IItemHolder holder) continue;

                found++;
                var anchors = BuildAnchors(behaviour.transform, holder.SlotCount,
                                           behaviour is PlayerCarry);

                var display = behaviour.GetComponent<ItemDisplay>();
                if (display == null) display = behaviour.gameObject.AddComponent<ItemDisplay>();

                var so = new SerializedObject(display);
                so.FindProperty("config").objectReferenceValue = config;

                var list = so.FindProperty("anchors");
                list.arraySize = anchors.Count;
                for (var i = 0; i < anchors.Count; i++)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = anchors[i];

                so.ApplyModifiedPropertiesWithoutUndo();
                log.Add($"{System.IO.Path.GetFileNameWithoutExtension(prefabPath)} · "
                      + $"{behaviour.name} ({behaviour.GetType().Name}) {anchors.Count}칸");
            }

            if (found == 0)
                CDebug.LogError($"[ItemVisualBuilder] {prefabPath}에 IItemHolder가 하나도 없다.");

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// 자리 하나에 앵커를 칸 수만큼 놓는다. 설비는 자기 윗면에, 플레이어는 손 위치에.
    static List<Transform> BuildAnchors(Transform owner, int count, bool isHand)
    {
        var old = owner.Find(AnchorRoot);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var parent = new GameObject(AnchorRoot).transform;
        parent.SetParent(owner, false);

        var anchors = new List<Transform>(count);
        var bounds = LocalBounds(owner);

        var rows = count <= MaxPerRow ? 1 : 2;
        var perRow = Mathf.CeilToInt(count / (float)rows);

        for (var i = 0; i < count; i++)
        {
            var slot = new GameObject($"Slot{i}").transform;
            slot.SetParent(parent, false);

            if (isHand)
            {
                slot.localPosition = HandOffset;
            }
            else
            {
                var col = i % perRow;
                var row = i / perRow;
                var x = perRow == 1
                    ? bounds.center.x
                    : bounds.center.x + (col / (perRow - 1f) - 0.5f) * bounds.size.x * SpreadX;
                var z = rows == 1
                    ? bounds.center.z
                    : bounds.center.z + (row / (rows - 1f) - 0.5f) * bounds.size.z * SpreadZ;

                slot.localPosition = new Vector3(x, bounds.max.y + SurfaceGap, z);
            }

            anchors.Add(slot);
        }

        return anchors;
    }

    /// 이 오브젝트가 차지하는 공간을 자기 로컬 좌표로 잰다. 설비의 메시는 Model 밑에 따로
    /// 옮겨져 있어서(카페 프리팹) 트랜스폼만 봐서는 윗면을 알 수 없다.
    static Bounds LocalBounds(Transform owner)
    {
        var renderers = owner.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one * 0.5f);

        var bounds = new Bounds(owner.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
        foreach (var renderer in renderers)
        {
            var box = renderer.bounds;
            for (var corner = 0; corner < 8; corner++)
            {
                var point = new Vector3(
                    (corner & 1) == 0 ? box.min.x : box.max.x,
                    (corner & 2) == 0 ? box.min.y : box.max.y,
                    (corner & 4) == 0 ? box.min.z : box.max.z);
                bounds.Encapsulate(owner.InverseTransformPoint(point));
            }
        }
        return bounds;
    }

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
            AssetDatabase.CreateFolder(parent, name);
    }

    static Color Hex(uint rgb) => new(
        ((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
}
