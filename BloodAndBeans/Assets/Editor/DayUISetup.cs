using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// PDF rev.3와 기획서 5.7의 UI 배선을 기존 프리팹에 적용한다.
public static class DayUISetup
{
    const string Parts = "Assets/Art/UI/Prefabs/Parts/";
    const string CafePath = "Assets/Art/Environment/Prefabs/Cafe.prefab";
    const string CustomerPath = "Assets/Art/Environment/Prefabs/Customer.prefab";
    const string HudPath = "Assets/Art/UI/Prefabs/Screen/UIMatchHudScreen.prefab";
    static UIThemeConfig Theme => Resources.Load<UIThemeConfig>(UIThemeConfig.AssetName);
    static TMP_FontAsset Font => TMP_Settings.defaultFontAsset;
    public static string Apply()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("플레이 종료 후 적용한다.");
        MakeIcon();
        MakeBadge();
        MakeOrder();
        Edit(Parts + "UICustomerOrder.prefab", root =>
        {
            var view = root.GetComponent<UICustomerOrder>();
            var data = new SerializedObject(view);
            if (data.FindProperty("expectedPrice").objectReferenceValue != null) return;
            var price = Text(root.transform, "ExpectedPrice", 18);
            Stretch(price.rectTransform, 6);
            price.alignment = TextAlignmentOptions.BottomRight;
            Set(view, "expectedPrice", price);
        });
        Edit(CafePath, root =>
        {
            Set(root.GetComponent<Cafe>(), "floor", root.transform.Find("Floor").GetComponent<Collider>());
            foreach (var shelf in root.GetComponentsInChildren<IngredientShelf>(true))
                if (shelf.gameObject.activeSelf && shelf.GetComponentInChildren<UIIngredientBadge>(true) == null)
                    PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Parts + "UIIngredientBadge.prefab"), shelf.transform);
        });
        Edit(CustomerPath, root =>
        {
            var old = root.GetComponent<UICustomerPatienceBar>();
            if (old != null) Object.DestroyImmediate(old);
            if (root.GetComponentInChildren<UICustomerOrder>(true) == null)
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Parts + "UICustomerOrder.prefab"), root.transform);
        });
        Edit(Parts + "UICompletionGauge.prefab", root =>
        {
            var view = root.GetComponent<UICompletionGauge>();
            var data = new SerializedObject(view);
            var bar = (RectTransform)data.FindProperty("bar").objectReferenceValue;
            if (data.FindProperty("progress").objectReferenceValue == null)
            {
                var progress = Image(bar, "CookingProgress", Theme.Blue);
                progress.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                progress.type = UnityEngine.UI.Image.Type.Filled;
                progress.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
                Stretch(progress.rectTransform, 4);
                progress.transform.SetAsFirstSibling();
                Set(view, "progress", progress);
            }
        });
        Edit(HudPath, Header);
        Edit(HudPath, Skill);
        Edit(Parts + "UICustomerOrder.prefab", root => Set(root.GetComponent<UICustomerOrder>(), "warning", Sound(root, true)));
        Edit(HudPath, root =>
        {
            var skill = root.GetComponentInChildren<UIDaySkillSlot>(true);
            Set(skill, "readySound", Sound(skill.gameObject, false));
        });
        AssetDatabase.SaveAssets();
        return "낮 상단 띠 · 재료 배지 · 손님 말풍선 · 월드 조리 진행 바 연결";
    }
    public static int ApplyScene()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("플레이 종료 후 적용한다.");
        var count = 0;
        foreach (var facility in Object.FindObjectsByType<SharedFacility>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (facility.Kind != FacilityKind.Beans && facility.Kind != FacilityKind.Bread) continue;
            if (facility.GetComponentInChildren<UIIngredientBadge>(true) != null) continue;
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Parts + "UIIngredientBadge.prefab"), facility.transform);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(facility.gameObject.scene);
            count++;
        }
        return count;
    }
    static AudioSource Sound(GameObject root, bool spatial)
    {
        var source = root.GetComponent<AudioSource>();
        if (source == null) source = root.AddComponent<AudioSource>();
        source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/sfx_bell.wav");
        source.playOnAwake = false;
        source.spatialBlend = spatial ? 1f : 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        // ponytail: 기존 종소리로 피드백을 연결한다. 전용 효과음이 준비되면 프리팹에서 교체한다.
        source.volume = 0.35f;
        source.minDistance = 3f;
        source.maxDistance = 40f;
        return source;
    }
    static void MakeIcon()
    {
        var path = Parts + "UIDayItemIcon.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = Rect("UIDayItemIcon", null);
        try
        {
            var layout = root.gameObject.AddComponent<LayoutElement>(); layout.preferredWidth = 64; layout.preferredHeight = 64;
            var part = root.gameObject.AddComponent<UIDayItemIcon>();
            var edge = Image(root, "Border", Theme.Gold); Stretch(edge.rectTransform);
            var back = Image(root, "Back", Theme.Panel); Stretch(back.rectTransform, 3);
            var icon = Image(root, "Icon", Color.white); Stretch(icon.rectTransform, 8); icon.preserveAspect = true;
            var count = Text(root, "Count", 20); count.alignment = TextAlignmentOptions.BottomRight; Stretch(count.rectTransform);
            Set(part, "icon", icon); Set(part, "border", edge); Set(part, "count", count);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
        }
        finally { Object.DestroyImmediate(root.gameObject); }
    }
    static void MakeBadge()
    {
        var path = Parts + "UIIngredientBadge.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = World("UIIngredientBadge", new Vector2(80, 90), 1.7f);
        try
        {
            var view = root.gameObject.AddComponent<UIIngredientBadge>();
            var icon = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Parts + "UIDayItemIcon.prefab"), root);
            Stretch((RectTransform)icon.transform);
            Set(view, "canvas", root.GetComponent<Canvas>()); Set(view, "icon", icon.GetComponent<UIDayItemIcon>());
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
        }
        finally { Object.DestroyImmediate(root.gameObject); }
    }
    static void MakeOrder()
    {
        var path = Parts + "UICustomerOrder.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = World("UICustomerOrder", new Vector2(310, 100), 2.2f);
        try
        {
            var view = root.gameObject.AddComponent<UICustomerOrder>();
            var back = Image(root, "Back", Theme.Panel); Stretch(back.rectTransform);
            var ring = Ring(root, "Patience"); Stretch(ring.rectTransform);
            var row = Rect("Icons", root); Stretch(row, 12);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 6; layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            Set(view, "canvas", root.GetComponent<Canvas>()); Set(view, "patience", ring); Set(view, "icons", row); Set(view, "theme", Theme);
            Set(view, "iconPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Parts + "UIDayItemIcon.prefab").GetComponent<UIDayItemIcon>());
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
        }
        finally { Object.DestroyImmediate(root.gameObject); }
    }
    static void Skill(GameObject root)
    {
        if (root.GetComponentInChildren<UIDaySkillSlot>(true) != null) return;
        var rect = Rect("SkillSlot", root.transform);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(24, 24); rect.sizeDelta = new Vector2(100, 128);
        rect.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
        var view = rect.gameObject.AddComponent<UIDaySkillSlot>();
        var circle = AssetDatabase.LoadAssetAtPath<Sprite>(CircleSprite);
        var back = Image(rect, "Back", Theme.Panel); Circle(back, circle);
        var fill = Image(rect, "Cooldown", Color.white); Circle(fill, circle);
        fill.type = UnityEngine.UI.Image.Type.Filled;
        fill.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical;
        fill.fillOrigin = (int)UnityEngine.UI.Image.OriginVertical.Bottom;
        var count = Text(rect, "Count", 40); Circle(count.rectTransform); count.alignment = TextAlignmentOptions.Center;
        var key = Text(rect, "Key", 20); key.alignment = TextAlignmentOptions.Bottom;
        key.rectTransform.anchorMin = Vector2.zero; key.rectTransform.anchorMax = new Vector2(1, 0);
        key.rectTransform.pivot = new Vector2(.5f, 0); key.rectTransform.sizeDelta = new Vector2(0, 24);
        Set(view, "cooldown", fill); Set(view, "count", count); Set(view, "key", key);
    }
    const string CircleSprite = "Assets/Art/UI/Sprites/Ingame/Circle.png";
    /// 슬롯 위쪽 100×100 정원 자리. 아래 28은 키 글자 몫이다.
    static void Circle(RectTransform rect)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1);
        rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(100, 100);
    }
    static void Circle(UnityEngine.UI.Image image, Sprite sprite)
    {
        image.sprite = sprite; image.raycastTarget = false; Circle(image.rectTransform);
    }
    static void Header(GameObject root)
    {
        var view = root.GetComponent<UIMatchHudScreen>();
        if (root.transform.Find("DayHeader") != null) return;
        var row = Rect("DayHeader", root.transform);
        row.anchorMin = new Vector2(0, 1); row.anchorMax = Vector2.one; row.pivot = new Vector2(.5f, 1);
        row.sizeDelta = new Vector2(0, 64); row.anchoredPosition = Vector2.zero;
        row.gameObject.AddComponent<UnityEngine.UI.Image>().color = Theme.Panel;
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.padding = new RectOffset(24, 24, 8, 8); layout.spacing = 16;
        layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true;
        var ranking = Text(row, "Ranking", 22); ranking.alignment = TextAlignmentOptions.MidlineLeft;
        var clock = Text(row, "Clock", 26); clock.alignment = TextAlignmentOptions.Center;
        var revenue = Text(row, "Revenue", 22); revenue.alignment = TextAlignmentOptions.MidlineRight;
        foreach (var t in new[] { ranking, clock, revenue }) { var e = t.gameObject.AddComponent<LayoutElement>(); e.flexibleWidth = 1; e.minWidth = 200; }
        Set(view, "dayHeader", row.gameObject); Set(view, "dayRanking", ranking); Set(view, "dayClock", clock); Set(view, "dayRevenue", revenue);
        var data = new SerializedObject(view); var night = data.FindProperty("nightHeader");
        var names = new[] { "TopLeft", "TopCenter", "Team", "Details" }; night.arraySize = names.Length;
        for (var i = 0; i < names.Length; i++) night.GetArrayElementAtIndex(i).objectReferenceValue = root.transform.Find(names[i]).gameObject;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    static void Edit(string path, System.Action<GameObject> action)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { action(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static RectTransform Rect(string name, Transform parent)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform; rect.SetParent(parent, false); return rect;
    }
    static RectTransform World(string name, Vector2 size, float height)
    {
        var root = Rect(name, null); root.sizeDelta = size; root.localScale = Vector3.one * .008f;
        root.localPosition = Vector3.up * height; root.gameObject.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace; return root;
    }
    // 줄어드는 링 (기획서 5.7.1). 시작 위치와 방향은 만든 뒤 프리팹에서 바꿀 수 있다.
    static UnityEngine.UI.Image Ring(Transform parent, string name)
    {
        var ring = Image(parent, name, Color.white);
        ring.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Sprites/Ingame/Ring.png");
        ring.type = UnityEngine.UI.Image.Type.Filled;
        ring.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
        ring.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
        ring.fillClockwise = true;
        ring.raycastTarget = false;
        return ring;
    }
    static UnityEngine.UI.Image Image(Transform parent, string name, Color color)
    {
        var image = Rect(name, parent).gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false; return image;
    }
    static TMP_Text Text(Transform parent, string name, float size)
    {
        var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>(); text.font = Font; text.fontSize = size;
        text.color = Theme.Cream; text.raycastTarget = false; text.text = string.Empty; return text;
    }
    static void Stretch(RectTransform rect, float inset = 0)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.one * inset; rect.offsetMax = -Vector2.one * inset;
    }
    static void Set(Object target, string field, Object value)
    {
        var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
    }
}
