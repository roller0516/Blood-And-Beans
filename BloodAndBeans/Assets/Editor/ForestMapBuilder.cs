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
/// 나무는 `NetworkObject`가 없는 순수 표현이라 씬에 그대로 굽는다.
/// ponytail: 숲 상자는 이제 런타임에 `MatchDirector`가 프리팹으로 스폰한다. 씬에 상자가 없으면
/// 아래의 상자 배치·통로 검사는 0개로 돈다. 런타임 자리에 통로를 보장하려면 그쪽으로 옮긴다.
public static class ForestMapBuilder
{
    const string MenuPath = "Blood & Beans/숲 맵 생성";
    const string RerollMenuPath = "Blood & Beans/숲 맵 생성 — 씨앗 무작위";
    const string VerifyMenuPath = "Blood & Beans/숲 씨앗 훑기";

    /// 씨앗 훑기가 볼 씨앗 수. 씬의 씨앗부터 이만큼 이어서 센다.
    const int VerifySeedCount = 200;

    const string ForestRootName = "Forest";
    const string GlowChildName = "Glow";
    const string BodyChildName = "Body";
    const string GroundName = "Ground";

    /// `ItemBoxView.bodyHeight` 기본값과 같아야 편집 화면과 플레이 화면의 상자 크기가 같다.
    const float BodyHeight = 0.9f;

    /// 지면 높이. 중력이 없어서(PlayerMove) 한번 어긋나면 스스로 내려오지 않는다.
    /// Kenney nature-kit 모델은 원점이 밑동이라 그대로 0에 놓는다.
    const float GroundY = 0f;

    /// 상자 주변은 비운다. 나무가 상자를 가리면 못 찾는다. 콜라이더가 생긴 뒤로는 "가린다"가
    /// "못 다가간다"가 되므로 `ItemBox.reach`(2.5) + 나무 반경 + 플레이어 반경보다 커야 한다.
    const float BoxClearance = 4.5f;
    const float SpawnClearance = 6f;    // 스폰 자리도 비운다. 시작하자마자 나무에 끼면 안 된다

    /// 콜라이더 두께. 모델의 XZ 반경에 이 배수를 곱한다. 줄기만 잡으면 탑다운에서 나무를
    /// 뚫고 지나가 보이고, 수관을 통째로 잡으면 숲이 벽이 된다 — 그 사이의 조정 손잡이다.
    const float ColliderFactor = 0.32f;
    const float ColliderMinRadius = 0.35f;
    const float ColliderMaxRadius = 1.1f;

    /// 세워 둔 캡슐 하나로 나무를 대신한다. 중력이 없고(PlayerMove) 플레이어 y가 고정이라
    /// 충돌은 사실상 평면 위의 원이다.
    /// ponytail: `log`처럼 누운 모델도 원으로 근사한다. 실루엣이 어긋나 보이면 그때 박스로 바꾼다.
    const float ColliderHeight = 3f;
    const string CollidersChildName = "Colliders";

    /// 지나갈 수 있어야 하는 것들. 기획서 6.2가 수풀을 은폐물로 쓰므로 몸으로 막지 않는다.
    static readonly string[] NoColliderModels =
    {
        "forestpack_foliage_grassPatch_small_1", "forestpack_foliage_grassPatch_small_2",
        "forestpack_foliage_mushroom_blue_big", "forestpack_foliage_mushroom_red_small",
    };

    /// 연결성 검사 격자. 플레이어가 지나갈 틈보다 촘촘해야 통로를 놓치지 않는다.
    const float ReachCell = 0.5f;

    /// CharacterController 반지름 0.5 + 스킨과 조작 여유.
    const float PlayerRadius = 0.6f;

    /// 막힌 상자를 뚫는 시도 횟수. 한 번에 통로 하나를 낸다.
    const int RepairPasses = 12;

    /// 바깥에서 중심으로 갈수록 빽빽해진다. 기획서 6.2-3: 중심부는 늦게 열리고 조우가
    /// 거기서 생긴다 — 화면에서도 안쪽이 더 답답해야 그 긴장이 읽힌다.
    const float OuterDensity = 0.42f;
    const float InnerDensity = 0.78f;

    /// 나무를 뿌릴 격자 간격. 이 칸마다 위 확률로 한 그루를 시도하고, 칸 안에서 흔든다.
    const float ScatterStep = 2.2f;

    /// Supercyan 전나무는 원본이 3.43유닛이다. 이 배수로 6유닛 안팎이 되게 키운다 —
    /// 플레이어 캡슐(높이 2)보다 충분히 커야 숲이 잔디밭으로 보이지 않는다.
    /// 잎나무(2.18)는 같은 배수에서 3.8유닛이 되어 실루엣이 갈린다.
    const float TreeScale = 1.75f;
    const float TreeScaleJitter = 0.35f;

    /// 바닥 풀은 Kenney 덩어리를 흩뿌리지 않는다. `ForestGrass`가 컴퓨트 셰이더로 위치를
    /// 뽑아 인디렉트 드로우 한 번에 그린다 - 덩어리 2,494개(33.9만 삼각형)를 대체한다.
    const string GrassComputePath = "Assets/Art/Shaders/ForestGrassPositions.compute";
    const string GrassMaterialPath = MaterialFolder + "ForestGrassBlade.mat";

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

    /// 숲의 나무·수풀. Supercyan Free Forest Sample의 High Quality 프리팹을 쓴다.
    /// Mobile 쪽은 같은 메시에 저해상도 텍스처라 탑다운에서 흐리게 뭉친다.
    const string ForestModels = "Assets/AssetStore/Supercyan Free Forest Sample/Prefabs/High Quality/";
    const string SurvivalModels = "Assets/AssetStore/Kenney/survival-kit/Models/FBX format/";
    const string MaterialFolder = "Assets/Art/Environment/Materials/";
    const string ForestMaterialFolder = MaterialFolder + "Forest/";

    /// 숲 머티리얼은 팩의 High Quality `.mat`을 그대로 쓴다. Supercyan은 텍스처가 있는
    /// 팩이라 단색으로 덮으면 나무가 덩어리가 된다 — 색도 텍스처도 건드리지 않는다.
    /// 인스턴싱 플래그만 켜야 해서 프로젝트 소유 사본을 둔다 (`EnsureInstancedCopy`).

    /// 숲 바닥. 지면은 평면 하나뿐이고 그 위를 `ForestGrass`가 덮으므로 타일을 깔지 않는다.
    static readonly Color GroundColour = new(0.24f, 0.36f, 0.19f);

    /// 침엽수와 활엽수를 섞어 실루엣을 갈라 놓는다. 팩이 주는 나무는 이 둘뿐이라
    /// 크기 지터(`TreeScaleJitter`)와 회전이 다양성을 대신 만든다.
    /// 경로는 `ForestModels` 기준 상대 경로다 — 팩이 종류별 하위 폴더를 쓴다.
    static readonly string[] TreeModels =
    {
        "Tree/Fir/forestpack_tree_fir_tall",
        //"Tree/Leaf/Normal/forestpack_tree_1_leaf_1",
    };

    static readonly string[] UndergrowthModels =
    {
        "Tree/Treestump/forestpack_tree_stump_1",
        "Stone/forestpack_stone_large_1",
        "Stone/forestpack_stone_medium_1",
        "Foliage/Grass/forestpack_foliage_grassPatch_small_1",
        "Foliage/Grass/forestpack_foliage_grassPatch_small_2",
        "Foliage/Mushroom/forestpack_foliage_mushroom_blue_big",
        "Foliage/Mushroom/forestpack_foliage_mushroom_red_small",
    };

    /// 등급별 겉모습 (기획서 6.5.2). 형태·재질·색·발광이 모두 달라야 원거리에서 구분된다.
    static readonly string[] TierMeshModels = { "box", "chest", "box-large" };
    static readonly string[] TierMaterialNames = { "Box_T1", "Box_T2", "Box_T3" };

    // 링별 등급 가중치는 `ForestRings`에 있다 (기획서 6.3). 런타임 재배치와 같은 표를 본다.

    /// 상자를 뿌릴 링. 링마다 상자 수를 이 비중으로 나누고, 링 안에서는 섹터를 균등하게
    /// 잘라 하나씩 놓는다 — 순수 난수로 뿌리면 한쪽 모서리에 몰려서 스폰 자리에 따라
    /// 유불리가 갈린다. `Centre`와 `Jitter`는 숲 반지름에 대한 비율이다.
    static readonly (float Centre, float Jitter, int Share)[] BoxRings =
    {
        (0.14f, 0.05f, 3),   // 중심 - 3등급이 나오는 링 (기획서 6.3)
        (0.40f, 0.07f, 6),
        (0.78f, 0.07f, 8),
    };

    /// 상자끼리 이만큼은 떨어뜨린다. 붙어 있으면 한 번 개척으로 둘을 다 먹는다.
    const float BoxSeparation = 7f;
    const int BoxPlaceAttempts = 24;

    /// 씬에 적힌 씨앗으로 굽는다. 몇 번을 돌려도 같은 숲이 나온다.
    [MenuItem(MenuPath)]
    internal static void Build() => Build(null);

    /// 씨앗을 새로 뽑아 씬에 적고 굽는다. 마음에 드는 숲이 나오면 그 씨앗이 `MatchDirector`에
    /// 남으므로 「숲 맵 생성」으로 언제든 같은 숲을 되살린다.
    [MenuItem(RerollMenuPath)]
    internal static void Reroll() => Build(Random.Range(int.MinValue, int.MaxValue));

    /// 씨앗을 통째로 훑어 "네 모서리에서 걸어서 모든 상자에 닿는다"는 불변식을 확인한다.
    /// 씬은 건드리지 않는다. 밀도·콜라이더 두께·상자 링을 만진 뒤 이걸 돌린다 — 한 씨앗만
    /// 보고 넘어가면 다른 씨앗에서 중심부가 벽이 되는 것을 못 잡는다.
    [MenuItem(VerifyMenuPath)]
    internal static void VerifySeeds()
    {
        var director = Object.FindFirstObjectByType<MatchDirector>();
        if (director == null)
        {
            EditorUtility.DisplayDialog("숲 씨앗 훑기",
                "열려 있는 씬에 MatchDirector가 없다. 매치 씬(Battle_01)을 먼저 연다.", "확인");
            return;
        }

        var so = new SerializedObject(director);
        var origin = so.FindProperty("cafeOrigin").vector3Value;
        var forestSize = so.FindProperty("forestSize").vector2Value;
        var spawns = SpawnPoints(origin, forestSize,
            so.FindProperty("spawnInset").floatValue,
            so.FindProperty("spawnSlotSpacing").floatValue);

        var boxCount = Object.FindObjectsByType<ItemBox>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        var trees = LoadModels(ForestModels, TreeModels);
        var undergrowth = LoadModels(ForestModels, UndergrowthModels);
        if (trees.Count == 0)
        {
            Debug.LogError($"{ForestModels}에서 나무 모델을 하나도 찾지 못했다.");
            return;
        }

        var baseRadii = new Dictionary<GameObject, float>();
        foreach (var model in trees) baseRadii[model] = BaseRadius(model);
        foreach (var model in undergrowth) baseRadii[model] = BaseRadius(model);

        var densityScale = so.FindProperty("forestDensity").floatValue;
        var first = so.FindProperty("mapSeed").intValue;
        var state = Random.state;
        var failed = new List<int>();
        var carved = 0;

        for (var i = 0; i < VerifySeedCount; i++)
        {
            var seed = first + i;
            Random.InitState(seed);

            var boxes = BoxPositions(boxCount, origin, forestSize, spawns);
            var props = Scatter(origin, forestSize, CollectKeepOut(spawns, boxes),
                                trees, undergrowth, baseRadii, densityScale);

            carved += OpenPassages(props, origin, forestSize, spawns, boxes);
            if (FirstUnreachable(Walkable(props, origin, forestSize, spawns), boxes).HasValue)
                failed.Add(seed);
        }

        Random.state = state;

        if (failed.Count > 0)
        {
            Debug.LogError($"씨앗 훑기: {VerifySeedCount}개 중 {failed.Count}개가 막혔다 — "
                         + $"{string.Join(", ", failed)}");
            return;
        }

        Debug.Log($"씨앗 훑기: 씨앗 {first}부터 {VerifySeedCount}개 모두 모든 상자에 닿는다. "
                + $"통로 내느라 걷어낸 나무 총 {carved}개.");
    }

    static void Build(int? reroll)
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

        var seedProperty = so.FindProperty("mapSeed");
        if (reroll.HasValue)
        {
            seedProperty.intValue = reroll.Value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var seed = seedProperty.intValue;
        var densityScale = so.FindProperty("forestDensity").floatValue;

        var spawns = SpawnPoints(origin, forestSize, spawnInset, spawnSpacing);
        var boxes = Object.FindObjectsByType<ItemBox>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        // 상자 자리와 숲이 같은 씨앗을 쓴다. 하나만 흔들면 상자만 다른 같은 숲이 나온다.
        var state = Random.state;
        Random.InitState(seed);

        var boxPositions = BoxPositions(boxes.Length, origin, forestSize, spawns);
        for (var i = 0; i < boxes.Length; i++)
        {
            // y는 `GroundBox`가 규격에 맞춘다. 여기서는 평면 자리만 정한다.
            var t = boxes[i].transform;
            t.position = new Vector3(boxPositions[i].x, t.position.y, boxPositions[i].z);
        }

        var keepOut = CollectKeepOut(spawns, boxPositions);

        DressBoxes(boxes, origin, forestSize);
        var planted = PlantForest(origin, forestSize, keepOut, spawns, boxPositions, densityScale);

        Random.state = state;

        var scene = director.gameObject.scene;
        PaintGround(scene);
        EditorSceneManager.MarkSceneDirty(scene);

        // 안개 격자는 숲 크기에서 런타임에 유도된다 (`FogOfWar.ApplyGrid`). 여기 적는 것은
        // 저장되는 값이 아니라 "이 숲이면 격자가 이만큼 된다"는 확인용이다 — 숲을 키웠을 때
        // 안개가 따라왔는지 눈으로 보라고 남긴다.
        Debug.Log($"숲 맵 생성: 씨앗 {seed}, 나무·수풀 {planted}개, 상자 {boxes.Length}개. "
                + $"숲 {forestSize.x}x{forestSize.y}, 원점 {origin}. "
                + $"안개 격자는 숲에서 유도된다 (월드 ±{Mathf.Max(forestSize.x, forestSize.y) * 0.5f:F0} + 시야 반경).");
    }

    /// 팀 스폰 자리. `MatchDirector.NightSpawnPosition`과 같은 식이다 — 어긋나면 팀이
    /// 나무 속에서 시작한다.
    static List<Vector3> SpawnPoints(
        Vector3 origin, Vector2 forestSize, float spawnInset, float spawnSpacing)
    {
        var corners = new[]
        {
            new Vector2(-1f, 1f), new Vector2(1f, 1f),
            new Vector2(-1f, -1f), new Vector2(1f, -1f),
        };

        var points = new List<Vector3>();
        foreach (var corner in corners)
        {
            var edge = new Vector3(corner.x * (forestSize.x * 0.5f - spawnInset), 0f,
                                   corner.y * (forestSize.y * 0.5f - spawnInset));
            var inward = new Vector3(-corner.x, 0f, -corner.y).normalized;
            for (var slot = 0; slot < 2; slot++)
                points.Add(origin + edge + inward * (slot * spawnSpacing));
        }

        return points;
    }

    /// 상자를 링별 지터 극좌표로 놓는다. 링 안에서 각도를 균등하게 잘라 하나씩 넣고 반지름과
    /// 각도만 흔든다 — 순수 난수로 뿌리면 한쪽 모서리에 몰려 스폰 자리에 따라 유불리가 갈린다.
    static List<Vector3> BoxPositions(int count, Vector3 origin, Vector2 forestSize, List<Vector3> spawns)
    {
        var placed = new List<Vector3>(count);
        if (count == 0) return placed;

        var radius = Mathf.Min(forestSize.x, forestSize.y) * 0.5f;
        var counts = ShareOut(count);

        for (var ring = 0; ring < BoxRings.Length; ring++)
        {
            var band = BoxRings[ring];
            var slots = counts[ring];
            var sector = slots > 0 ? Mathf.PI * 2f / slots : 0f;

            for (var slot = 0; slot < slots; slot++)
            {
                var best = Vector3.zero;
                var bestGap = float.MinValue;

                for (var attempt = 0; attempt < BoxPlaceAttempts; attempt++)
                {
                    var angle = (slot + Random.value) * sector;
                    var r = (band.Centre + Random.Range(-band.Jitter, band.Jitter)) * radius;
                    var candidate = origin + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);

                    var gap = NearestGap(candidate, placed, spawns);
                    if (gap > bestGap)
                    {
                        bestGap = gap;
                        best = candidate;
                    }

                    if (gap >= BoxSeparation) break;
                }

                placed.Add(best);
            }
        }

        return placed;
    }

    /// 상자 수를 링 비중대로 나눈다. 나머지는 바깥 링부터 채운다 — 바깥이 넓어 자리가 남는다.
    static int[] ShareOut(int total)
    {
        var weight = 0;
        foreach (var ring in BoxRings) weight += ring.Share;

        var counts = new int[BoxRings.Length];
        var assigned = 0;
        for (var i = 0; i < BoxRings.Length; i++)
        {
            counts[i] = total * BoxRings[i].Share / weight;
            assigned += counts[i];
        }

        for (var i = BoxRings.Length - 1; assigned < total; i = i > 0 ? i - 1 : BoxRings.Length - 1)
        {
            counts[i]++;
            assigned++;
        }

        return counts;
    }

    /// 이 자리가 이미 놓인 상자·스폰에서 얼마나 떨어져 있는지. 스폰은 `SpawnClearance`만큼
    /// 먼저 깎아 같은 잣대로 비교한다 — 스폰 위에 상자를 놓으면 시작하자마자 하나가 공짜다.
    static float NearestGap(Vector3 candidate, List<Vector3> placed, List<Vector3> spawns)
    {
        var gap = float.MaxValue;
        foreach (var other in placed) gap = Mathf.Min(gap, Flat(candidate - other).magnitude);
        foreach (var spawn in spawns)
            gap = Mathf.Min(gap, Flat(candidate - spawn).magnitude - SpawnClearance);
        return gap;
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    /// 인스턴싱에 필요한 조각 하나. 팩 모델은 서브메시 하나짜리지만, 서브메시마다 다른
    /// 머티리얼을 쓰는 모델이 들어와도 그대로 돌도록 이 단위로 배치를 나눈다.
    readonly struct ModelPart
    {
        public readonly Mesh Mesh;
        public readonly int Submesh;
        public readonly Material Material;

        /// 모델 안에서 메시가 놓인 로컬 오프셋.
        public readonly Vector3 LocalOffset;

        public ModelPart(Mesh mesh, int submesh, Material material, Vector3 localOffset)
        {
            Mesh = mesh;
            Submesh = submesh;
            Material = material;
            LocalOffset = localOffset;
        }
    }

    /// 모델을 인스턴싱용 조각으로 편다. 머티리얼은 팩 것을 그대로 쓰되 인스턴싱 사본으로 바꾼다.
    static List<ModelPart> PartsOf(GameObject model, Dictionary<Material, Material> copies)
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
                var source = submesh < shared.Length ? shared[submesh] : null;
                if (source == null) continue;

                if (!copies.TryGetValue(source, out var material))
                {
                    material = EnsureInstancedCopy(source);
                    copies[source] = material;
                }

                if (material == null) continue;
                parts.Add(new ModelPart(filter.sharedMesh, submesh, material, offset));
            }
        }

        return parts;
    }

    /// 팩 머티리얼의 프로젝트 소유 사본을 만든다. 색·텍스처는 그대로 두고 GPU 인스턴싱만 켠다.
    ///
    /// 원본을 직접 고치지 않는 이유는 두 가지다. 서드파티 원본은 건드리지 않는 것이 규칙이고
    /// (AGENTS.md), 팩을 다시 임포트하면 플래그가 조용히 되돌아간다. 인스턴싱이 꺼진
    /// 머티리얼을 `RenderMeshInstanced`에 넘기면 예외가 URP 프레임을 죽여 화면이 하얘진다.
    ///
    /// 사본은 매번 원본 속성을 다시 받는다 — 팩 쪽 색이 바뀌면 다음 굽기에 따라온다.
    static Material EnsureInstancedCopy(Material source)
    {
        var sourcePath = AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(sourcePath))
        {
            Debug.LogWarning($"머티리얼의 에셋 경로를 찾지 못했다: {source.name}");
            return null;
        }

        if (!AssetDatabase.IsValidFolder(ForestMaterialFolder.TrimEnd('/')))
            AssetDatabase.CreateFolder(MaterialFolder.TrimEnd('/'), "Forest");

        var path = ForestMaterialFolder + source.name + ".mat";
        var copy = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (copy == null)
        {
            if (!AssetDatabase.CopyAsset(sourcePath, path))
            {
                Debug.LogError($"머티리얼 사본을 만들지 못했다: {sourcePath} -> {path}");
                return null;
            }

            copy = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (copy == null) return null;
        }
        else copy.CopyPropertiesFromMaterial(source);

        copy.enableInstancing = true;
        EditorUtility.SetDirty(copy);
        return copy;
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
    static List<KeepOut> CollectKeepOut(List<Vector3> spawns, List<Vector3> boxes)
    {
        var keepOut = new List<KeepOut>();
        foreach (var box in boxes) keepOut.Add(new KeepOut(box, BoxClearance));
        foreach (var spawn in spawns) keepOut.Add(new KeepOut(spawn, SpawnClearance));
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

    /// 상자에 링 가중치와 등급별 겉모습을 채운다. 자리는 `PlaceBoxes`가 이미 정했다.
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

            var w = ForestRings.Weights(ForestRings.ZoneOf(ratio), 1);   // 편집 화면용 1일차 값
            var weights = new Vector3Int(w.T1, w.T2, w.T3);

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

    /// 심기로 정한 것 하나. 그림과 콜라이더가 같은 목록에서 나와야 한다 — 통로를 뚫느라
    /// 지운 나무가 그림에 남으면 허공에서 막히거나 없는 나무를 뚫고 지나간다.
    struct Prop
    {
        public GameObject Model;
        public Vector3 World;
        public float Yaw;
        public float Scale;

        /// 0이면 콜라이더를 달지 않는다 (풀·덤불).
        public float Radius;
    }

    /// 나무와 수풀을 지터 격자에 뿌린다. 씬을 건드리지 않으므로 씨앗 훑기(`VerifySeeds`)가
    /// 같은 결과를 다시 만들어 볼 수 있다.
    static List<Prop> Scatter(Vector3 origin, Vector2 forestSize, List<KeepOut> keepOut,
                              List<GameObject> trees, List<GameObject> undergrowth,
                              Dictionary<GameObject, float> baseRadii, float densityScale)
    {
        var halfX = forestSize.x * 0.5f;
        var halfZ = forestSize.y * 0.5f;
        var radius = Mathf.Min(halfX, halfZ);
        var props = new List<Prop>();

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
            var density = Mathf.Lerp(InnerDensity, OuterDensity, ratio) * densityScale;
            if (Random.value > density) continue;

            // 넷에 하나 정도는 나무 대신 낮은 수풀을 놓아 눈높이를 흔든다.
            var pool = undergrowth.Count > 0 && Random.value < 0.25f ? undergrowth : trees;
            var model = pool[Random.Range(0, pool.Count)];
            var scale = TreeScale * Random.Range(1f - TreeScaleJitter, 1f + TreeScaleJitter);

            props.Add(new Prop
            {
                Model = model,
                World = world,
                Yaw = Random.Range(0f, 360f),
                Scale = scale,
                Radius = baseRadii[model] <= 0f ? 0f
                       : Mathf.Clamp(baseRadii[model] * scale * ColliderFactor,
                                     ColliderMinRadius, ColliderMaxRadius),
            });
        }

        return props;
    }

    static int PlantForest(Vector3 origin, Vector2 forestSize, List<KeepOut> keepOut,
                           List<Vector3> spawns, List<Vector3> boxes, float densityScale)
    {
        // 예전에는 프리팹 인스턴스 259개를 씬에 구웠다. 씬 오브젝트로 두면 SRP Batcher가
        // 먼저 잡아서 GPU 인스턴싱이 동작하지 않고, 풀을 깔면 드로우콜이 수천 개가 된다.
        var old = GameObject.Find(ForestRootName);
        if (old != null) Object.DestroyImmediate(old);

        var root = new GameObject(ForestRootName, typeof(ForestInstances), typeof(ForestGrass));
        root.transform.position = origin;
        SetupGrass(root.GetComponent<ForestGrass>(), origin, forestSize);

        var copies = new Dictionary<Material, Material>();
        var trees = LoadModels(ForestModels, TreeModels);
        var undergrowth = LoadModels(ForestModels, UndergrowthModels);
        if (trees.Count == 0)
        {
            Debug.LogError($"{ForestModels}에서 나무 모델을 하나도 찾지 못했다. "
                         + "AssetStore의 Supercyan Free Forest Sample이 임포트되었는지 확인한다.");
            return 0;
        }

        var parts = new Dictionary<GameObject, List<ModelPart>>();
        var baseRadii = new Dictionary<GameObject, float>();
        foreach (var model in trees) parts[model] = PartsOf(model, copies);
        foreach (var model in undergrowth) if (!parts.ContainsKey(model)) parts[model] = PartsOf(model, copies);
        foreach (var model in parts.Keys) baseRadii[model] = BaseRadius(model);

        var props = Scatter(origin, forestSize, keepOut, trees, undergrowth, baseRadii, densityScale);
        var carved = OpenPassages(props, origin, forestSize, spawns, boxes);
        var planted = props.Count;

        // --- 배치로 굽는다 ---
        var collected = new Dictionary<BatchKey, List<Placement>>();

        foreach (var prop in props)
        foreach (var part in parts[prop.Model])
        {
            var key = new BatchKey(part.Mesh, part.Submesh, part.Material, castShadows: true);
            if (!collected.TryGetValue(key, out var list))
            {
                list = new List<Placement>();
                collected[key] = list;
            }

            list.Add(new Placement
            {
                Position = prop.World + part.LocalOffset * prop.Scale,
                Yaw = prop.Yaw,
                Scale = prop.Scale,
            });
        }

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

        var colliders = BakeColliders(root.transform, props);

        Debug.Log($"인스턴싱: 배치 {batches.Length}개 · 나무/수풀 {planted}개 (GameObject 0개) · "
                + $"콜라이더 {colliders}개 · 통로 내느라 걷어낸 {carved}개. "
                + "바닥 풀은 ForestGrass가 컴퓨트로 뽑는다.");
        return planted;
    }

    /// 스케일 1 기준 모델의 XZ 반경. 콜라이더를 달지 않는 모델은 0을 준다.
    static float BaseRadius(GameObject model)
    {
        foreach (var name in NoColliderModels)
            if (model.name == name) return 0f;

        var has = false;
        var bounds = new Bounds();

        foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;

            var b = filter.sharedMesh.bounds;
            b.center += filter.transform.localPosition;

            if (!has)
            {
                bounds = b;
                has = true;
            }
            else bounds.Encapsulate(b);
        }

        return has ? Mathf.Max(bounds.extents.x, bounds.extents.z) : 0f;
    }

    /// 콜라이더를 오브젝트 하나에 캡슐 여러 개로 단다. 나무마다 GameObject를 두면 씬 파일이
    /// 세 배로 부풀고 하이어라키가 수백 줄이 된다. 렌더러가 없으므로 GPU 인스턴싱을 위해
    /// GameObject를 버린 이유(`ForestInstances`)와는 상관이 없다 — 그쪽은 SRP Batcher 얘기다.
    static int BakeColliders(Transform parent, List<Prop> props)
    {
        var holder = new GameObject(CollidersChildName);
        holder.transform.SetParent(parent, worldPositionStays: false);
        holder.transform.localPosition = Vector3.zero;
        holder.transform.localRotation = Quaternion.identity;
        holder.transform.localScale = Vector3.one;

        var count = 0;
        foreach (var prop in props)
        {
            if (prop.Radius <= 0f) continue;

            var capsule = holder.AddComponent<CapsuleCollider>();
            capsule.direction = 1;   // Y축. 중력이 없어 충돌은 사실상 평면 위의 원이다
            capsule.radius = prop.Radius;
            capsule.height = ColliderHeight;
            capsule.center = holder.transform.InverseTransformPoint(
                new Vector3(prop.World.x, GroundY + ColliderHeight * 0.5f, prop.World.z));
            count++;
        }

        return count;
    }

    /// 스폰에서 모든 상자에 갈 수 있는지 확인하고, 막혀 있으면 통로를 낸다. 걷어낸 나무 수를
    /// 돌려준다.
    ///
    /// 콜라이더가 생기면서 밀도 0.78인 중심부가 통째로 벽이 되는 씨앗이 나온다. 그러면
    /// 3등급이 몰린 중심(기획서 6.3)에 아무도 닿지 못해 판이 성립하지 않는다.
    /// ponytail: 막힌 상자마다 직선 하나를 뚫는 최소 수리다. 통로가 부자연스러우면 그때
    /// 경로를 격자 A*로 바꾼다.
    static int OpenPassages(List<Prop> props, Vector3 origin, Vector2 forestSize,
                            List<Vector3> spawns, List<Vector3> boxes)
    {
        var removed = 0;

        for (var pass = 0; pass <= RepairPasses; pass++)
        {
            var grid = Walkable(props, origin, forestSize, spawns);
            var blocked = FirstUnreachable(grid, boxes);
            if (!blocked.HasValue) return removed;

            if (pass == RepairPasses)
            {
                Debug.LogError($"통로를 {RepairPasses}번 뚫었는데도 {blocked.Value}의 상자에 "
                             + "닿지 못한다. 씨앗을 바꾸거나 밀도를 낮춘다.");
                return removed;
            }

            removed += Carve(props, grid, blocked.Value);
        }

        return removed;
    }

    /// 플레이어가 설 수 있는 칸과, 스폰에서 걸어 닿는 칸.
    sealed class ReachGrid
    {
        public Vector3 Corner;      // (0,0) 칸의 월드 좌표
        public int Nx;
        public int Nz;
        public bool[] Reached;

        public int Index(Vector3 world)
        {
            var x = Mathf.Clamp(Mathf.RoundToInt((world.x - Corner.x) / ReachCell), 0, Nx - 1);
            var z = Mathf.Clamp(Mathf.RoundToInt((world.z - Corner.z) / ReachCell), 0, Nz - 1);
            return z * Nx + x;
        }

        public Vector3 Centre(int index) =>
            new(Corner.x + index % Nx * ReachCell, 0f, Corner.z + index / Nx * ReachCell);
    }

    static ReachGrid Walkable(List<Prop> props, Vector3 origin, Vector2 forestSize, List<Vector3> spawns)
    {
        var grid = new ReachGrid
        {
            Corner = new Vector3(origin.x - forestSize.x * 0.5f, 0f, origin.z - forestSize.y * 0.5f),
            Nx = Mathf.CeilToInt(forestSize.x / ReachCell) + 1,
            Nz = Mathf.CeilToInt(forestSize.y / ReachCell) + 1,
        };
        grid.Reached = new bool[grid.Nx * grid.Nz];

        var free = new bool[grid.Reached.Length];
        for (var i = 0; i < free.Length; i++) free[i] = true;

        // 나무마다 자기가 막는 칸만 훑는다. 칸마다 나무 전체를 재면 곱셈이 수백만 번이 된다.
        foreach (var prop in props)
        {
            if (prop.Radius <= 0f) continue;

            var block = prop.Radius + PlayerRadius;
            var steps = Mathf.CeilToInt(block / ReachCell);
            var centre = grid.Index(prop.World);
            var cx = centre % grid.Nx;
            var cz = centre / grid.Nx;

            for (var dz = -steps; dz <= steps; dz++)
            for (var dx = -steps; dx <= steps; dx++)
            {
                var x = cx + dx;
                var z = cz + dz;
                if (x < 0 || z < 0 || x >= grid.Nx || z >= grid.Nz) continue;

                var index = z * grid.Nx + x;
                if (Flat(grid.Centre(index) - prop.World).sqrMagnitude > block * block) continue;
                free[index] = false;
            }
        }

        // 스폰 칸은 `SpawnClearance`로 비워 둔 자리라 무조건 열려 있다.
        var queue = new Queue<int>();
        foreach (var spawn in spawns)
        {
            var index = grid.Index(spawn);
            if (grid.Reached[index]) continue;

            grid.Reached[index] = true;
            queue.Enqueue(index);
        }

        // 대각선은 열지 않는다. 나무 두 그루가 대각으로 맞닿은 틈은 사람이 못 지나간다.
        while (queue.Count > 0)
        {
            var index = queue.Dequeue();
            var x = index % grid.Nx;
            var z = index / grid.Nx;

            for (var side = 0; side < 4; side++)
            {
                var nx = x + (side == 0 ? 1 : side == 1 ? -1 : 0);
                var nz = z + (side == 2 ? 1 : side == 3 ? -1 : 0);
                if (nx < 0 || nz < 0 || nx >= grid.Nx || nz >= grid.Nz) continue;

                var next = nz * grid.Nx + nx;
                if (grid.Reached[next] || !free[next]) continue;

                grid.Reached[next] = true;
                queue.Enqueue(next);
            }
        }

        return grid;
    }

    static Vector3? FirstUnreachable(ReachGrid grid, List<Vector3> boxes)
    {
        foreach (var box in boxes)
            if (!grid.Reached[grid.Index(box)]) return box;

        return null;
    }

    /// 막힌 상자에서 가장 가까운 열린 칸까지 직선으로 나무를 걷어낸다. 격자 한 칸만큼 넉넉히
    /// 비우지 않으면 통로가 뚫려도 칸 중심이 막힌 채로 남아 플러드 필이 지나가지 못한다.
    static int Carve(List<Prop> props, ReachGrid grid, Vector3 from)
    {
        var target = from;
        var best = float.MaxValue;

        for (var i = 0; i < grid.Reached.Length; i++)
        {
            if (!grid.Reached[i]) continue;

            var centre = grid.Centre(i);
            var distance = Flat(centre - from).sqrMagnitude;
            if (distance >= best) continue;

            best = distance;
            target = centre;
        }

        var removed = 0;
        for (var i = props.Count - 1; i >= 0; i--)
        {
            var prop = props[i];
            if (prop.Radius <= 0f) continue;

            var clearance = prop.Radius + PlayerRadius + ReachCell;
            if (DistanceToSegment(Flat(prop.World), Flat(from), Flat(target)) > clearance) continue;

            props.RemoveAt(i);
            removed++;
        }

        return removed;
    }

    static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        var ab = b - a;
        var length = ab.sqrMagnitude;
        var t = length > 0f ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / length) : 0f;
        return (point - (a + ab * t)).magnitude;
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

    /// 팩 프리팹을 읽는다. FBX가 아니라 프리팹인 이유는 머티리얼이다 — 팩의 High Quality
    /// 머티리얼은 프리팹 쪽에 물려 있고, FBX를 직접 읽으면 임포터가 만든 것이 딸려 온다.
    static List<GameObject> LoadModels(string folder, string[] names)
    {
        var loaded = new List<GameObject>();
        foreach (var name in names)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(folder + name + ".prefab");
            if (model != null) loaded.Add(model);
            else Debug.LogWarning($"모델을 찾지 못했다: {folder}{name}.prefab");
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
