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
        AsSprite(DashIcon); AsSprite(RingSprite); MakeSkillSlot();
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
        source.outputAudioMixerGroup = SoundSetup.SfxGroup();
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
            var back = Image(root, "Back", Color.white); Stretch(back.rectTransform);
            back.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Sprites/Ingame/OrderBubble.png");
            back.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Environment/Materials/UIBubbleFill.mat");
            var fill = back.gameObject.AddComponent<UIBubbleFill>();
            var row = Rect("Icons", root); Stretch(row, 12);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 6; layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            Set(view, "canvas", root.GetComponent<Canvas>()); Set(view, "patience", fill); Set(view, "icons", row); Set(view, "theme", Theme);
            Set(view, "iconPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Parts + "UIDayItemIcon.prefab").GetComponent<UIDayItemIcon>());
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
        }
        finally { Object.DestroyImmediate(root.gameObject); }
    }
    static void Skill(GameObject root)
    {
        var view = root.GetComponentInChildren<UIDaySkillSlot>(true);
        if (view == null)
        {
            var rect = Rect("SkillSlot", root.transform);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(24, 24); rect.sizeDelta = new Vector2(100, 128);
            rect.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            view = rect.gameObject.AddComponent<UIDaySkillSlot>();
        }
        if (new SerializedObject(view).FindProperty("slot").objectReferenceValue == null)
        {
            // 예전 칸은 원·게이지·글자를 직접 들고 있었다. 파츠로 바꾼다.
            for (var i = view.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(view.transform.GetChild(i).gameObject);
            var part = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SkillSlotPart), view.transform);
            part.name = "Slot"; Stretch((RectTransform)part.transform);
            Set(view, "slot", part.GetComponent<UISkillSlot>());
        }
        SkillIcons(view);
    }
    /// 대시 칸을 스킬과 같은 원형 파츠로 바꾼다. 아이콘만 대시 그림으로 덮는다.
    static void Dash(GameObject root)
    {
        var view = root.GetComponent<UIMatchHudScreen>();
        var data = new SerializedObject(view);
        var slot = data.FindProperty("dashSlot");
        // 예전 칸은 같은 필드에 RectTransform으로 물려 있었다. 타입으로 판단한다.
        if (slot.objectReferenceValue is UISkillSlot) return;
        var old = root.transform.Find("BottomLeft/DashSlot");
        var part = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SkillSlotPart), old.parent);
        part.name = "DashSlot"; part.transform.SetSiblingIndex(old.GetSiblingIndex());
        var icon = (UnityEngine.UI.Image)new SerializedObject(part.GetComponent<UISkillSlot>()).FindProperty("icon").objectReferenceValue;
        icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DashIcon);
        icon.enabled = true;
        part.SetActive(false);
        Object.DestroyImmediate(old.gameObject);
        slot.objectReferenceValue = part.GetComponent<UISkillSlot>();
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    public static void ApplySkillIcons()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("플레이 종료 후 적용한다.");
        Edit(HudPath, Skill);
        AssetDatabase.SaveAssets();
    }
    /// 원형 칸 파츠를 만들고 HUD의 스킬·대시 칸을 그 파츠로 바꾼다.
    public static string ApplySkillSlots()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("플레이 종료 후 적용한다.");
        AsSprite(DashIcon); AsSprite(RingSprite);
        MakeSkillSlot();
        Edit(HudPath, Skill);
        Edit(HudPath, Dash);
        AssetDatabase.SaveAssets();
        return "UISkillSlot 파츠 · 스킬 칸 · 대시 칸";
    }
    public static string ApplySkillDecor()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("플레이 종료 후 적용한다.");
        MakeSkillSlot();
        Edit(HudPath, root =>
        {
            var skill = root.GetComponentInChildren<UIDaySkillSlot>(true);
            var skillSlot = (UISkillSlot)new SerializedObject(skill).FindProperty("slot").objectReferenceValue;
            var dashSlot = (UISkillSlot)new SerializedObject(root.GetComponent<UIMatchHudScreen>()).FindProperty("dashSlot").objectReferenceValue;
            skillSlot.SetKey("Q");
            dashSlot.SetKey("Space");
        });
        AssetDatabase.SaveAssets();
        return "반투명 원형 배경 · 흰 테두리 · Q/Space 키 배지 · 준비 완료 VFX 연결";
    }
    public static string ApplySkillVfx()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("플레이 종료 후 적용한다.");
        MakeSkillReadyVfx();
        Edit(SkillReadyVfx, EnergyPulse);
        Edit(SkillSlotPart, ReadyVfx);
        AssetDatabase.SaveAssets();
        return "스킬·대시 공통 파츠에 준비 완료 Particle System VFX 연결";
    }
    static void AsSprite(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.textureType == TextureImporterType.Sprite) return;
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }
    /// 위는 정원, 아래 28은 키 글자 몫이다. 원 크기는 칸 폭을 따른다.
    static void MakeSkillSlot()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SkillSlotPart) != null)
        {
            Edit(SkillSlotPart, StyleSkillSlot);
            return;
        }
        var root = Rect("UISkillSlot", null);
        try
        {
            root.sizeDelta = new Vector2(100, 128);
            var layout = root.gameObject.AddComponent<LayoutElement>(); layout.preferredWidth = 100; layout.preferredHeight = 128;
            var part = root.gameObject.AddComponent<UISkillSlot>();
            var face = Rect("Circle", root);
            face.anchorMin = new Vector2(0, 1); face.anchorMax = Vector2.one; face.pivot = new Vector2(.5f, 1);
            face.anchoredPosition = Vector2.zero; face.sizeDelta = new Vector2(0, 100);
            var aspect = face.gameObject.AddComponent<AspectRatioFitter>(); aspect.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight; aspect.aspectRatio = 1;
            var circle = AssetDatabase.LoadAssetAtPath<Sprite>(CircleSprite);
            var back = Image(face, "Back", new Color(.06f, .05f, .04f, .55f)); back.sprite = circle; Stretch(back.rectTransform);
            var icon = Image(face, "Icon", Color.white); icon.preserveAspect = true; Stretch(icon.rectTransform, 10);
            icon.enabled = false; // 그림이 정해지기 전 흰 사각형이 보이지 않게 한다
            var fill = Image(face, "Cooldown", new Color(0, 0, 0, .6f)); fill.sprite = circle; Stretch(fill.rectTransform);
            fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            fill.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top; fill.fillClockwise = false; fill.fillAmount = 0;
            var ring = Image(face, "Ring", new Color(1, 1, 1, .6f)); ring.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RingSprite); Stretch(ring.rectTransform);
            var count = Text(face, "Count", 36); count.color = Color.white; count.fontStyle = FontStyles.Bold; count.alignment = TextAlignmentOptions.Center; Stretch(count.rectTransform);
            var key = Text(root, "Key", 20); key.fontStyle = FontStyles.UpperCase | FontStyles.Bold; key.alignment = TextAlignmentOptions.Bottom;
            key.rectTransform.anchorMin = Vector2.zero; key.rectTransform.anchorMax = new Vector2(1, 0);
            key.rectTransform.pivot = new Vector2(.5f, 0); key.rectTransform.sizeDelta = new Vector2(0, 24);
            Set(part, "icon", icon); Set(part, "cooldown", fill); Set(part, "ring", ring); Set(part, "count", count); Set(part, "key", key);
            StyleSkillSlot(root.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, SkillSlotPart);
        }
        finally { Object.DestroyImmediate(root.gameObject); }
    }
    static void StyleSkillSlot(GameObject root)
    {
        var part = root.GetComponent<UISkillSlot>();
        var face = root.transform.Find("Circle");
        var back = face.Find("Back").GetComponent<UnityEngine.UI.Image>();
        back.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CircleSprite);
        back.color = new Color(.2f, .2f, .2f, .55f);
        var ringTransform = face.Find("Ring");
        if (ringTransform != null)
        {
            var ring = ringTransform.GetComponent<UnityEngine.UI.Image>();
            ring.color = new Color(1, 1, 1, .95f);
            var border = ring.GetComponent<Outline>();
            if (border == null) border = ring.gameObject.AddComponent<Outline>();
            border.effectColor = Color.white; border.effectDistance = Vector2.one;
            var data = new SerializedObject(part);
            data.FindProperty("ringColor").colorValue = ring.color;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        ReadyVfx(root);

        var badgeTransform = root.transform.Find("KeyBadge");
        var badge = badgeTransform == null ? Image(root.transform, "KeyBadge", Color.white) : badgeTransform.GetComponent<UnityEngine.UI.Image>();
        badge.color = new Color(.1f, .1f, .12f, .8f);
        badge.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        badge.type = UnityEngine.UI.Image.Type.Sliced;
        badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = badge.rectTransform.pivot = new Vector2(.5f, 0);
        badge.rectTransform.anchoredPosition = Vector2.zero;
        var layout = badge.GetComponent<HorizontalLayoutGroup>();
        if (layout == null) layout = badge.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 2, 2); layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fitter = badge.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = badge.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var key = (TMP_Text)new SerializedObject(part).FindProperty("key").objectReferenceValue;
        key.transform.SetParent(badge.transform, false);
        key.fontStyle = FontStyles.Bold; key.alignment = TextAlignmentOptions.Center; key.color = Color.white;
        if (string.IsNullOrEmpty(key.text)) key.text = "Q";
    }
    static void ReadyVfx(GameObject root)
    {
        var face = root.transform.Find("Circle");
        var effect = face.Find("ReadyVfx");
        if (effect == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(MakeSkillReadyVfx(), face);
            instance.name = "ReadyVfx";
            effect = instance.transform;
            Stretch((RectTransform)effect, -40);
        }
        Set(root.GetComponent<UISkillSlot>(), "readyEffect", effect.GetComponent<ParticleSystem>());
        foreach (var name in new[] { "ReadyGlow", "ReadyBurst", "ReadyFlash" })
        {
            var obsolete = face.Find(name);
            if (obsolete != null) Object.DestroyImmediate(obsolete.gameObject);
        }
        effect.SetAsLastSibling();
    }
    static GameObject MakeSkillReadyVfx()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkillReadyVfx);
        if (prefab != null) return prefab;
        var root = Rect("SkillReadyVfx", null);
        try
        {
            // 연출값은 생성 후 VFX 프리팹의 Particle System 모듈에서 조정한다.
            root.sizeDelta = new Vector2(180, 180);
            EnergyPulse(root.gameObject);
            return PrefabUtility.SaveAsPrefabAsset(root.gameObject, SkillReadyVfx);
        }
        finally { Object.DestroyImmediate(root.gameObject); }
    }
    static void EnergyPulse(GameObject root)
    {
        var point = AssetDatabase.LoadAssetAtPath<Material>(ReadyPointMaterial);
        var circle = AssetDatabase.LoadAssetAtPath<Material>(ReadyCircleMaterial);
        if (point == null || circle == null) throw new System.InvalidOperationException("Point와 Circle 재질이 필요하다.");
        point = ReadyUIMaterial(point, ReadyPointUIMaterialPath);
        circle = ReadyUIMaterial(circle, ReadyCircleUIMaterialPath);
        // ponytail: 레퍼런스 기준 초기 연출값. 이후 아트 조정은 프리팹의 직렬화된 파티클 모듈에서 한다.
        var red = new Color(1, .035f, .065f, 1);
        var hot = new Color(1, .7f, .72f, 1);
        var core = ReadyParticles(root, "", point, .68f, 0, 76, 1);
        ReadySize(core, 1, .85f, .65f, .75f, 1.05f, 1.15f);
        ReadyTint(core, hot, Color.white, Color.white, 0, .35f, .9f, 1, .65f, 0);

        var halo = ReadyParticles(root, "ChargeHalo", point, .68f, 0, 170, 1);
        ReadySize(halo, 1.15f, 1, .75f, .55f, .85f, 1.1f);
        ReadyTint(halo, red, red, hot, 0, .15f, .4f, .7f, .35f, 0);

        var gather = ReadyParticles(root, "InwardEnergy", point, ReadyChargeSeconds, 0, 7, 18);
        var shape = gather.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 70; shape.radiusThickness = .12f;
        var motion = gather.velocityOverLifetime; motion.enabled = true;
        motion.space = ParticleSystemSimulationSpace.Local; motion.radial = -150; motion.orbitalZ = 1.3f;
        ReadySize(gather, .2f, .8f, 1, 1, .75f, .1f);
        ReadyTint(gather, red, hot, Color.white, 0, .8f, 1, 1, .8f, 0);

        var flash = ReadyParticles(root, "WhiteFlash", ReadyUIMaterial(point, ReadyFlashMaterialPath, ReadyFlashBrightness), .55f, ReadyChargeSeconds, 110, 1);
        ReadySize(flash, .06f, .18f, .45f, .82f, 1.22f, 1.6f);
        ReadyTint(flash, Color.white, Color.white, Color.white, 1, 1, .85f, .45f, .15f, 0);

        var wave = ReadyParticles(root, "CircleBurst", circle, .34f, ReadyChargeSeconds, 118, 1);
        ReadySize(wave, .3f, .7f, 1.05f, 1.3f, 1.45f, 1.6f);
        ReadyTint(wave, hot, red, red, 1, .9f, .65f, .35f, .12f, 0);

        var sparks = ReadyParticles(root, "OutwardSparks", point, .32f, ReadyChargeSeconds, 5, 12);
        var sparkShape = sparks.shape; sparkShape.enabled = true;
        sparkShape.shapeType = ParticleSystemShapeType.Circle; sparkShape.radius = 20;
        var sparkMain = sparks.main; sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(100, 180);
        ReadySize(sparks, 1, 1, .8f, .6f, .3f, 0);
        ReadyTint(sparks, hot, red, red, 1, 1, .8f, .5f, .2f, 0);

        var graphic = root.GetComponent<Coffee.UIExtensions.UIParticle>();
        if (graphic == null) graphic = root.AddComponent<Coffee.UIExtensions.UIParticle>();
        graphic.raycastTarget = false;
        graphic.autoScalingMode = Coffee.UIExtensions.UIParticle.AutoScalingMode.None;
        graphic.scale = ReadyParticleScale;
        graphic.useCustomView = true;
        graphic.customViewSize = 1000;
        // 흰 코어를 붉은 후광보다 나중에 그려 중앙이 어두워지지 않게 한다.
        graphic.particles.Clear();
        graphic.particles.AddRange(new[] { halo, gather, wave, sparks, core, flash });
        graphic.RefreshParticles(graphic.particles);
        foreach (var name in new[] { "Wave", "Sparks" })
        {
            var old = root.transform.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }
    }
    static Material ReadyUIMaterial(Material source, string path, float brightness = 1)
    {
        var shader = Shader.Find(ReadyUIShader);
        if (shader == null) throw new System.InvalidOperationException("UIParticle의 UI/Additive 셰이더가 필요하다.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { mainTexture = source.mainTexture };
            var tint = source.GetColor("_Color");
            tint.r *= brightness; tint.g *= brightness; tint.b *= brightness;
            material.SetColor("_Color", tint);
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            // 기존 아트 밝기는 보존하고, Overlay UI에서 쓸 수 없는 깊이 페이딩과 알파 합성만 제거한다.
            material.shader = shader;
            material.shaderKeywords = System.Array.Empty<string>();
            EditorUtility.SetDirty(material);
        }
        return material;
    }
    static ParticleSystem ReadyParticles(GameObject root, string name, Material material, float lifetime, float delay, float size, short count)
    {
        var layer = string.IsNullOrEmpty(name) ? root.transform : root.transform.Find(name);
        if (layer == null) layer = Rect(name, root.transform);
        var particles = layer.GetComponent<ParticleSystem>();
        if (particles == null) particles = layer.gameObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.loop = false; main.duration = lifetime; main.playOnAwake = false;
        main.startLifetime = lifetime; main.startDelay = delay; main.startSize = size; main.startSpeed = 0;
        main.startColor = Color.white; main.maxParticles = count;
        main.useUnscaledTime = true; main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var emission = particles.emission;
        emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, count) });
        var shape = particles.shape; shape.enabled = false;
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material; renderer.maxParticleSize = 1;
        return particles;
    }
    static void ReadySize(ParticleSystem particles, params float[] values)
    {
        var keys = new Keyframe[values.Length];
        for (var i = 0; i < values.Length; i++) keys[i] = new Keyframe((float)i / (values.Length - 1), values[i]);
        var size = particles.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(keys));
    }
    static void ReadyTint(ParticleSystem particles, Color begin, Color peak, Color end, params float[] alphas)
    {
        var alpha = new GradientAlphaKey[alphas.Length];
        for (var i = 0; i < alphas.Length; i++) alpha[i] = new GradientAlphaKey(alphas[i], (float)i / (alphas.Length - 1));
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(begin, 0), new GradientColorKey(peak, .65f), new GradientColorKey(end, 1) }, alpha);
        var color = particles.colorOverLifetime; color.enabled = true; color.color = gradient;
    }
    static void SkillIcons(UIDaySkillSlot view)
    {
        Set(view, "igniteIcon", AssetDatabase.LoadAssetAtPath<Sprite>(SkillSprites + "Skill_Dokkaebi_Day_Ignite.png"));
        Set(view, "glideIcon", AssetDatabase.LoadAssetAtPath<Sprite>(SkillSprites + "Skill_Bat_Day_Glide.png"));
        Set(view, "wispIcon", AssetDatabase.LoadAssetAtPath<Sprite>(SkillSprites + "Skill_Dokkaebi_Night_Wisp.png"));
        Set(view, "echoIcon", AssetDatabase.LoadAssetAtPath<Sprite>(SkillSprites + "Skill_Bat_Night_Echo.png"));
    }
    const string SkillSprites = "Assets/Art/UI/Sprites/Ingame/";
    const string CircleSprite = "Assets/Art/UI/Sprites/Ingame/Circle.png";
    const string RingSprite = "Assets/Art/UI/Sprites/Ingame/SkillRing.png";
    const string DashIcon = "Assets/Art/UI/Sprites/Ingame/Skill_Dash.png";
    const string SkillReadyVfx = "Assets/Art/VFX/Common/Prefabs/SkillReadyVfx.prefab";
    const string ReadyPointMaterial = "Assets/AssetStore/Hovl Studio/Magic effects pack/Materials/Point.mat";
    const string ReadyCircleMaterial = "Assets/AssetStore/Hovl Studio/Magic effects pack/Materials/Circle.mat";
    const string ReadyPointUIMaterialPath = "Assets/Art/VFX/Common/Materials/SkillReadyPoint.mat";
    const string ReadyCircleUIMaterialPath = "Assets/Art/VFX/Common/Materials/SkillReadyCircle.mat";
    const string ReadyFlashMaterialPath = "Assets/Art/VFX/Common/Materials/SkillReadyFlash.mat";
    const string ReadyUIShader = "UI/Additive";
    // ponytail: 요청에 맞춘 초기 HDR 배율. 생성 후 밝기는 섬광 재질의 Color에서 직접 조정한다.
    const float ReadyFlashBrightness = 4f;
    const float ReadyChargeSeconds = .44f;
    /// 파티클 크기를 캔버스 좌표 그대로 사용한다. 커스텀 베이크 뷰로 화면 크기 제한을 피한다.
    const float ReadyParticleScale = 1f;
    const string SkillSlotPart = Parts + "UISkillSlot.prefab";
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
        Set(view, "dayHeader", row.gameObject); Set(view, "dayClock", clock); Set(view, "dayRevenue", revenue);
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
