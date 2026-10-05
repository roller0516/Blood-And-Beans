using System.Collections.Generic;
using System.Linq;
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
/// 나무는 `NetworkObject`가 없는 순수 표현이라 씬의 Terrain에 트리 인스턴스로 굽는다.
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

    /// 평균 크기(`TreeScale`) 나무의 반경을 이 범위로 묶는다. 터레인 트리 콜라이더는 나무 크기에
    /// 비례해서만 커지므로, 묶음은 원형 하나에 한 번 걸고 지터(`TreeScaleJitter`)만큼은 비례로 둔다.
    const float ColliderMinRadius = 0.35f;
    const float ColliderMaxRadius = 1.1f;

    /// 세워 둔 캡슐 하나로 나무를 대신한다. 중력이 없고(PlayerMove) 플레이어 y가 고정이라
    /// 충돌은 사실상 평면 위의 원이다. 평균 크기 나무 기준 높이다.
    /// ponytail: `log`처럼 누운 모델도 원으로 근사한다. 실루엣이 어긋나 보이면 그때 박스로 바꾼다.
    const float ColliderHeight = 3f;

    /// 지나갈 수 있어야 하는 것들. 기획서 6.2가 수풀을 은폐물로 쓰므로 몸으로 막지 않는다.
    static readonly string[] NoColliderModels =
    {
        "forestpack_foliage_mushroom_blue_big", "forestpack_foliage_mushroom_red_small",
    };

    /// 숲 가장자리의 보이지 않는 벽. 중력이 없어 땅이 끝나도 떨어지지 않으므로 이것이 없으면
    /// 숲 밖 허공으로 걸어 나간다. 플레이어 높이(2)를 넉넉히 덮으면 된다.
    const string BoundaryChildName = "Boundary";
    const float BoundaryHeight = 4f;
    const float BoundaryThickness = 1f;

    /// 벽 너머를 가리는 안개 원통과 구름 띠. 안개는 소프트 파티클 깊이 페이드라 뒤가 허공이면
    /// 꽉 찬 색이 되고, 구름은 그 앞에서 모양을 낸다. 플레이어는 숲에 밤에만, 광장에 낮에만 있으므로
    /// 색은 장소마다 고정이다 — 페이즈 따라 바꾸는 코드가 필요 없다.
    const string BoundaryVfxFolder = "Assets/Art/VFX/Shared/";
    const string BoundaryCloudPrefabPath = BoundaryVfxFolder + "Prefabs/BoundaryClouds.prefab";
    static string BoundaryFogPath(bool night) => BoundaryVfxFolder + $"Materials/BoundaryFog_{(night ? "Night" : "Day")}.mat";
    static string BoundaryCloudPath(bool night) => BoundaryVfxFolder + $"Materials/BoundaryCloud_{(night ? "Night" : "Day")}.mat";

    /// 안개는 경계를 한 바퀴 두르는 타원 원통 하나다. 변마다 상자를 두면 모서리에서 따로 놀아 보인다.
    /// 안쪽을 향한 면만 있고 뚜껑이 없다 — 뚜껑이 있으면 올려다볼 때 하늘이 막힌다.
    /// 반지름은 직사각형 네 모서리를 지나는 타원(반축 × √2)에 벽 두께를 더한 값이다.
    /// 아래로 깊게, 위로 높게 둬서 윗선·아랫선이 화면에 들어오지 않게 한다.
    const string BoundaryRingMeshPath = BoundaryVfxFolder + "Meshes/BoundaryRing.asset";
    const int BoundaryRingSegments = 64;
    const float BoundaryFogBottom = -30f;
    const float BoundaryFogHeight = 400f;

    /// 구름 띠는 안개 원통과 같은 타원을 따라 원통 면 바로 안쪽에 한 바퀴 두른다. 면 뒤로 가면
    /// 불투명한 안개에 가려진다. 프리팹(`BoundaryClouds`)의 밀도는 길이 `BoundaryCloudReferenceLength`
    /// 기준이라 둘레에 비례해 늘린다.
    const float BoundaryCloudHeight = 2.5f;
    const float BoundaryCloudReferenceLength = 100f;
    const float BoundaryCloudInset = 3f;          // 원통 면에서 안쪽으로 물린 거리
    const float BoundaryCloudTube = 2f;           // 띠의 두께(높이·깊이) 반경

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

    /// 터레인 설정. **도구가 소유한다** — 매번 새로 만들므로 Inspector에서 고친 값은 다음 굽기에
    /// 되돌아간다. 지면은 평평하고(중력 없음, `GroundY`) 레이어는 하나뿐이라 해상도를 최소로 둔다.
    const string TerrainFolder = "Assets/Art/Environment/Terrain/";
    const float TerrainHeight = 1f;               // 높이가 없어 경계 상자 두께로만 쓰인다

    const int TerrainHeightmapResolution = 33;    // Unity 최솟값
    const int TerrainAlphamapResolution = 16;     // Unity 최솟값. 레이어 하나라 칠할 것이 없다
    const int TerrainBaseMapResolution = 16;

    /// 평지라 LOD가 틀어질 높이가 없다. 최댓값으로 두어 패치를 가장 거칠게 쓴다.
    const float TerrainPixelError = 200f;

    /// LODGroup이 없는 팩 모델에 붙일 컬링 높이. 팩의 다른 모델(돌·그루터기)이 쓰는 값과 맞춘다.
    const float PackLodCullHeight = 0.03f;

    /// 예전 바닥(Plane)의 머티리얼. 텍스처·타일링·매끄러움을 TerrainLayer로 옮겨 겉모습을 유지한다.
    const string GroundMaterialPath =
        "Assets/AssetStore/Supercyan Free Forest Sample/Textures/Ground/Materials/forestpack_moss_light_tile_diffuse.mat";

    /// 숲의 나무·수풀. Supercyan Free Forest Sample의 High Quality 프리팹을 쓴다.
    /// Mobile 쪽은 같은 메시에 저해상도 텍스처라 탑다운에서 흐리게 뭉친다.
    const string ForestModels = "Assets/AssetStore/Supercyan Free Forest Sample/Prefabs/High Quality/";
    const string SurvivalModels = "Assets/AssetStore/Kenney/survival-kit/Models/FBX format/";
    const string MaterialFolder = "Assets/Art/Environment/Materials/";
    const string ForestMaterialFolder = MaterialFolder + "Forest/";

    /// 숲 머티리얼은 팩의 High Quality `.mat`을 그대로 쓴다. Supercyan은 텍스처가 있는
    /// 팩이라 단색으로 덮으면 나무가 덩어리가 된다 — 색도 텍스처도 건드리지 않는다.
    /// 인스턴싱 플래그만 켜야 해서 프로젝트 소유 사본을 두고, 그것을 물린 프리팹 변형을
    /// 터레인 트리 원형으로 쓴다 (`EnsureInstancedCopy`, `TreePrefab`).

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

        var radii = new Dictionary<GameObject, float>();
        foreach (var model in trees.Concat(undergrowth)) radii[model] = LocalRadius(model);

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
                                trees, undergrowth, radii, densityScale);

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

        var scene = director.gameObject.scene;
        DressBoxes(boxes, origin, forestSize);
        var planted = PlantForest(scene, origin, forestSize, director.GroundSize, keepOut, spawns, boxPositions, densityScale);

        Random.state = state;

        EditorSceneManager.MarkSceneDirty(scene);

        // 안개 격자는 숲 크기에서 런타임에 유도된다 (`FogOfWar.ApplyGrid`). 여기 적는 것은
        // 저장되는 값이 아니라 "이 숲이면 격자가 이만큼 된다"는 확인용이다 — 숲을 키웠을 때
        // 안개가 따라왔는지 눈으로 보라고 남긴다.
        Debug.Log($"숲 맵 생성: 씨앗 {seed}, 나무·수풀 {planted}개, 상자 {boxes.Length}개. "
                + $"숲 {forestSize.x}x{forestSize.y}, 원점 {origin}. "
                + $"안개 격자는 터레인에서 유도된다 (월드 ±{Mathf.Max(director.GroundSize.x, director.GroundSize.y) * 0.5f:F0} + 시야 반경).");
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

    /// 터레인 트리 원형으로 쓸 프리팹 변형을 만든다. 팩 프리팹의 머티리얼을 인스턴싱 사본으로
    /// 바꾸고, 팩 콜라이더를 `LocalRadius` 캡슐 하나로 갈아 끼운다. TerrainCollider가 이 캡슐을
    /// 나무마다 크기에 맞춰 세운다 — 통로 검사(`Prop.Radius`)와 같은 반경이어야 한다.
    static GameObject TreePrefab(GameObject model, Dictionary<Material, Material> copies)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        try
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var shared = renderer.sharedMaterials;
                for (var i = 0; i < shared.Length; i++)
                {
                    if (shared[i] == null) continue;
                    if (!copies.TryGetValue(shared[i], out var copy))
                    {
                        copy = EnsureInstancedCopy(shared[i]);
                        copies[shared[i]] = copy;
                    }
                    if (copy != null) shared[i] = copy;
                }
                renderer.sharedMaterials = shared;
            }

            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);

            var radius = LocalRadius(model);
            if (radius > 0f)
            {
                var capsule = instance.AddComponent<CapsuleCollider>();
                capsule.direction = 1;   // Y축
                capsule.radius = radius;
                capsule.height = ColliderHeight / TreeScale;
                capsule.center = Vector3.up * (capsule.height * 0.5f);
            }

            // LODGroup이 없는 원형은 터레인이 빌보드 나무로 취급해 Soft Occlusion 셰이더를 요구한다.
            if (instance.GetComponent<LODGroup>() == null)
                instance.AddComponent<LODGroup>().SetLODs(new[]
                {
                    new LOD(PackLodCullHeight, instance.GetComponentsInChildren<Renderer>(true)),
                });

            return PrefabUtility.SaveAsPrefabAsset(instance, TerrainFolder + model.name + ".prefab");
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
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

    /// 숲 바닥 레이어. 예전 Plane(UV 0~1이 숲 전체)과 같은 겉모습이 되도록 타일 크기를
    /// 숲 크기 ÷ 머티리얼 타일링으로 잡는다.
    static TerrainLayer GroundLayer(Vector2 forestSize)
    {
        var source = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
        if (source == null)
        {
            Debug.LogError($"바닥 머티리얼을 찾지 못했다: {GroundMaterialPath}");
            return null;
        }

        var path = TerrainFolder + "ForestGround.terrainlayer";
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, path);
        }

        var tiling = source.mainTextureScale;
        layer.diffuseTexture = source.mainTexture as Texture2D;
        layer.tileSize = new Vector2(forestSize.x / tiling.x, forestSize.y / tiling.y);
        layer.metallic = 0f;
        layer.smoothness = source.HasProperty("_Smoothness") ? source.GetFloat("_Smoothness") : 0f;
        EditorUtility.SetDirty(layer);
        return layer;
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
                              Dictionary<GameObject, float> radii, float densityScale)
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
                Radius = radii[model] * scale,
            });
        }

        return props;
    }

    static int PlantForest(Scene scene, Vector3 origin, Vector2 forestSize, Vector2 groundSize, List<KeepOut> keepOut,
                           List<Vector3> spawns, List<Vector3> boxes, float densityScale)
    {
        var old = GameObject.Find(ForestRootName);
        if (old != null) Object.DestroyImmediate(old);

        var trees = LoadModels(ForestModels, TreeModels);
        var undergrowth = LoadModels(ForestModels, UndergrowthModels);
        if (trees.Count == 0)
        {
            Debug.LogError($"{ForestModels}에서 나무 모델을 하나도 찾지 못했다. "
                         + "AssetStore의 Supercyan Free Forest Sample이 임포트되었는지 확인한다.");
            return 0;
        }

        if (!AssetDatabase.IsValidFolder(TerrainFolder.TrimEnd('/')))
            AssetDatabase.CreateFolder("Assets/Art/Environment", "Terrain");

        var copies = new Dictionary<Material, Material>();
        var prototypeIndex = new Dictionary<GameObject, int>();
        var prototypes = new List<TreePrototype>();
        var radii = new Dictionary<GameObject, float>();
        foreach (var model in trees.Concat(undergrowth))
        {
            if (prototypeIndex.ContainsKey(model)) continue;
            prototypeIndex[model] = prototypes.Count;
            prototypes.Add(new TreePrototype { prefab = TreePrefab(model, copies) });
            radii[model] = LocalRadius(model);
        }

        var props = Scatter(origin, forestSize, keepOut, trees, undergrowth, radii, densityScale);
        var carved = OpenPassages(props, origin, forestSize, spawns, boxes);

        var corner = new Vector3(origin.x - groundSize.x * 0.5f, GroundY, origin.z - groundSize.y * 0.5f);
        var data = BakeTerrainData(scene, groundSize, GroundLayer(forestSize));
        data.treePrototypes = prototypes.ToArray();

        var instances = new TreeInstance[props.Count];
        for (var i = 0; i < props.Count; i++)
        {
            var prop = props[i];
            instances[i] = new TreeInstance
            {
                position = new Vector3((prop.World.x - corner.x) / groundSize.x, 0f,
                                       (prop.World.z - corner.z) / groundSize.y),
                widthScale = prop.Scale,
                heightScale = prop.Scale,
                rotation = prop.Yaw * Mathf.Deg2Rad,
                color = Color.white,
                lightmapColor = Color.white,
                prototypeIndex = prototypeIndex[prop.Model],
            };
        }
        data.SetTreeInstances(instances, snapToHeightmap: true);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        var root = Terrain.CreateTerrainGameObject(data);
        root.name = ForestRootName;
        root.transform.position = corner;
        SetupTerrain(root.GetComponent<Terrain>(), forestSize);
        // 터레인 원점이 여유분 바깥 모서리라 숲은 로컬 margin~margin+forestSize다.
        BuildBoundary(root.transform, new Vector3(groundSize.x * 0.5f, 0f, groundSize.y * 0.5f), forestSize, night: true);

        var colliders = props.Count(prop => prop.Radius > 0f);

        Debug.Log($"터레인: 트리 원형 {prototypes.Count}종 · 나무/수풀 {props.Count}개 · "
                + $"트리 콜라이더 {colliders}개 · 통로 내느라 걷어낸 {carved}개.");
        return props.Count;
    }

    /// `center`(부모 로컬) 둘레 `size` 직사각형의 네 변에 벽을, 그 바깥에 안개 원통·구름 띠를 세운다. 벽 안쪽 면이
    /// 경계에 맞닿는다. 다시 부르면 이전 것을 지우고 새로 세운다. 광장(`DayV5Setup`)도 같은 것을 쓴다.
    internal static void BuildBoundary(Transform parent, Vector3 center, Vector2 size, bool night)
    {
        var old = parent.Find(BoundaryChildName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var holder = new GameObject(BoundaryChildName);
        holder.transform.SetParent(parent, false);
        holder.layer = parent.gameObject.layer;

        var fog = AssetDatabase.LoadAssetAtPath<Material>(BoundaryFogPath(night));
        var cloud = AssetDatabase.LoadAssetAtPath<Material>(BoundaryCloudPath(night));
        var clouds = AssetDatabase.LoadAssetAtPath<ParticleSystem>(BoundaryCloudPrefabPath);
        if (fog == null || cloud == null || clouds == null)
            Debug.LogError($"경계 안개·구름 에셋이 없어 벽만 세운다. {BoundaryVfxFolder} 아래를 확인한다.");

        // (바깥 방향, 그 방향의 폭, 변 길이)
        var sides = new[]
        {
            (Vector3.left, size.x, size.y), (Vector3.right, size.x, size.y),
            (Vector3.back, size.y, size.x), (Vector3.forward, size.y, size.x),
        };
        foreach (var (outward, across, length) in sides)
        {
            var edge = center + outward * (across * 0.5f);
            var facing = Quaternion.LookRotation(outward);   // 로컬 z가 바깥, x가 변을 따라간다

            var wall = holder.AddComponent<BoxCollider>();
            wall.center = edge + outward * (BoundaryThickness * 0.5f) + Vector3.up * (BoundaryHeight * 0.5f);
            wall.size = Abs(facing * new Vector3(length + BoundaryThickness * 2f, BoundaryHeight, BoundaryThickness));
        }

        if (fog != null) AddFogRing(holder.transform, center, size, fog);
        if (cloud != null && clouds != null) AddCloudRing(holder.transform, center, size, clouds, cloud);
    }

    /// 안개 원통의 타원 반축(x, z). 원통과 구름 띠가 같은 타원을 쓴다.
    static Vector2 RingRadii(Vector2 size) =>
        size * (0.5f * Mathf.Sqrt(2f)) + Vector2.one * BoundaryThickness;

    static Vector3 Abs(Vector3 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    /// 경계를 두르는 안개 원통. 단위 원통 메시를 타원 반축·높이로 늘린다.
    static void AddFogRing(Transform holder, Vector3 center, Vector2 size, Material material)
    {
        var ring = new GameObject("Fog", typeof(MeshFilter), typeof(MeshRenderer));
        ring.layer = holder.gameObject.layer;
        ring.transform.SetParent(holder, false);
        ring.transform.localPosition = center + Vector3.up * BoundaryFogBottom;
        var radii = RingRadii(size);
        ring.transform.localScale = new Vector3(radii.x, BoundaryFogHeight, radii.y);
        ring.GetComponent<MeshFilter>().sharedMesh = RingMesh();

        var renderer = ring.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    /// 반지름 1, 높이 0~1의 뚜껑 없는 원통. 면은 안쪽을 본다. 한 번 만들어 에셋으로 둔다.
    static Mesh RingMesh()
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(BoundaryRingMeshPath);
        if (mesh != null) return mesh;

        var vertices = new Vector3[(BoundaryRingSegments + 1) * 2];
        var normals = new Vector3[vertices.Length];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[BoundaryRingSegments * 6];
        for (var i = 0; i <= BoundaryRingSegments; i++)
        {
            var angle = i * Mathf.PI * 2f / BoundaryRingSegments;
            var around = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            vertices[i * 2] = around;
            vertices[i * 2 + 1] = around + Vector3.up;
            normals[i * 2] = normals[i * 2 + 1] = -around;
            uv[i * 2] = new Vector2((float)i / BoundaryRingSegments, 0f);
            uv[i * 2 + 1] = new Vector2((float)i / BoundaryRingSegments, 1f);
            if (i == BoundaryRingSegments) break;

            // 안에서 볼 때 시계 방향이 되게 감는다.
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3, t = i * 6;
            triangles[t] = a; triangles[t + 1] = c; triangles[t + 2] = d;
            triangles[t + 3] = a; triangles[t + 4] = d; triangles[t + 5] = b;
        }

        mesh = new Mesh { name = "BoundaryRing", vertices = vertices, normals = normals, uv = uv, triangles = triangles };
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, BoundaryRingMeshPath);
        return mesh;
    }

    /// 구름 띠 프리팹을 안개 원통 안쪽의 타원 도넛으로 편다. 밀도는 프리팹 값을 둘레 비례로 유지한다.
    static void AddCloudRing(Transform holder, Vector3 center, Vector2 size, ParticleSystem prefab, Material material)
    {
        var clouds = (ParticleSystem)PrefabUtility.InstantiatePrefab(prefab, holder);
        clouds.gameObject.layer = holder.gameObject.layer;
        clouds.transform.SetLocalPositionAndRotation(center + Vector3.up * BoundaryCloudHeight, Quaternion.identity);

        var radii = RingRadii(size) - Vector2.one * BoundaryCloudInset;
        var radius = Mathf.Min(radii.x, radii.y);
        // Donut은 shape 로컬 XY 평면이다. x축으로 90° 눕히면 scale.x가 월드 x, scale.y가 월드 z 반경을 늘린다.
        var shape = clouds.shape;
        shape.shapeType = ParticleSystemShapeType.Donut;
        shape.radius = radius;
        shape.donutRadius = BoundaryCloudTube;
        shape.radiusThickness = 1f;
        shape.arc = 360f;
        shape.rotation = new Vector3(90f, 0f, 0f);
        shape.scale = new Vector3(radii.x / radius, radii.y / radius, 1f);

        // 프리팹의 x 흐름은 직선 띠 기준이다. 원에서는 둘레를 따라 도는 각속도로 바꿔야 띠를 벗어나지 않는다.
        // 선속도 x·y·z와 궤도 x·y·z는 각각 같은 커브 모드여야 한다 — 전부 두 상수로 맞춘다.
        var velocity = clouds.velocityOverLifetime;
        var drift = velocity.x;
        velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.orbitalY = new ParticleSystem.MinMaxCurve(drift.constantMin / radius, drift.constantMax / radius);
        velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);

        // 타원 둘레 (Ramanujan 근사).
        var (a, b) = (radii.x, radii.y);
        var perimeter = Mathf.PI * (3f * (a + b) - Mathf.Sqrt((3f * a + b) * (a + 3f * b)));
        var ratio = perimeter / BoundaryCloudReferenceLength;
        var main = clouds.main;
        main.maxParticles = Mathf.CeilToInt(main.maxParticles * ratio);
        var emission = clouds.emission;
        emission.rateOverTimeMultiplier *= ratio;

        clouds.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
    }

    /// 씬마다 TerrainData 한 벌. 다시 구우면 같은 에셋을 덮어써 GUID가 유지된다.
    static TerrainData BakeTerrainData(Scene scene, Vector2 groundSize, TerrainLayer ground)
    {
        var path = TerrainFolder + scene.name + "_Forest.asset";
        var data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
        if (data == null)
        {
            data = new TerrainData();
            AssetDatabase.CreateAsset(data, path);
        }

        // 해상도를 먼저 정한다. 해상도를 바꾸면 크기가 다시 맞춰진다.
        data.heightmapResolution = TerrainHeightmapResolution;
        data.size = new Vector3(groundSize.x, TerrainHeight, groundSize.y);
        data.SetHeights(0, 0, new float[TerrainHeightmapResolution, TerrainHeightmapResolution]);

        data.alphamapResolution = TerrainAlphamapResolution;
        data.baseMapResolution = TerrainBaseMapResolution;
        data.terrainLayers = ground != null ? new[] { ground } : new TerrainLayer[0];
        if (ground != null)
        {
            var weights = new float[TerrainAlphamapResolution, TerrainAlphamapResolution, 1];
            for (var z = 0; z < TerrainAlphamapResolution; z++)
            for (var x = 0; x < TerrainAlphamapResolution; x++)
                weights[z, x, 0] = 1f;
            data.SetAlphamaps(0, 0, weights);
        }

        // 디테일(풀)은 쓰지 않는다.
        data.detailPrototypes = new DetailPrototype[0];
        data.SetDetailResolution(0, 8);
        return data;
    }

    /// 탑다운 평지에 맞춘 렌더 설정. 나무는 숲 대각선 안이면 전부 실물로 그리고(절두체 컬링은
    /// 터레인이 나무 단위로 한다), 평지라 그림자를 드리울 일이 없는 바닥은 그림자 패스에서 뺀다.
    static void SetupTerrain(Terrain terrain, Vector2 forestSize)
    {
        var diagonal = forestSize.magnitude;

        terrain.drawInstanced = true;
        terrain.heightmapPixelError = TerrainPixelError;
        terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        terrain.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        terrain.treeDistance = diagonal;
        terrain.treeBillboardDistance = diagonal;
        terrain.detailObjectDistance = 0f;
        terrain.collectDetailPatches = false;
        terrain.allowAutoConnect = false;
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

    /// 크기 1 원형의 콜라이더 반경. 평균 크기 나무에서 `ColliderMinRadius`~`ColliderMaxRadius`가
    /// 되게 잡는다. 0이면 콜라이더를 달지 않는다.
    static float LocalRadius(GameObject model)
    {
        var baseRadius = BaseRadius(model);
        return baseRadius <= 0f ? 0f
             : Mathf.Clamp(baseRadius * TreeScale * ColliderFactor, ColliderMinRadius, ColliderMaxRadius) / TreeScale;
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
