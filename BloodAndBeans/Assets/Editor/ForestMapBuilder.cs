using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;

/// 밤 숲의 실물을 만든다. 기획서 1.2(어둠의 숲) · 6.3(숲의 구조)를 화면에 세우는 도구다.
///
/// 배치 규칙은 코드가 아니라 `MatchDirector`가 가진다. 숲 크기·원점·스폰 들여쓰기를 여기서
/// 다시 적으면 맵을 넓힐 때 두 곳을 같이 고쳐야 하고, 한쪽만 고치면 팀이 지형 밖에서 시작한다.
/// 그래서 이 도구는 `SerializedObject`로 그 값을 읽어 쓴다.
///
/// 나무는 `NetworkObject`가 없는 순수 표현이라 씬에 그대로 굽는다. 상자는 씬 NetworkObject라
/// 위치를 건드리지 않는다 — 기획서 6.3이 "박스의 위치는 맵마다 고정"이라고 했고, 이미 놓인
/// 동심원 링이 그 고정 배치다. 이 도구는 그 자리에 등급 가중치와 겉모습만 채운다.
public static class ForestMapBuilder
{
    const string MenuPath = "Tools/Blood & Beans/숲 맵 생성";

    /// 같은 씬에서 몇 번을 돌려도 같은 숲이 나오게 하는 씨앗. 배치가 마음에 들지 않으면
    /// 이 값만 바꾼다.
    const int Seed = 20260828;

    const string ForestRootName = "Forest";
    const string GlowChildName = "Glow";
    const string BodyChildName = "Body";
    const string GroundName = "Ground";

    /// `ItemBoxView.bodyHeight` 기본값과 같아야 편집 화면과 플레이 화면의 상자 크기가 같다.
    const float BodyHeight = 0.9f;

    /// 지면 높이. 중력이 없어서(PlayerMove) 한번 어긋나면 스스로 내려오지 않는다.
    /// Kenney nature-kit 모델은 원점이 밑동이라 그대로 0에 놓는다.
    const float GroundY = 0f;

    const float BoxClearance = 3.5f;      // 상자 주변은 비운다. 나무가 상자를 가리면 못 찾는다
    const float SpawnClearance = 6f;    // 스폰 자리도 비운다. 시작하자마자 나무에 끼면 안 된다

    /// 바깥에서 중심으로 갈수록 빽빽해진다. 기획서 6.2-3: 중심부는 늦게 열리고 조우가
    /// 거기서 생긴다 — 화면에서도 안쪽이 더 답답해야 그 긴장이 읽힌다.
    const float OuterDensity = 0.42f;
    const float InnerDensity = 0.78f;

    /// 나무를 뿌릴 격자 간격. 이 칸마다 위 확률로 한 그루를 시도하고, 칸 안에서 흔든다.
    const float ScatterStep = 2.2f;

    /// Kenney nature-kit 나무는 원본이 1.7유닛이라 플레이어 캡슐(높이 2)보다 작다. 그대로
    /// 심으면 숲이 아니라 잔디밭이 된다. 이 배수로 6유닛 안팎이 되게 키운다.
    const float TreeScale = 3.5f;
    const float TreeScaleJitter = 0.35f;

    /// 바닥 풀은 Kenney 덩어리를 흩뿌리지 않는다. `ForestGrass`가 컴퓨트 셰이더로 위치를
    /// 뽑아 인디렉트 드로우 한 번에 그린다 - 덩어리 2,494개(33.9만 삼각형)를 대체한다.
    const string GrassComputePath = "Assets/Art/Shaders/ForestGrassPositions.compute";
    const string GrassMaterialPath = "Assets/Art/Materials/ForestGrassBlade.mat";

    /// 바닥 풀 설정. **도구가 소유한다.** 이 도구는 매번 `ForestGrass`를 새로 만들므로,
    /// Inspector에서 손으로 맞춘 값은 다음 실행에서 스크립트 기본값으로 되돌아간다.
    /// 값을 바꾸려면 여기를 고치고 도구를 다시 돌린다.
    ///
    /// 잎 하나의 마디 수. 원본 저장소는 5지만 그쪽은 지면에 붙은 시야 기준이다.
    /// 우리 탑다운에서는 마디가 보이지 않아 2로 낮춘다 - 잎당 삼각형 11 -> 5.
    const int GrassSubdivision = 2;

    /// 풀 사이 간격. 이 값이 곧 개수다 - 절반으로 줄이면 네 배가 된다.
    /// 저장소 기본값은 0.5인데 우리 60x60 맵에서는 듬성해 보여서 0.12로 좁혔다.
    const float GrassSpacing = 0.12f;

    /// 이 거리마다 밀도가 절반이 된다. 저장소 기본값 50은 300유닛 시야 기준이라
    /// 60x60 맵 전체가 "가까움"으로 잡혀 감쇠가 걸리지 않는다 - 균일한 밀도가 된다.
    const float GrassFullDensityDistance = 50f;

    const float GrassDrawDistance = 300f;
    const int GrassMaxBlades = 700000;

    const string NatureModels = "Assets/AssetStore/Kenney/nature-kit/Models/FBX format/";
    const string SurvivalModels = "Assets/AssetStore/Kenney/survival-kit/Models/FBX format/";
    const string MaterialFolder = "Assets/Art/Materials/";
    const string ForestMaterialFolder = "Assets/Art/Materials/Forest/";

    /// 숲 팔레트. Kenney FBX에 박힌 머티리얼을 그대로 쓰면 잎이 청록으로 나온다 —
    /// Unity의 `ImportViaMaterialDescription`이 이 FBX의 디퓨즈를 제대로 읽지 못해서
    /// leafsDark가 (0.45, 0.83, 0.84)로 들어온다. 서드파티 원본은 고치지 않는 것이 규칙이라
    /// (AGENTS.md) 프로젝트 소유 머티리얼을 만들어 임포터 리맵으로 갈아 끼운다.
    ///
    /// 색은 기획서 12장 "밤: 채도를 죽인다"에 맞춰 낮은 채도로 잡았다.
    /// `Sways`가 true면 `FoliageWind` 셰이더를 물린다. 잎과 풀만 흔들린다 — 줄기·바위·흙이
    /// 같이 흔들리면 나무가 통째로 미끄러지는 것처럼 보인다.
    static readonly (string Name, Color Colour, bool Sways)[] ForestPalette =
    {
        ("leafsDark",    new Color(0.13f, 0.28f, 0.17f), true),
        ("leafsGreen",   new Color(0.24f, 0.42f, 0.23f), true),
        ("leafsFall",    new Color(0.45f, 0.30f, 0.14f), true),
        ("grass",        new Color(0.24f, 0.38f, 0.21f), true),
        ("corn",         new Color(0.52f, 0.46f, 0.20f), true),
        ("woodBarkDark", new Color(0.24f, 0.18f, 0.13f), false),
        ("woodBark",     new Color(0.33f, 0.24f, 0.16f), false),
        ("woodBirch",    new Color(0.62f, 0.60f, 0.55f), false),
        ("woodInner",    new Color(0.42f, 0.32f, 0.21f), false),
        ("wood",         new Color(0.38f, 0.28f, 0.18f), false),
        ("woodDark",     new Color(0.26f, 0.19f, 0.13f), false),
        ("dirt",         new Color(0.31f, 0.25f, 0.19f), false),
        ("dirtDark",     new Color(0.24f, 0.19f, 0.15f), false),
        ("stone",        new Color(0.40f, 0.41f, 0.43f), false),
        ("stoneDark",    new Color(0.30f, 0.31f, 0.33f), false),
        ("rock",         new Color(0.35f, 0.35f, 0.38f), false),
        ("water",        new Color(0.16f, 0.28f, 0.34f), false),
        ("colorWhite",   new Color(0.72f, 0.72f, 0.70f), false),
        ("colorTan",     new Color(0.52f, 0.43f, 0.31f), false),
        ("colorRed",     new Color(0.42f, 0.16f, 0.16f), false),
        ("colorRedDark", new Color(0.32f, 0.12f, 0.13f), false),
        ("colorYellow",  new Color(0.55f, 0.45f, 0.20f), false),
        ("colorPurple",  new Color(0.32f, 0.26f, 0.42f), false),
        ("_defaultMat",  new Color(0.35f, 0.35f, 0.35f), false),
    };

    /// 숲 바닥. 지면은 평면 하나뿐이라 킷의 `ground_grass` 타일을 깔 이유가 없다 —
    /// 이 킷은 텍스처 없이 단색 머티리얼이라 타일을 깔아도 결과가 같고 드로우콜만 늘어난다.
    static readonly Color GroundColour = new(0.24f, 0.36f, 0.19f);

    /// 밤 숲이라 어두운 변종을 우선 쓴다. 침엽수를 섞어 실루엣을 갈라 놓는다.
    static readonly string[] TreeModels =
    {
        "tree_default_dark", "tree_simple_dark", "tree_detailed_dark", "tree_oak_dark",
        "tree_pineTallA", "tree_pineTallC", "tree_pineRoundB", "tree_pineDefaultA",
    };

    static readonly string[] UndergrowthModels =
    {
        "rock_smallA", "rock_smallD", "rock_tallB", "stump_old", "stump_round",
        "plant_bush", "plant_bushLarge", "log", "grass_large",
    };

    /// 등급별 겉모습 (기획서 6.5.2). 형태·재질·색·발광이 모두 달라야 원거리에서 구분된다.
    static readonly string[] TierMeshModels = { "box", "chest", "box-large" };
    static readonly string[] TierMaterialNames = { "Box_T1", "Box_T2", "Box_T3" };

    /// 링별 등급 가중치 (기획서 6.3). 바깥 1등급 위주 · 중간 2등급 · 중심 3등급.
    /// 0으로 잘라내지 않고 꼬리를 남긴 이유는 매 밤 리롤이 의미를 가지려면 링 안에서도
    /// 뽑기가 흔들려야 하기 때문이다.
    static readonly Vector3Int OuterWeights = new(8, 2, 0);
    static readonly Vector3Int MidWeights = new(2, 7, 1);
    static readonly Vector3Int CoreWeights = new(0, 2, 8);

    /// 링 경계. 숲 반지름에 대한 비율이다.
    const float CoreRingRatio = 0.25f;
    const float MidRingRatio = 0.55f;

    [MenuItem(MenuPath)]
    static void Build()
    {
        var director = Object.FindFirstObjectByType<MatchDirector>();
        if (director == null)
        {
            EditorUtility.DisplayDialog("숲 맵 생성",
                "열려 있는 씬에 MatchDirector가 없다. 매치 씬(Battle_01)을 먼저 연다.", "확인");
            return;
        }

        var so = new SerializedObject(director);
        var origin = so.FindProperty("cafeOrigin").vector3Value;
        var forestSize = so.FindProperty("forestSize").vector2Value;
        var spawnInset = so.FindProperty("spawnInset").floatValue;
        var spawnSpacing = so.FindProperty("spawnSlotSpacing").floatValue;

        var boxes = Object.FindObjectsByType<ItemBox>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var keepOut = CollectKeepOut(origin, forestSize, spawnInset, spawnSpacing, boxes);

        DressBoxes(boxes, origin, forestSize);
        var planted = PlantForest(origin, forestSize, keepOut);

        var scene = director.gameObject.scene;
        PaintGround(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"숲 맵 생성: 나무·수풀 {planted}개, 상자 {boxes.Length}개 정리. "
                + $"숲 {forestSize.x}x{forestSize.y}, 원점 {origin}.");
    }

    /// 숲 팔레트를 만들어 이름으로 찾을 수 있게 돌려준다.
    ///
    /// 임포터 리맵(`AddRemap` + `materialLocation = External`)은 쓰지 않는다. External로
    /// 바꾸는 순간 Unity가 잘못된 색 그대로 머티리얼을 서드파티 폴더에 추출해 버리고,
    /// 추출된 파일이 리맵보다 우선한다. 원본 팩은 손대지 않는다는 규칙(AGENTS.md)에도 어긋난다.
    /// 그래서 심는 시점에 씬 인스턴스의 머티리얼만 갈아 끼운다 — 그쪽은 우리 것이다.
    static Dictionary<string, Material> BuildPalette()
    {
        var palette = new Dictionary<string, Material>();
        foreach (var entry in ForestPalette)
            palette[entry.Name] = EnsurePaletteMaterial(entry.Name, entry.Colour, entry.Sways);
        return palette;
    }

    /// 인스턴싱에 필요한 조각 하나. 모델 하나가 서브메시마다 다른 머티리얼을 쓰므로
    /// (나무 = 줄기 + 잎) 모델이 아니라 이 단위로 배치를 나눈다.
    readonly struct ModelPart
    {
        public readonly Mesh Mesh;
        public readonly int Submesh;
        public readonly Material Material;

        /// 모델 안에서 메시가 놓인 로컬 오프셋. Kenney 모델은 (0, -0.05, 0)만큼 내려가 있다.
        public readonly Vector3 LocalOffset;

        public ModelPart(Mesh mesh, int submesh, Material material, Vector3 localOffset)
        {
            Mesh = mesh;
            Submesh = submesh;
            Material = material;
            LocalOffset = localOffset;
        }
    }

    /// 모델을 인스턴싱용 조각으로 편다. 킷 머티리얼은 이 시점에 팔레트로 바꾼다.
    static List<ModelPart> PartsOf(GameObject model, Dictionary<string, Material> palette)
    {
        var parts = new List<ModelPart>();

        foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;

            var offset = renderer.transform.localPosition;
            var shared = renderer.sharedMaterials;

            for (var submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
            {
                var kit = submesh < shared.Length ? shared[submesh] : null;
                if (kit == null) continue;

                if (!palette.TryGetValue(kit.name, out var replacement))
                {
                    // 조용히 넘기면 킷의 잘못된 색(잎이 청록)이 그대로 남는다.
                    Debug.LogWarning($"숲 팔레트에 없는 머티리얼: {kit.name} ({model.name}). "
                                   + "ForestPalette에 색을 추가한다.");
                    continue;
                }

                parts.Add(new ModelPart(filter.sharedMesh, submesh, replacement, offset));
            }
        }

        return parts;
    }

    static Material EnsurePaletteMaterial(string name, Color colour, bool sways)
    {
        if (!AssetDatabase.IsValidFolder(ForestMaterialFolder.TrimEnd('/')))
            AssetDatabase.CreateFolder("Assets/Art/Materials", "Forest");

        var shader = sways
            ? AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/Shaders/FoliageWind.shadergraph")
            : Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
        {
            Debug.LogError("FoliageWind.shadergraph를 찾지 못했다. 잎이 흔들리지 않는다.");
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        var path = ForestMaterialFolder + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        // GPU 인스턴싱을 켜지 않으면 `Graphics.RenderMeshInstanced`가
        // InvalidOperationException을 던지고, 그 예외가 URP 프레임을 통째로 죽여 화면이
        // 하얗게 나온다. 씬 오브젝트로 그릴 때는 SRP Batcher가 대신 처리해서 이 플래그가
        // 필요 없었다.
        material.enableInstancing = true;

        // 색은 매번 다시 넣는다. 팔레트를 고쳤을 때 도구를 다시 돌리면 반영되어야 한다.
        material.SetColor("_BaseColor", colour);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    /// 숲 바닥을 잔디색으로 맞춘다. 기본 URP `Lit`(패키지 공유 에셋)을 물고 있으면
    /// 색을 바꾸는 순간 그것을 쓰는 다른 오브젝트까지 같이 물든다.
    static void PaintGround(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name != GroundName) continue;

            var renderer = root.GetComponent<MeshRenderer>();
            if (renderer == null) return;

            var path = MaterialFolder + "ForestGround.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", GroundColour);
            material.SetFloat("_Smoothness", 0f);
            EditorUtility.SetDirty(material);

            renderer.sharedMaterial = material;
            return;
        }

        Debug.LogWarning($"씬에 '{GroundName}' 오브젝트가 없다. 바닥 색을 맞추지 못했다.");
    }

    /// 나무를 놓지 않을 자리. 상자와 팀 스폰이다.
    static List<KeepOut> CollectKeepOut(
        Vector3 origin, Vector2 forestSize, float spawnInset, float spawnSpacing, ItemBox[] boxes)
    {
        var keepOut = new List<KeepOut>();

        foreach (var box in boxes) keepOut.Add(new KeepOut(box.transform.position, BoxClearance));

        // 네 모서리 x 팀당 자리 둘. MatchDirector.NightSpawnPosition과 같은 식이다.
        var corners = new[]
        {
            new Vector2(-1f, 1f), new Vector2(1f, 1f),
            new Vector2(-1f, -1f), new Vector2(1f, -1f),
        };

        foreach (var corner in corners)
        {
            var edge = new Vector3(corner.x * (forestSize.x * 0.5f - spawnInset), 0f,
                                   corner.y * (forestSize.y * 0.5f - spawnInset));
            var inward = new Vector3(-corner.x, 0f, -corner.y).normalized;
            for (var slot = 0; slot < 2; slot++)
                keepOut.Add(new KeepOut(origin + edge + inward * (slot * spawnSpacing), SpawnClearance));
        }

        return keepOut;
    }

    readonly struct KeepOut
    {
        public readonly Vector3 Centre;
        public readonly float Radius;

        public KeepOut(Vector3 centre, float radius)
        {
            Centre = centre;
            Radius = radius;
        }
    }

    static bool Blocked(Vector3 world, List<KeepOut> keepOut)
    {
        foreach (var area in keepOut)
        {
            var flat = world - area.Centre;
            flat.y = 0f;
            if (flat.sqrMagnitude < area.Radius * area.Radius) return true;
        }
        return false;
    }

    /// 상자에 링 가중치와 등급별 겉모습을 채운다. 위치는 건드리지 않는다.
    static void DressBoxes(ItemBox[] boxes, Vector3 origin, Vector2 forestSize)
    {
        var meshes = new Object[TierMeshModels.Length];
        for (var i = 0; i < TierMeshModels.Length; i++)
            meshes[i] = LoadMesh(SurvivalModels + TierMeshModels[i] + ".fbx");

        var materials = new Object[TierMaterialNames.Length];
        for (var i = 0; i < TierMaterialNames.Length; i++)
            materials[i] = AssetDatabase.LoadAssetAtPath<Material>(
                MaterialFolder + TierMaterialNames[i] + ".mat");

        var glowMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "ItemBoxGlow.mat");
        var radius = Mathf.Min(forestSize.x, forestSize.y) * 0.5f;

        foreach (var box in boxes)
        {
            var flat = box.transform.position - origin;
            flat.y = 0f;
            var ratio = radius > 0f ? flat.magnitude / radius : 1f;

            var weights = ratio < CoreRingRatio ? CoreWeights
                        : ratio < MidRingRatio ? MidWeights
                        : OuterWeights;

            var boxSo = new SerializedObject(box);
            boxSo.FindProperty("tierWeights").vector3IntValue = weights;
            boxSo.ApplyModifiedPropertiesWithoutUndo();

            DressView(box, meshes, materials, glowMaterial);
        }
    }

    static void DressView(ItemBox box, Object[] meshes, Object[] materials, Material glowMaterial)
    {
        var view = box.GetComponent<ItemBoxView>();
        if (view == null) view = box.gameObject.AddComponent<ItemBoxView>();

        GroundBox(box);
        var body = EnsureBody(box.transform);
        SeedBody(body, meshes.Length > 0 ? meshes[0] as Mesh : null,
                       materials.Length > 0 ? materials[0] as Material : null);
        var glow = EnsureGlow(box.transform, glowMaterial);

        var viewSo = new SerializedObject(view);
        viewSo.FindProperty("body").objectReferenceValue = body;
        viewSo.FindProperty("glow").objectReferenceValue = glow;
        FillArray(viewSo.FindProperty("tierMaterials"), materials);
        FillArray(viewSo.FindProperty("tierMeshes"), meshes);
        viewSo.ApplyModifiedPropertiesWithoutUndo();
    }

    static void FillArray(SerializedProperty array, Object[] values)
    {
        array.arraySize = values.Length;
        for (var i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    /// 본체 메시를 자식으로 뺀다. `ItemBoxView`가 등급마다 메시 크기를 정규화하면서 스케일을
    /// 건드리는데, 루트에 있으면 콜라이더와 발광 셸까지 같이 늘어난다. 상호작용 범위가
    /// 등급에 따라 달라지면 안 된다.
    static Renderer EnsureBody(Transform parent)
    {
        // 루트에 남아 있던 메시는 걷어낸다. 예전 상자는 큐브를 루트에 직접 달고 있었다.
        var rootFilter = parent.GetComponent<MeshFilter>();
        var rootRenderer = parent.GetComponent<MeshRenderer>();
        if (rootRenderer != null) Object.DestroyImmediate(rootRenderer);
        if (rootFilter != null) Object.DestroyImmediate(rootFilter);

        var existing = parent.Find(BodyChildName);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var body = new GameObject(BodyChildName, typeof(MeshFilter), typeof(MeshRenderer));
        body.transform.SetParent(parent, false);
        return body.GetComponent<MeshRenderer>();
    }

    /// 편집 중에 보이도록 1등급 겉모습을 미리 끼워 둔다. 실제 등급은 밤마다 서버가 뽑고
    /// `ItemBoxView`가 갈아 끼운다 — 씬에 구워 둔 이 값은 어디까지나 에디터용 자리표시다.
    static void SeedBody(Renderer body, Mesh mesh, Material material)
    {
        if (material != null) body.sharedMaterial = material;
        if (mesh == null) return;

        var filter = body.GetComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        var height = mesh.bounds.size.y;
        if (height <= Mathf.Epsilon) return;

        // `ItemBoxView.Normalise`와 같은 식이다. 편집 화면과 플레이 화면이 어긋나면 안 된다.
        var scale = BodyHeight / height;
        body.transform.localScale = Vector3.one * scale;
        body.transform.localPosition =
            new Vector3(0f, -BodyHeight * 0.5f - mesh.bounds.min.y * scale, 0f);
    }

    /// 상자 루트를 규격에 맞춘다. **루트 원점은 상자의 중심이고, 밑면이 지면에 닿는다.**
    ///
    /// 씬의 상자는 Unity 기본 Cube 시절 규격이 남아 있었다 — y=0.5에 스케일 0.8과 1.25가
    /// 섞여 있어서 상자마다 크기가 달랐다. 등급은 매 밤 리롤되므로(기획서 6.3) 자리에 따라
    /// 상자 크기가 다르면 안 된다. 루트 원점 높이는 그대로 상호작용 기준점이라
    /// (`ItemBox.InReach`) 반 높이에 두어 예전 값과 거의 같게 맞춘다.
    static void GroundBox(ItemBox box)
    {
        var t = box.transform;
        var position = t.position;
        position.y = BodyHeight * 0.5f;
        t.position = position;
        t.localScale = Vector3.one;

        var collider = box.GetComponent<BoxCollider>();
        if (collider == null) return;

        collider.center = Vector3.zero;
        collider.size = Vector3.one * BodyHeight;
    }

    /// 3등급 발광 아웃라인. 안개 너머로 새어 나와야 하므로(기획서 6.5.2) 상자 본체와 별개의
    /// Transparent 오브젝트다 — 안개 패스는 불투명만 덮기 때문이다.
    static Renderer EnsureGlow(Transform parent, Material glowMaterial)
    {
        var existing = parent.Find(GlowChildName);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        // 메시와 크기는 `ItemBoxView`가 등급에 맞춰 채운다 — 본체와 같은 메시를 살짝 키워
        // 그리는 아웃라인 헐이라 여기서 정할 수 있는 것이 없다.
        var glow = new GameObject(GlowChildName, typeof(MeshFilter), typeof(MeshRenderer));
        glow.transform.SetParent(parent, false);
        glow.transform.localPosition = Vector3.zero;

        var renderer = glow.GetComponent<Renderer>();
        renderer.sharedMaterial = glowMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        // 편집 중에는 꺼 둔다. 켜고 끄는 것은 등급을 아는 런타임(`ItemBoxView`)의 몫인데,
        // 에디터에서는 그 Update가 돌지 않아 모든 상자가 3등급처럼 빛나 보인다.
        renderer.enabled = false;
        return renderer;
    }

    /// 배치를 모으는 열쇠. 같은 (메시, 서브메시, 머티리얼, 그림자)끼리 한 번에 그린다.
    readonly struct BatchKey : System.IEquatable<BatchKey>
    {
        public readonly Mesh Mesh;
        public readonly int Submesh;
        public readonly Material Material;
        public readonly bool CastShadows;

        public BatchKey(Mesh mesh, int submesh, Material material, bool castShadows)
        {
            Mesh = mesh;
            Submesh = submesh;
            Material = material;
            CastShadows = castShadows;
        }

        public bool Equals(BatchKey other) =>
            Mesh == other.Mesh && Submesh == other.Submesh
            && Material == other.Material && CastShadows == other.CastShadows;

        public override bool Equals(object obj) => obj is BatchKey other && Equals(other);

        public override int GetHashCode() =>
            System.HashCode.Combine(Mesh, Submesh, Material, CastShadows);
    }

    struct Placement
    {
        public Vector3 Position;
        public float Yaw;
        public float Scale;
    }

    static int PlantForest(Vector3 origin, Vector2 forestSize, List<KeepOut> keepOut)
    {
        // 예전에는 프리팹 인스턴스 259개를 씬에 구웠다. 씬 오브젝트로 두면 SRP Batcher가
        // 먼저 잡아서 GPU 인스턴싱이 동작하지 않고, 풀을 깔면 드로우콜이 수천 개가 된다.
        var old = GameObject.Find(ForestRootName);
        if (old != null) Object.DestroyImmediate(old);

        var root = new GameObject(ForestRootName, typeof(ForestInstances), typeof(ForestGrass));
        root.transform.position = origin;
        SetupGrass(root.GetComponent<ForestGrass>(), origin, forestSize);

        var palette = BuildPalette();
        var trees = LoadModels(NatureModels, TreeModels);
        var undergrowth = LoadModels(NatureModels, UndergrowthModels);
        if (trees.Count == 0)
        {
            Debug.LogError($"{NatureModels}에서 나무 모델을 하나도 찾지 못했다. "
                         + "AssetStore/Kenney/nature-kit이 임포트되었는지 확인한다.");
            return 0;
        }

        var parts = new Dictionary<GameObject, List<ModelPart>>();
        foreach (var model in trees) parts[model] = PartsOf(model, palette);
        foreach (var model in undergrowth) if (!parts.ContainsKey(model)) parts[model] = PartsOf(model, palette);

        var collected = new Dictionary<BatchKey, List<Placement>>();

        void Add(GameObject model, Vector3 world, float yaw, float scale, bool castShadows)
        {
            foreach (var part in parts[model])
            {
                var key = new BatchKey(part.Mesh, part.Submesh, part.Material, castShadows);
                if (!collected.TryGetValue(key, out var list))
                {
                    list = new List<Placement>();
                    collected[key] = list;
                }

                list.Add(new Placement
                {
                    Position = world + part.LocalOffset * scale,
                    Yaw = yaw,
                    Scale = scale,
                });
            }
        }

        var state = Random.state;
        Random.InitState(Seed);

        var halfX = forestSize.x * 0.5f;
        var halfZ = forestSize.y * 0.5f;
        var radius = Mathf.Min(halfX, halfZ);
        var planted = 0;

        // --- 나무와 수풀 ---
        for (var x = -halfX; x <= halfX; x += ScatterStep)
        for (var z = -halfZ; z <= halfZ; z += ScatterStep)
        {
            var jitter = new Vector3(Random.Range(-ScatterStep, ScatterStep) * 0.5f, 0f,
                                     Random.Range(-ScatterStep, ScatterStep) * 0.5f);
            var local = new Vector3(x, 0f, z) + jitter;
            if (Mathf.Abs(local.x) > halfX || Mathf.Abs(local.z) > halfZ) continue;

            var world = origin + local + Vector3.up * GroundY;
            if (Blocked(world, keepOut)) continue;

            // 중심으로 갈수록 빽빽하게. ratio 0 = 중심, 1 = 가장자리.
            var ratio = radius > 0f ? Mathf.Clamp01(local.magnitude / radius) : 1f;
            var density = Mathf.Lerp(InnerDensity, OuterDensity, ratio);
            if (Random.value > density) continue;

            // 넷에 하나 정도는 나무 대신 낮은 수풀을 놓아 눈높이를 흔든다.
            var pool = undergrowth.Count > 0 && Random.value < 0.25f ? undergrowth : trees;
            Add(pool[Random.Range(0, pool.Count)], world,
                Random.Range(0f, 360f),
                TreeScale * Random.Range(1f - TreeScaleJitter, 1f + TreeScaleJitter),
                castShadows: true);
            planted++;
        }

        Random.state = state;

        // --- 배치로 굽는다 ---
        var batches = new ForestInstances.Batch[collected.Count];
        var index = 0;
        foreach (var pair in collected)
        {
            var list = pair.Value;
            var positions = new Vector3[list.Count];
            var yaws = new float[list.Count];
            var scales = new float[list.Count];

            for (var i = 0; i < list.Count; i++)
            {
                positions[i] = list[i].Position;
                yaws[i] = list[i].Yaw;
                scales[i] = list[i].Scale;
            }

            batches[index++] = new ForestInstances.Batch
            {
                Mesh = pair.Key.Mesh,
                Submesh = pair.Key.Submesh,
                Material = pair.Key.Material,
                CastShadows = pair.Key.CastShadows,
                Positions = positions,
                Yaws = yaws,
                Scales = scales,
            };
        }

        var bounds = new Bounds(origin, new Vector3(forestSize.x + 20f, 30f, forestSize.y + 20f));
        root.GetComponent<ForestInstances>().SetBatches(batches, bounds);

        Debug.Log($"인스턴싱: 배치 {batches.Length}개 · 나무/수풀 {planted}개 "
                + "(GameObject 0개). 바닥 풀은 ForestGrass가 컴퓨트로 뽑는다.");
        return planted;
    }

    /// 바닥 풀을 맵에 맞춘다. 크기와 원점은 `MatchDirector`에서 온 값을 그대로 넘긴다 -
    /// 여기서 다시 적으면 맵을 넓힐 때 풀만 예전 크기로 남는다.
    static void SetupGrass(ForestGrass grass, Vector3 origin, Vector2 forestSize)
    {
        var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(GrassComputePath);
        var material = AssetDatabase.LoadAssetAtPath<Material>(GrassMaterialPath);

        if (compute == null || material == null)
        {
            Debug.LogError($"바닥 풀 에셋을 찾지 못했다: {GrassComputePath} / {GrassMaterialPath}");
            return;
        }

        var so = new SerializedObject(grass);
        so.FindProperty("positionsCompute").objectReferenceValue = compute;
        so.FindProperty("bladeMaterial").objectReferenceValue = material;
        so.FindProperty("subdivision").intValue = GrassSubdivision;
        so.FindProperty("spacing").floatValue = GrassSpacing;
        so.FindProperty("fullDensityDistance").floatValue = GrassFullDensityDistance;
        so.FindProperty("drawDistance").floatValue = GrassDrawDistance;
        so.FindProperty("maxBlades").intValue = GrassMaxBlades;
        so.ApplyModifiedPropertiesWithoutUndo();

        grass.Configure(origin, forestSize, GroundY);
        EditorUtility.SetDirty(grass);
    }

    static List<GameObject> LoadModels(string folder, string[] names)
    {
        var loaded = new List<GameObject>();
        foreach (var name in names)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(folder + name + ".fbx");
            if (model != null) loaded.Add(model);
            else Debug.LogWarning($"모델을 찾지 못했다: {folder}{name}.fbx");
        }
        return loaded;
    }

    static Mesh LoadMesh(string path)
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is Mesh mesh) return mesh;

        Debug.LogWarning($"메시를 찾지 못했다: {path}");
        return null;
    }
}
