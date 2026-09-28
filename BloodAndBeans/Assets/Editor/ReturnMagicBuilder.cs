using System;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.Rendering;

/// 바닥 문양 텍스처와 입체 리본을 귀환·전송 프리팹에 연결한다.
public static class ReturnMagicBuilder
{
    const string Folder = "Assets/Art/VFX/";
    const string ZonePath = "Assets/Art/Environment/Prefabs/ReturnZone.prefab";
    const string PlayerPath = "Assets/Art/Character/Prefabs/Player.prefab";
    const string ShaderName = "BB/ReturnMagic";
    const string CircleName = "ReturnMagicCircle";
    const string ReferencePath = "Assets/AssetStore/Hovl Studio/Magic effects pack/Prefabs/Magic circles/Magic circle 2.prefab";
    const string TrailReferencePath = "Assets/AssetStore/Hovl Studio/Magic effects pack/Prefabs/Character auras/Love aura.prefab";

    // ponytail: 참고 이미지에서 잡은 아트 초깃값. 아트 확정 뒤 생성된 프리팹·머티리얼에서 조정한다.
    [MenuItem("Blood & Beans/귀환 마법진과 전송 이펙트 만들기")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("플레이를 멈춘 뒤 생성하세요.");
        var shader = Shader.Find(ShaderName);
        if (shader == null) throw new InvalidOperationException(ShaderName + " 셰이더를 먼저 임포트하세요.");
        var glow = Material("ReturnMagicGlow", shader, new Color(2.8f, 0.6f, 4.2f, 1f), 0f);
        var ghost = Material("TeleportAfterimage", shader, new Color(1.8f, 0.7f, 2.6f, 0.7f), 1f);
        var mist = Material("ReturnMagicMist", shader, new Color(0.8f, 0.24f, 1.65f, 0.45f), 0f);
        mist.SetFloat("_Flow", 1f);
        var circle = GroundPlane();
        var star = ImportedMesh("TeleportStar");
        BuildZone(circle, star, glow, mist);
        BuildTeleport("TeleportDeparture", star, glow, mist, false);
        BuildTeleport("TeleportArrival", star, glow, mist, true);

        var player = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            var so = new SerializedObject(player.GetComponent<PlayerVisuals>());
            so.FindProperty("teleportAfterimageMaterial").objectReferenceValue = ghost;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
        RegisterEffects();
        AssetDatabase.SaveAssets();
    }

    static Material Material(string name, Shader shader, Color color, float rim)
    {
        var path = Folder + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Rim", rim);
        mat.SetFloat("_Opacity", 1f);
        mat.SetFloat("_Pulse", rim > 0 ? 0f : 0.16f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void BuildZone(Mesh mesh, Mesh star, Material material, Material mist)
    {
        var root = PrefabUtility.LoadPrefabContents(ZonePath);
        try
        {
            // 기존 트리거 크기와 네트워크 루트는 보존하고 불투명 판만 숨긴다.
            root.GetComponent<MeshRenderer>().enabled = false;
            var old = root.transform.Find(CircleName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var circle = new GameObject(CircleName);
            circle.transform.SetParent(root.transform, false);
            circle.transform.localPosition = new Vector3(0f, -0.46f, 0f);
            var radius = root.GetComponent<ReturnZone>().Radius;
            var scale = root.transform.lossyScale;
            circle.transform.localScale = new Vector3(radius / scale.x, 1f / scale.y, radius / scale.z);
            circle.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = circle.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GroundMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var motes = Particles(circle.transform, "RisingRunes", star, material, true, 3f, 0.04f, 0.12f);
            var main = motes.main;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startRotation3D = true; main.startRotationX = -Mathf.PI / 2f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            var emission = motes.emission; emission.rateOverTime = 7f;
            var shape = motes.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.96f; shape.radiusThickness = 0.12f; shape.rotation = new Vector3(90f, 0f, 0f);
            var velocity = motes.velocityOverLifetime; velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local; velocity.y = 0.3f;
            // 얇은 기둥처럼 올라가는 잔광. 같은 메시를 낮은 밀도로 재사용한다.
            var wisps = Particles(circle.transform, "RuneWisps", ImportedMesh("TeleportMist"), mist, true, 4f, 0.35f, 0.7f);
            main = wisps.main; main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startRotation3D = true; main.startRotationY = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            emission = wisps.emission; emission.rateOverTime = 1.25f;
            shape = wisps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.8f; shape.radiusThickness = 0.15f; shape.rotation = new Vector3(90,0,0);
            var wispRenderer = wisps.GetComponent<ParticleSystemRenderer>();
            wispRenderer.alignment = ParticleSystemRenderSpace.Local;
            AddReferenceLayers(circle.transform);
            var visuals = root.GetComponent<ReturnZoneVisuals>();
            if (visuals == null) visuals = root.AddComponent<ReturnZoneVisuals>();
            var so = new SerializedObject(visuals);
            so.FindProperty("zone").objectReferenceValue = root.GetComponent<ReturnZone>();
            so.FindProperty("circle").objectReferenceValue = circle.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, ZonePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void BuildTeleport(string name, Mesh star, Material material, Material mist, bool arrival)
    {
        var root = new GameObject(name);
        try
        {
            var system = root.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.duration = 2f; main.loop = false; main.playOnAwake = false;
            main.startLifetime = 2f; main.startSpeed = 0f; main.maxParticles = 1;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;

            var stars = Particles(root.transform, "StarBurst", star, material, false, 1.5f, 0.045f, 0.16f);
            emission = stars.emission;
            emission.rateOverTime = 0f; emission.SetBursts(new[] { new ParticleSystem.Burst(0.08f, 22) });
            shape = stars.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f; shape.radius = 0.65f; shape.rotation = new Vector3(-90f, 0f, 0f);
            main = stars.main; main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.8f);
            // 별 메시는 바닥(XZ)에 구워져 있다. X로 90° 세워 카메라(View 정렬)를 향하게 한다.
            main.startRotation3D = true; main.startRotationX = -90f * Mathf.Deg2Rad;

            AddTeleportTrail(root.transform, arrival);

            var veil = Particles(root.transform, "SpiralVeil", ImportedMesh("TeleportMist"), mist, false, 1.8f, 1f, 1f);
            veil.GetComponent<ParticleSystemRenderer>().alignment = ParticleSystemRenderSpace.Local;
            emission = veil.emission; emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var rotation = veil.rotationOverLifetime; rotation.enabled = true; rotation.separateAxes = true;
            rotation.x = 0; rotation.y = arrival ? -1.3f : 1.3f; rotation.z = 0;

            var wave = Particles(root.transform, "GroundWave", ImportedMesh("TeleportShockwave"), material, false, 0.85f, 1f, 1f);
            wave.transform.localPosition = new Vector3(0,0.04f,0);
            wave.GetComponent<ParticleSystemRenderer>().alignment = ParticleSystemRenderSpace.Local;
            emission = wave.emission; emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var size = wave.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0,arrival ? 1.6f : .25f,1,arrival ? .25f : 1.6f));

            var dust = Particles(root.transform,"Stardust",star,material,false,1.9f,0.014f,0.048f);
            emission = dust.emission; emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.12f, 64) });
            shape = dust.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .65f;
            main = dust.main; main.startSpeed = new ParticleSystem.MinMaxCurve(.15f,.65f);
            main.startRotation3D = true; main.startRotationX = -Mathf.PI/2;
            var velocity = dust.velocityOverLifetime; velocity.enabled = true; velocity.y = .65f;
            PrefabUtility.SaveAsPrefabAsset(root, Folder + name + ".prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    public static void ApplyTeleportTrails()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("플레이를 멈춘 뒤 적용하세요.");
        foreach (var name in new[] { "TeleportDeparture", "TeleportArrival" })
        {
            var path = Folder + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var oldName in new[] { "Spiral", "TeleportTrails" })
                {
                    var old = root.transform.Find(oldName);
                    if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                }
                AddTeleportTrail(root.transform, name == "TeleportArrival");
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
    }

    static void AddTeleportTrail(Transform root, bool arrival)
    {
        var reference = AssetDatabase.LoadAssetAtPath<GameObject>(TrailReferencePath);
        if (reference == null || reference.transform.Find("Trails") == null)
            throw new InvalidOperationException("Love aura의 Trails를 찾을 수 없다.");
        var go = UnityEngine.Object.Instantiate(reference.transform.Find("Trails").gameObject, root, false);
        go.name = "TeleportTrails";
        go.transform.localPosition = new Vector3(0, arrival ? 2.5f : .1f, 0);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        var ps = go.GetComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false; main.prewarm = false; main.playOnAwake = false;
        main.duration = 1.5f; main.startLifetime = 1.35f; main.startSpeed = 0;
        main.startDelay = 0; main.maxParticles = 3;
        main.startSize = .12f; main.startColor = Color.white;
        var emission = ps.emission; emission.rateOverTime = 0; emission.rateOverDistance = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0, 3) });
        var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = .7f; shape.radiusThickness = 0; shape.rotation = new Vector3(90,0,0);
        shape.arc = 360; shape.arcMode = ParticleSystemShapeMultiModeValue.BurstSpread;
        var velocity = ps.velocityOverLifetime; velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = 0; velocity.z = 0; velocity.y = arrival ? -1.7f : 1.7f;
        velocity.orbitalX = 0; velocity.orbitalZ = 0; velocity.orbitalY = arrival ? -3f : 3f;
        velocity.radial = 0;
        var color = ps.colorOverLifetime; color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white,0), new GradientColorKey(new Color(.65f,.4f,1),1) },
            new[] { new GradientAlphaKey(0,0), new GradientAlphaKey(1,.15f), new GradientAlphaKey(1,.65f), new GradientAlphaKey(0,1) });
        color.color = gradient;
        var trails = ps.trails; trails.enabled = true; trails.ratio = 1;
        trails.lifetime = .5f; trails.dieWithParticles = true; trails.worldSpace = false;
        trails.minVertexDistance = .025f; trails.inheritParticleColor = true;
        trails.colorOverLifetime = Color.white; trails.colorOverTrail = Color.white;
        trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0,1,1,0));
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        var source = renderer.trailMaterial;
        var material = Material("TeleportTrail", Shader.Find(ShaderName), new Color(2.8f,.85f,4f,1), 0);
        material.SetTexture("_MainTex", source.GetTexture("_MainTex"));
        renderer.trailMaterial = material; renderer.renderMode = ParticleSystemRenderMode.None;
    }

    public static void ApplyGroundTexture()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("플레이를 멈춘 뒤 적용하세요.");
        var root = PrefabUtility.LoadPrefabContents(ZonePath);
        try
        {
            var circle = root.transform.Find(CircleName);
            if (circle == null) throw new InvalidOperationException("기존 마법진을 먼저 생성하세요.");
            circle.GetComponent<MeshFilter>().sharedMesh = GroundPlane();
            circle.GetComponent<MeshRenderer>().sharedMaterial = GroundMaterial();
            PrefabUtility.SaveAsPrefabAsset(root, ZonePath);
            AssetDatabase.SaveAssets();
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static Mesh GroundPlane()
    {
        var path = Folder + "ReturnMagicPlane.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;
        // 베이크 카메라 폭 2.1과 일치시켜 기존 문양의 반지름을 보존한다.
        mesh = new Mesh { name = "ReturnMagicPlane" };
        mesh.vertices = new[] { new Vector3(-1.05f,0,-1.05f), new Vector3(-1.05f,0,1.05f),
            new Vector3(1.05f,0,1.05f), new Vector3(1.05f,0,-1.05f) };
        mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
        mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        mesh.triangles = new[] { 0,1,2,0,2,3 };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static Material GroundMaterial()
    {
        var path = Folder + "ReturnMagicCircle.png";
        AssetDatabase.ImportAsset(path);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("마법진 PNG를 먼저 베이크하세요.");
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        var material = Material("ReturnMagicGround", Shader.Find(ShaderName), new Color(2.8f,.6f,4.2f,1), 0);
        material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        return material;
    }

    static void AddReferenceLayers(Transform circle)
    {
        var reference = AssetDatabase.LoadAssetAtPath<GameObject>(ReferencePath);
        if (reference == null) throw new InvalidOperationException("Magic circle 2 프리팹이 없다.");
        foreach (var name in new[] { "Sparks", "Sides" })
        {
            var source = reference.transform.Find(name);
            if (source == null) throw new InvalidOperationException("Magic circle 2 파츠가 없다: " + name);
            var layer = UnityEngine.Object.Instantiate(source.gameObject, circle, false);
            bool beam = name == "Sparks";
            layer.name = beam ? "AscendingLightStreaks" : "PerimeterLightVeil";
            layer.transform.localPosition = new Vector3(0, 0.03f, 0);
            layer.transform.localScale = beam ? Vector3.one : new Vector3(.48f, 1f, .48f);
            var ps = layer.GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = true; main.prewarm = true;
            main.maxParticles = beam ? 64 : 8;
            main.startColor = beam ? new Color(.78f,.42f,1f,.85f) : new Color(.65f,.3f,1f,.3f);
            main.startLifetime = beam ? 1.8f : 2.6f;
            main.scalingMode = beam ? ParticleSystemScalingMode.Shape : ParticleSystemScalingMode.Hierarchy;
            var emission = ps.emission; emission.rateOverTime = beam ? 14f : .65f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            var renderer = layer.GetComponent<ParticleSystemRenderer>();
            var sourceMaterial = renderer.sharedMaterial;
            var material = Material(beam ? "ReturnMagicStreaks" : "ReturnMagicVeil", Shader.Find(ShaderName),
                beam ? new Color(2.4f,1.3f,3.4f,1f) : new Color(1.3f,.6f,2f,1f), 0);
            material.SetTexture("_MainTex", sourceMaterial.GetTexture("_MainTex"));
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (beam)
            {
                main.startSpeed = new ParticleSystem.MinMaxCurve(.65f,1.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(.025f,.06f);
                var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone;
                shape.radius = .86f; shape.radiusThickness = .25f; shape.angle = 3f;
                shape.rotation = new Vector3(-90,0,0);
                renderer.lengthScale = 7f; renderer.velocityScale = .25f;
            }
        }
    }

    static ParticleSystem Particles(Transform parent, string name, Mesh mesh, Material material,
        bool loop, float lifetime, float minSize, float maxSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = loop; main.duration = 2f; main.playOnAwake = loop;
        main.startLifetime = lifetime; main.startSpeed = 0f; main.maxParticles = 128;
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var shape = ps.shape; shape.enabled = false;
        var color = ps.colorOverLifetime; color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.65f, 0.4f, 1f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = mesh;
        renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return ps;
    }

    static void RegisterEffects()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) throw new InvalidOperationException("어드레서블 설정이 없다.");
        var managerPath = Folder + nameof(EffectManager) + ".prefab";
        var root = PrefabUtility.LoadPrefabContents(managerPath);
        try
        {
            var so = new SerializedObject(root.GetComponent<EffectManager>());
            var entries = so.FindProperty("effects");
            foreach (var id in new[] { EffectId.TeleportDeparture, EffectId.TeleportArrival })
            {
                var guid = AssetDatabase.AssetPathToGUID(Folder + id + ".prefab");
                var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
                entry.address = id.ToString();
                SerializedProperty row = null;
                for (var i = 0; i < entries.arraySize; i++)
                    if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("id").intValue == (int)id)
                        row = entries.GetArrayElementAtIndex(i);
                if (row == null) { entries.arraySize++; row = entries.GetArrayElementAtIndex(entries.arraySize - 1); }
                row.FindPropertyRelative("id").intValue = (int)id;
                row.FindPropertyRelative("prefab.m_AssetGUID").stringValue = guid;
                row.FindPropertyRelative("offset").vector3Value = new Vector3(0f, -0.8f, 0f);
                row.FindPropertyRelative("common").boolValue = true;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, managerPath);
            EditorUtility.SetDirty(settings);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static Mesh ImportedMesh(string name)
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(Folder + "ReturnMagic.fbx"))
            if (asset is Mesh mesh && mesh.name == name) return mesh;
        throw new InvalidOperationException("Blender FBX에 메시가 없다: " + name);
    }
}

