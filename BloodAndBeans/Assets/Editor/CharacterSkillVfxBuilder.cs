using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.Rendering;

/// 캐릭터 스킬 텍스처로 파티클 프리팹을 만들고 연출 표에 등록한다.
public static class CharacterSkillVfxBuilder
{
    const string Root = "Assets/Art/VFX/Skills/";
    const string TextureFolder = Root + "Textures/";
    const string MaterialFolder = Root + "Materials/";
    const string PrefabFolder = Root + "Prefabs/";
    const string ManagerPath = "Assets/Art/VFX/Shared/Prefabs/EffectManager.prefab";
    const string ShaderName = "Universal Render Pipeline/Particles/Unlit";
    const string RendererPath = "Assets/Settings/PC_Renderer.asset";
    const string PlayerPath = "Assets/Art/Character/Prefabs/Player.prefab";
    const string BagPath = "Assets/Art/Environment/Prefabs/BuriedBag.prefab";

    static readonly string[] TextureNames =
    {
        "SkillDust", "SkillGreenStink", "SkillShockRing", "SkillPoofSheet", "SkillBatSheet",
        "SkillGrabDust", "SkillScentWisp", "SkillPuddle", "SkillDizzyStar"
    };

    static readonly string[] GlowTextures = { "SkillShockRing", "SkillScentWisp" };

    static readonly EffectRegistration[] Registrations =
    {
        new(EffectId.CoffinImpact, new Vector3(0f, 0.05f, 0f)),
        new(EffectId.ZombieWalk, new Vector3(0f, 1.6f, 0f)),
        new(EffectId.Howl, new Vector3(0f, 0.3f, 0f)),
        new(EffectId.FrogCurse, new Vector3(0f, 0.8f, 0f)),
        new(EffectId.BatSwarm, new Vector3(0f, 1.5f, 0f)),
        new(EffectId.ArmGrab, new Vector3(0f, 0.05f, 0f)),
        new(EffectId.TrackScent, new Vector3(0f, 0.5f, 0f)),
        new(EffectId.PotionPuddle, new Vector3(0f, 0.03f, 0f)),
        new(EffectId.Dizzy, new Vector3(0f, 1.7f, 0f))
    };

    // ponytail: 아트 초깃값. 확정 뒤 생성된 프리팹에서 조정한다.
    [MenuItem("Blood & Beans/캐릭터 스킬 이펙트 만들기")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("플레이를 멈춘 뒤 생성하세요.");
        ValidateInputs();
        EnsureFolder(MaterialFolder.TrimEnd('/'));
        EnsureFolder(PrefabFolder.TrimEnd('/'));

        var shader = Shader.Find(ShaderName);
        if (shader == null) throw new InvalidOperationException(ShaderName + " 셰이더를 찾을 수 없다.");
        var materials = ImportAndBuildMaterials(shader);

        BuildCoffinImpact(materials["SkillDust"]);
        BuildZombieWalk(materials["SkillGreenStink"]);
        BuildHowl(materials["SkillShockRing"]);
        BuildFrogCurse(materials["SkillPoofSheet"]);
        BuildBatSwarm(materials["SkillBatSheet"]);
        BuildArmGrab(materials["SkillGrabDust"]);
        BuildTrackScent(materials["SkillScentWisp"]);
        BuildPotionPuddle(materials["SkillPuddle"]);
        BuildDizzy(materials["SkillDizzyStar"]);

        RegisterEffects();
        AssetDatabase.SaveAssets();
    }

    static void ValidateInputs()
    {
        foreach (var name in TextureNames)
        {
            var path = TextureFolder + name + ".png";
            if (!File.Exists(path)) throw new InvalidOperationException("스킬 VFX 텍스처가 없다: " + path);
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(ManagerPath) == null)
            throw new InvalidOperationException("EffectManager 프리팹이 없다: " + ManagerPath);
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
            throw new InvalidOperationException("어드레서블 설정이 없다.");
        if (settings.DefaultGroup == null)
            throw new InvalidOperationException("어드레서블 기본 그룹이 없다.");
    }

    static Dictionary<string, Material> ImportAndBuildMaterials(Shader shader)
    {
        var materials = new Dictionary<string, Material>();
        foreach (var name in TextureNames)
        {
            var texturePath = TextureFolder + name + ".png";
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("텍스처 임포터를 찾을 수 없다: " + texturePath);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null) throw new InvalidOperationException("텍스처를 불러올 수 없다: " + texturePath);
            materials[name] = Material(name, shader, texture);
        }
        return materials;
    }

    static Material Material(string name, Shader shader, Texture2D texture)
    {
        var path = MaterialFolder + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetColor("_Color", Color.white);
        // 빛(고리·냄새 줄기)만 더하기로 섞는다. 먼지·박쥐 같은 물체를 더하면 배경이 비쳐 테두리가 지저분해진다.
        var additive = Array.IndexOf(GlowTextures, name) >= 0;
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", additive ? 2f : 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = 3000;
        EditorUtility.SetDirty(material);
        return material;
    }

    static void BuildCoffinImpact(Material material)
    {
        BuildPrefab(EffectId.CoffinImpact, false, 0.6f, root =>
        {
            var ring = Particles(root, "DustRing", material, false, 0.6f, 0.48f, 0.18f, 0.35f);
            var main = ring.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.9f);
            main.gravityModifier = 0.15f;
            var emission = ring.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
            var shape = ring.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.9f;
            shape.radiusThickness = 0.15f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            Fade(ring);

            var chunks = Particles(root, "RisingClumps", material, false, 0.6f, 0.55f, 0.24f, 0.5f);
            main = chunks.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.gravityModifier = 0.65f;
            emission = chunks.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.02f, 10) });
            shape = chunks.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = 0.55f;
            shape.angle = 24f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            Fade(chunks);
        });
    }

    static void BuildZombieWalk(Material material)
    {
        BuildPrefab(EffectId.ZombieWalk, true, 1.2f, root =>
        {
            var smoke = Particles(root, "StinkWisps", material, true, 1.2f, 1.35f, 0.32f, 0.55f);
            var emission = smoke.emission;
            emission.rateOverTime = 2.2f;
            var shape = smoke.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.28f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var velocity = smoke.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.55f, 0.8f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            Fade(smoke);
        });
    }

    static void BuildHowl(Material material)
    {
        BuildPrefab(EffectId.Howl, false, 0.7f, root =>
        {
            var rings = Particles(root, "ShockRings", material, false, 0.7f, 0.4f, 1f, 1f,
                ParticleSystemRenderMode.HorizontalBillboard);
            var emission = rings.emission;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 1),
                new ParticleSystem.Burst(0.12f, 1),
                new ParticleSystem.Burst(0.24f, 1)
            });
            Size(rings, new AnimationCurve(new Keyframe(0f, 0.25f), new Keyframe(1f, 10f)));
            Fade(rings);
        });
    }

    static void BuildFrogCurse(Material material)
    {
        BuildPrefab(EffectId.FrogCurse, false, 0.5f, root =>
        {
            var poof = Particles(root, "Poof", material, false, 0.5f, 0.5f, 2.1f, 2.1f);
            var emission = poof.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            Flipbook(poof, 1, false);
            Fade(poof);

            var pieces = Particles(root, "PoofPieces", material, false, 0.5f, 0.42f, 0.22f, 0.42f);
            var main = pieces.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            main.gravityModifier = 0.25f;
            emission = pieces.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.06f, 6) });
            var shape = pieces.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = 0.18f;
            shape.angle = 38f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            Flipbook(pieces, 1, true);
            Fade(pieces);
        });
    }

    static void BuildBatSwarm(Material material)
    {
        BuildPrefab(EffectId.BatSwarm, true, 1.5f, root =>
        {
            var bats = Particles(root, "OrbitingBats", material, true, 1.5f, 1.5f, 0.45f, 0.65f);
            var emission = bats.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 7) });
            var shape = bats.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.65f;
            var velocity = bats.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(1.1f, 1.7f);
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
            Flipbook(bats, 2, true);
            Fade(bats);
        });
    }

    static void BuildArmGrab(Material material)
    {
        BuildPrefab(EffectId.ArmGrab, true, 0.35f, root =>
        {
            var dust = Particles(root, "StruggleDust", material, true, 0.35f, 0.34f, 0.16f, 0.32f);
            var main = dust.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.85f);
            main.gravityModifier = 0.45f;
            var emission = dust.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 4) });
            var shape = dust.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.32f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            Fade(dust);
        });
    }

    static void BuildTrackScent(Material material)
    {
        BuildPrefab(EffectId.TrackScent, true, 1f, root =>
        {
            var scent = Particles(root, "ForwardScent", material, true, 1f, 0.8f, 0.22f, 0.38f);
            var main = scent.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 2f);
            main.startColor = new Color(1f, 0.78f, 0.32f, 0.8f);
            var emission = scent.emission;
            emission.rateOverTime = 10f;
            var shape = scent.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = 0.3f;
            shape.angle = 16f;
            Fade(scent);

            var rising = Particles(root, "RisingScent", material, true, 1f, 1.1f, 0.18f, 0.3f);
            main = rising.main;
            main.startColor = new Color(1f, 0.85f, 0.4f, 0.65f);
            emission = rising.emission;
            emission.rateOverTime = 4f;
            shape = rising.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.35f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var velocity = rising.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(0.5f, 0.85f);
            Fade(rising);
        });
    }

    /// 후각 연출만 갱신한다. 다른 스킬 프리팹의 아트 조정은 유지한다.
    [MenuItem("Blood & Beans/후각 연출 적용")]
    public static void BuildTrackSense()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("플레이를 멈춘 뒤 적용하세요.");
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "SkillScentWisp.mat");
        if (material == null) throw new InvalidOperationException("냄새 머티리얼이 없다.");
        BuildTrackScent(material);
        var manager = PrefabUtility.LoadPrefabContents(ManagerPath);
        try
        {
            var so = new SerializedObject(manager.GetComponent<EffectManager>());
            var entries = so.FindProperty("effects");
            for (var i = entries.arraySize - 1; i >= 0; i--)
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("id").intValue == (int)EffectId.TrackSniff)
                    entries.DeleteArrayElementAtIndex(i);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(manager, ManagerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(manager); }
        var feature = ConfigureTrackRenderer();
        ConfigureTrackPrefabs(feature);
    }

    static ScriptableObject ConfigureTrackRenderer()
    {
        // BB.Editor는 URP를 참조하지 않는다. 기존 Inspector 직렬화 API로 배선한다.
        var renderer = AssetDatabase.LoadAssetAtPath<ScriptableObject>(RendererPath);
        if (renderer == null) throw new InvalidOperationException("PC 렌더러가 없다.");
        var rendererSo = new SerializedObject(renderer);
        var features = rendererSo.FindProperty("m_RendererFeatures");
        UnityEngine.Object outline = null;
        ScriptableObject feature = null;
        for (var i = 0; i < features.arraySize; i++)
        {
            var f = features.GetArrayElementAtIndex(i).objectReferenceValue;
            if (f == null) continue;
            if (f.GetType().Name == nameof(OutlineFeature)) outline = f;
            if (f.GetType().Name == nameof(TrackSenseFeature)) feature = f as ScriptableObject;
        }
        if (outline == null) throw new InvalidOperationException("기존 OutlineFeature가 없다.");
        var mask = TrackOutlineMaterial("TrackScentMask", true);
        var edge = TrackOutlineMaterial("TrackScentOutline", false);
        var so = new SerializedObject(outline);
        so.FindProperty("scentMaskMaterial").objectReferenceValue = mask;
        so.FindProperty("scentOutlineMaterial").objectReferenceValue = edge;
        so.ApplyModifiedPropertiesWithoutUndo();

        var path = MaterialFolder + "TrackSense.mat";
        var wave = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (wave == null)
        {
            var shader = Shader.Find("BB/TrackSense");
            if (shader == null) throw new InvalidOperationException("후각 화면 셰이더가 없다.");
            wave = new Material(shader) { name = "TrackSense" };
            AssetDatabase.CreateAsset(wave, path);
        }
        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance(nameof(TrackSenseFeature));
            feature.name = "TrackSense";
            feature.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(feature, renderer);
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
        }
        var featureSo = new SerializedObject(feature);
        featureSo.FindProperty("passMaterial").objectReferenceValue = wave;
        var injection = featureSo.FindProperty("injectionPoint");
        injection.enumValueIndex = Array.IndexOf(injection.enumNames, "BeforeRenderingPostProcessing");
        featureSo.FindProperty("fetchColorBuffer").boolValue = true;
        featureSo.FindProperty("requirements").intValue = 0;
        featureSo.FindProperty("bindDepthStencilAttachment").boolValue = false;
        featureSo.FindProperty("m_Active").boolValue = true;
        featureSo.ApplyModifiedPropertiesWithoutUndo();
        var map = rendererSo.FindProperty("m_RendererFeatureMap");
        map.arraySize = features.arraySize;
        for (var i = 0; i < features.arraySize; i++)
        {
            var f = features.GetArrayElementAtIndex(i).objectReferenceValue;
            if (f != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(f, out string _, out long localId))
                map.GetArrayElementAtIndex(i).longValue = localId;
        }
        rendererSo.ApplyModifiedPropertiesWithoutUndo();
        renderer.GetType().GetMethod("SetDirty").Invoke(renderer, null);
        EditorUtility.SetDirty(feature);
        EditorUtility.SetDirty(renderer);
        AssetDatabase.SaveAssetIfDirty(renderer);
        return feature;
    }

    static Material TrackOutlineMaterial(string name, bool mask)
    {
        var path = MaterialFolder + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var shader = Shader.Find("BB/StationOutline");
            if (shader == null) throw new InvalidOperationException("스텐실 외곽선 셰이더가 없다.");
            material = new Material(shader) { name = name };
            material.SetColor("_OutlineColor", new Color(1.65f, 1.05f, 0.22f, 0.9f));
            material.SetFloat("_OutlineScale", mask ? 1f : 1.12f);
            AssetDatabase.CreateAsset(material, path);
        }
        // 설비(32), 다음 아이템(64)과 다른 스텐실 비트를 쓴다.
        material.SetFloat("_StencilRef", 128f);
        material.SetFloat("_StencilComp", (float)(mask ? CompareFunction.Always : CompareFunction.NotEqual));
        material.SetFloat("_StencilOp", (float)(mask ? StencilOp.Replace : StencilOp.Keep));
        material.SetFloat("_ColorWrite", mask ? 0f : 15f);
        material.SetFloat("_ZTest", (float)CompareFunction.Always);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }

    static void ConfigureTrackPrefabs(ScriptableObject feature)
    {
        var player = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            var visuals = player.GetComponent<PlayerVisuals>();
            if (visuals == null) throw new InvalidOperationException("플레이어 표현 컴포넌트가 없다.");
            var so = new SerializedObject(visuals);
            so.FindProperty("trackSense").objectReferenceValue = feature;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }

        var bagRoot = PrefabUtility.LoadPrefabContents(BagPath);
        try
        {
            var bag = bagRoot.GetComponent<BuriedBag>();
            var body = bagRoot.GetComponent<Renderer>();
            if (bag == null || body == null) throw new InvalidOperationException("가방 본체 참조가 없다.");
            var visuals = bagRoot.GetComponent<BagScentVisuals>();
            if (visuals == null) visuals = bagRoot.AddComponent<BagScentVisuals>();
            var so = new SerializedObject(visuals);
            so.FindProperty("bag").objectReferenceValue = bag;
            var bodies = so.FindProperty("body");
            bodies.arraySize = 1;
            bodies.GetArrayElementAtIndex(0).objectReferenceValue = body;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(bagRoot, BagPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(bagRoot); }
    }

    static void BuildPotionPuddle(Material material)
    {
        BuildPrefab(EffectId.PotionPuddle, true, 6f, root =>
        {
            var puddle = Particles(root, "Puddle", material, true, 6.1f, 6.2f, 8f, 8f,
                ParticleSystemRenderMode.HorizontalBillboard);
            var emission = puddle.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            Fade(puddle, 0.97f);

            var bubbles = Particles(root, "Bubbles", material, true, 1f, 0.9f, 0.1f, 0.26f);
            emission = bubbles.emission;
            emission.rateOverTime = 3f;
            var shape = bubbles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 2.7f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var velocity = bubbles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            Fade(bubbles);
        });
    }

    static void BuildDizzy(Material material)
    {
        BuildPrefab(EffectId.Dizzy, true, 0.8f, root =>
        {
            var stars = Particles(root, "OrbitingStars", material, true, 0.8f, 0.8f, 0.26f, 0.36f);
            var emission = stars.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 3) });
            var shape = stars.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.65f;
            shape.radiusThickness = 0f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var velocity = stars.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.orbitalY = 4f;
            Fade(stars);
        });
    }

    static void BuildPrefab(EffectId id, bool loop, float duration, Action<Transform> configure)
    {
        var root = new GameObject(id.ToString());
        try
        {
            var system = root.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.duration = duration;
            main.loop = loop;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.01f;
            main.startSpeed = 0f;
            main.startSize = 0f;
            main.maxParticles = 1;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            configure(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + id + ".prefab");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    static ParticleSystem Particles(Transform parent, string name, Material material, bool loop,
        float duration, float lifetime, float minSize, float maxSize,
        ParticleSystemRenderMode renderMode = ParticleSystemRenderMode.Billboard)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.duration = duration;
        main.loop = loop;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = lifetime;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startColor = Color.white;
        main.maxParticles = 128;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = system.emission;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 0f;
        var shape = system.shape;
        shape.enabled = false;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = renderMode;
        // 바닥에 눕는 판(웅덩이·하울링 고리)은 실제 반경이 크기라 화면 비율 상한에 잘리면 안 된다.
        if (renderMode == ParticleSystemRenderMode.HorizontalBillboard) renderer.maxParticleSize = 100f;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return system;
    }

    static void Flipbook(ParticleSystem system, int cycles, bool randomStart)
    {
        var sheet = system.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Grid;
        sheet.numTilesX = 4;
        sheet.numTilesY = 4;
        sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
        sheet.startFrame = randomStart ? new ParticleSystem.MinMaxCurve(0f, 1f) : 0f;
        sheet.cycleCount = cycles;
    }

    static void Size(ParticleSystem system, AnimationCurve curve)
    {
        var size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, curve);
    }

    static void Fade(ParticleSystem system, float fadeStart = 0.7f)
    {
        var color = system.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f),
                new GradientAlphaKey(1f, fadeStart), new GradientAlphaKey(0f, 1f)
            });
        color.color = gradient;
    }

    static void RegisterEffects()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) throw new InvalidOperationException("어드레서블 설정이 없다.");
        if (settings.DefaultGroup == null) throw new InvalidOperationException("어드레서블 기본 그룹이 없다.");
        var root = PrefabUtility.LoadPrefabContents(ManagerPath);
        try
        {
            var manager = root.GetComponent<EffectManager>();
            if (manager == null) throw new InvalidOperationException("EffectManager 컴포넌트가 없다: " + ManagerPath);
            var so = new SerializedObject(manager);
            var entries = so.FindProperty("effects");
            foreach (var registration in Registrations)
            {
                var prefabPath = PrefabFolder + registration.Id + ".prefab";
                var guid = AssetDatabase.AssetPathToGUID(prefabPath);
                if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("생성된 프리팹 GUID가 없다: " + prefabPath);
                var entry = settings.CreateOrMoveEntry(guid, AddressableGroups.Find(settings, AddressableGroups.Vfx));
                entry.address = AddressableGroups.VfxAddress + registration.Id;

                var rowIndex = -1;
                for (var i = 0; i < entries.arraySize; i++)
                {
                    var id = entries.GetArrayElementAtIndex(i).FindPropertyRelative("id").intValue;
                    if (id == (int)registration.Id && rowIndex < 0) rowIndex = i;
                }
                for (var i = entries.arraySize - 1; i > rowIndex && rowIndex >= 0; i--)
                {
                    var id = entries.GetArrayElementAtIndex(i).FindPropertyRelative("id").intValue;
                    if (id == (int)registration.Id) entries.DeleteArrayElementAtIndex(i);
                }
                if (rowIndex < 0)
                {
                    rowIndex = entries.arraySize;
                    entries.arraySize++;
                }

                var row = entries.GetArrayElementAtIndex(rowIndex);
                row.FindPropertyRelative("id").intValue = (int)registration.Id;
                var prefab = row.FindPropertyRelative("prefab");
                prefab.FindPropertyRelative("m_AssetGUID").stringValue = guid;
                var subObjectName = prefab.FindPropertyRelative("m_SubObjectName");
                if (subObjectName != null) subObjectName.stringValue = string.Empty;
                var subObjectType = prefab.FindPropertyRelative("m_SubObjectType");
                if (subObjectType != null) subObjectType.stringValue = string.Empty;
                row.FindPropertyRelative("offset").vector3Value = registration.Offset;
                row.FindPropertyRelative("common").boolValue = false;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, ManagerPath);
            EditorUtility.SetDirty(settings);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void EnsureFolder(string path)
    {
        if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent) && parent != "Assets") EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    readonly struct EffectRegistration
    {
        public readonly EffectId Id;
        public readonly Vector3 Offset;

        public EffectRegistration(EffectId id, Vector3 offset)
        {
            Id = id;
            Offset = offset;
        }
    }
}
