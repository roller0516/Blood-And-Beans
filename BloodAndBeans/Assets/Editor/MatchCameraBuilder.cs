using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

/// 매치 카메라 두 대의 역할을 갈라 세운다. 두 시점의 느낌을 비교하는 것이 목적이므로
/// (`MatchCameraDirector.PlayerView`) 조작 방식까지 서로 달라야 비교가 성립한다.
///
/// - **쿼터뷰**: 마우스가 개입하지 않는다. 궤도 축을 고정한 채 플레이어만 따라간다.
///   숲을 훑는 시점이라 시야를 플레이어가 마음대로 돌리면 "어디가 안 보이는가"라는
///   긴장이 사라진다.
/// - **TPP**: 마우스가 카메라를 돌린다. 어깨 너머 시점이라 돌릴 수 없으면 플레이어
///   뒤통수만 보게 된다.
///
/// 손으로 만들지 않는 이유는 값이 아직 정해지지 않아서다. 궤도 반경·눈높이·감도는 눈으로
/// 보고 고칠 값이고, 고칠 때마다 Inspector의 중첩 배열을 뒤지는 대신 여기 상수를 바꾸고
/// 다시 돌린다. 몇 번을 돌려도 카메라는 두 대뿐이다.
public static class MatchCameraBuilder
{
    const string MenuPath = "Tools/Blood & Beans/매치 카메라 세우기";
    const string TppCameraName = "TppCamera";

    /// 궤도의 중심. 플레이어 원점 기준의 눈높이다.
    static readonly Vector3 TppTargetOffset = new(0f, 1.4f, 0f);

    /// 플레이어에서 카메라까지의 거리.
    const float TppRadius = 3.5f;

    /// 화면에서 플레이어를 한쪽으로 밀어 어깨 너머를 만든다. Aim 뒤에 걸리는 로컬
    /// 오프셋이라 카메라를 돌려도 어깨 쪽이 따라 바뀌지 않는다.
    static readonly Vector3 TppShoulder = new(0.6f, 0f, 0f);

    /// 카메라가 장애물에서 유지하는 거리. 어깨 오프셋이 디오클루더보다 뒤에(Aim 뒤에)
    /// 걸리므로 그 폭보다 넉넉해야 한다 — 벽에 딱 붙여 세우면 옆으로 민 만큼 다시 벽
    /// 안으로 들어간다.
    const float TppCameraRadius = 0.75f;

    /// 카메라를 밀어낼 레이어. Default만 본다. 카페는 팀별 레이어(CafeTeam0~3)에 있고
    /// 남의 팀 카페는 컬링으로 보이지 않는데(TeamVision), 그것까지 넣으면 화면에 없는
    /// 벽이 카메라를 당긴다.
    static readonly LayerMask TppOccluders = 1 << 0;

    /// 위아래로 볼 수 있는 각도. 아래로 조금 넘겨야 발밑의 상자가 보인다.
    static readonly Vector2 TppPitchRange = new(-15f, 55f);
    const float TppPitchStart = 10f;

    /// TPP 화각. 예전에는 쿼터뷰에서 그대로 복사했는데, 쿼터뷰를 멀리서 좁게 잡으면서
    /// 갈라섰다 — 두 시점은 이제 프레이밍 자체가 다르다.
    const float TppFieldOfView = 60f;

    /// 쿼터뷰가 내려다보는 각. 로스트아크처럼 지형과 발밑이 같이 읽히는 높이다.
    const float QuarterPitch = 55f;

    /// 쿼터뷰의 거리. 캐릭터보다 주변 지형이 주인공이 되는 거리다.
    const float QuarterRadius = 16f;

    /// 쿼터뷰 화각. 멀리서 좁게 잡아야 원근이 눌려 쿼터뷰로 읽힌다 — 같은 거리라도
    /// 화각이 넓으면 화면 가장자리가 벌어져 3인칭에 가까워진다.
    const float QuarterFieldOfView = 38f;

    /// 쿼터뷰는 마우스가 없어서 실제로 쓰이는 각은 QuarterPitch 하나뿐이다. 범위는 그
    /// 값이 잘리지 않게만 열어 둔다.
    static readonly Vector2 QuarterPitchRange = new(5f, 80f);

    /// 마우스 입력에 곱해지는 값. 세로가 음수인 것이 반전 없음이다 — 궤도의 세로 축은
    /// 값이 커질수록 카메라가 위로 올라가 내려다보므로(`CinemachineOrbitalFollow`),
    /// 마우스를 올려 위를 보려면 축 값이 줄어야 한다.
    /// 개발 콘솔 「치트 → 마우스 감도」와 설정 팝업에서 배수를 바꿔 보고, 정한 값을 여기 적는다.
    static readonly Vector2 TppLookGain = new(3f, -1.5f);

    [MenuItem(MenuPath)]
    static void Build()
    {
        var director = Object.FindAnyObjectByType<MatchCameraDirector>();
        if (director == null)
        {
            EditorUtility.DisplayDialog("매치 카메라 세우기",
                "열려 있는 씬에 MatchCameraDirector가 없다. 매치 씬(Battle_01)을 먼저 연다.", "확인");
            return;
        }

        var so = new SerializedObject(director);
        var tppProperty = so.FindProperty("tppCamera");
        var nightCamera = so.FindProperty("nightCamera").objectReferenceValue as CinemachineCamera;

        // 쿼터뷰에서 걷어내기 전에 입력 배선을 먼저 챙긴다. Input Action 참조는 씬에만 있고
        // 코드로 다시 만들 수 없다 — 지우고 나면 어느 액션이었는지 알 방법이 없다.
        var inputActions = ReadInputActions(nightCamera);

        var camera = tppProperty.objectReferenceValue as CinemachineCamera;
        if (camera == null) camera = Find(director.transform);
        if (camera == null) camera = Create(director.transform);

        ConfigureTpp(camera, so.FindProperty("idlePriority").intValue, inputActions);
        var frozen = ConfigureQuarter(nightCamera);

        tppProperty.objectReferenceValue = camera;
        so.ApplyModifiedPropertiesWithoutUndo();

        var scene = director.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("매치 카메라 세우기: TPP는 마우스로 돌아가고, 쿼터뷰는 고정했다"
                + (frozen ? " (쿼터뷰의 입력 컨트롤러 제거)" : "")
                + ". 전환은 개발 콘솔 → 치트 → 시점.");
    }

    // ── TPP ───────────────────────────────────────────────────────

    static CinemachineCamera Find(Transform parent)
    {
        var existing = parent.Find(TppCameraName);
        return existing != null ? existing.GetComponent<CinemachineCamera>() : null;
    }

    static CinemachineCamera Create(Transform parent)
    {
        var go = new GameObject(TppCameraName);
        go.transform.SetParent(parent, false);
        return go.AddComponent<CinemachineCamera>();
    }

    /// 값은 매번 다시 넣는다. 위 상수를 고치고 다시 돌리면 반영되어야 한다.
    ///
    /// `CinemachineThirdPersonFollow`가 아니라 궤도를 쓴다. 어깨 추적기는 대상의 회전을
    /// 그대로 따르도록 만들어져 있어서, 플레이어를 이동 방향으로 돌리는 이 프로젝트에서는
    /// (`PlayerMove.StepMove`) 마우스가 끼어들 자리가 없다 — 화면이 고정된 것처럼 보인다.
    /// 궤도는 마우스가 축을 직접 돌리고, 어깨 너머 구도는 Aim 뒤의 오프셋으로 만든다.
    static void ConfigureTpp(CinemachineCamera camera, int idlePriority,
                             InputActionEntry[] inputActions)
    {
        // 우선순위는 `MatchCameraDirector`가 매 전환마다 다시 정한다. 여기서는 재생 전에
        // 이 카메라가 브레인을 뺏지 않도록 쉬는 값으로만 둔다.
        camera.Priority = idlePriority;

        camera.Lens.FieldOfView = TppFieldOfView;

        Remove<CinemachineThirdPersonFollow>(camera.gameObject);

        var orbit = Ensure<CinemachineOrbitalFollow>(camera.gameObject);
        orbit.TargetOffset = TppTargetOffset;
        orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
        orbit.Radius = TppRadius;

        orbit.HorizontalAxis.Range = new Vector2(-180f, 180f);
        orbit.HorizontalAxis.Wrap = true;

        orbit.VerticalAxis.Range = TppPitchRange;
        orbit.VerticalAxis.Center = TppPitchStart;
        orbit.VerticalAxis.Value = TppPitchStart;

        // 궤도는 위치만 정한다. 어디를 보는지는 Aim이 정해야 플레이어가 화면에 남는다.
        Ensure<CinemachineRotationComposer>(camera.gameObject);

        // 어깨 너머는 Aim 뒤에 거는 로컬 오프셋으로 만든다. 궤도 중심을 옆으로 밀면
        // 카메라가 도는 축까지 같이 밀려 좌우 회전이 비뚤어진다.
        var offset = Ensure<CinemachineCameraOffset>(camera.gameObject);
        offset.ApplyAfter = CinemachineCore.Stage.Aim;
        offset.Offset = TppShoulder;

        // 마우스 감도는 설정 팝업이 이 컴포넌트를 통해 만진다. 카메라를 다시 세워도
        // 붙어 있어야 한다 — 없으면 팝업의 감도 줄이 통째로 사라진다.
        Ensure<LookSensitivity>(camera.gameObject);

        // 벽이 끼면 카메라를 앞으로 당긴다. 언리얼 스프링암의 bDoCollisionTest 자리다.
        // 없으면 나무와 카페 벽을 그냥 통과한다. 쿼터뷰는 붙이지 않는다 — 위에서 내려다보는
        // 각이라 막힐 일이 드물고, 나무마다 카메라가 내려오면 그게 더 산만하다.
        var deoccluder = Ensure<CinemachineDeoccluder>(camera.gameObject);
        deoccluder.CollideAgainst = TppOccluders;
        deoccluder.TransparentLayers = 0;
        deoccluder.IgnoreTag = string.Empty;
        deoccluder.MinimumDistanceFromTarget = 0.3f;

        // 이 구조체에는 필드 초기화가 없어서 AddComponent 직후 Enabled가 false다. 통째로 넣는다.
        deoccluder.AvoidObstacles = new CinemachineDeoccluder.ObstacleAvoidance
        {
            Enabled = true,
            DistanceLimit = 0f,
            MinimumOcclusionTime = 0f,
            CameraRadius = TppCameraRadius,

            // 궤도를 따라 앞으로 당긴다. 스프링암과 같은 해법이라 시점 비교의 기준이 흔들리지 않는다.
            Strategy = CinemachineDeoccluder.ObstacleAvoidance.ResolutionStrategy.PullCameraForward,
            MaximumEffort = 4,
            SmoothingTime = 0f,
            Damping = 0.4f,
            DampingWhenOccluded = 0.2f,
        };

        // 대시 연출의 화면 흔들림은 임펄스로 온다(`DashVisuals`). 리스너가 없으면 이 시점만
        // 조용해서, 시점을 비교하는 도중에 연출이 사라진 것처럼 보인다.
        Ensure<CinemachineImpulseListener>(camera.gameObject);

        WireInput(camera.gameObject, inputActions);
        EditorUtility.SetDirty(camera);
    }

    // ── 입력 ──────────────────────────────────────────────────────

    /// 축 하나의 입력 배선. 이름이 열쇠다 — Cinemachine이 축마다 "Look Orbit X"처럼
    /// 이름을 붙이고, 그 이름으로만 어느 축인지 알 수 있다.
    readonly struct InputActionEntry
    {
        public readonly string Name;
        public readonly InputActionReference Action;

        public InputActionEntry(string name, InputActionReference action)
        {
            Name = name;
            Action = action;
        }
    }

    static InputActionEntry[] ReadInputActions(CinemachineCamera source)
    {
        var controller = source != null
            ? source.GetComponent<CinemachineInputAxisController>()
            : null;
        if (controller == null) return System.Array.Empty<InputActionEntry>();

        var entries = new InputActionEntry[controller.Controllers.Count];
        for (var i = 0; i < entries.Length; i++)
        {
            var axis = controller.Controllers[i];
            entries[i] = new InputActionEntry(axis.Name, axis.Input.InputAction);
        }
        return entries;
    }

    static void WireInput(GameObject go, InputActionEntry[] inputActions)
    {
        var controller = Ensure<CinemachineInputAxisController>(go);

        // 축은 궤도 컴포넌트가 신고한다. 이 호출 전에는 목록이 비어 있어 감도를 넣을 곳이 없다.
        controller.SynchronizeControllers();

        var wired = 0;
        foreach (var axis in controller.Controllers)
        {
            foreach (var entry in inputActions)
            {
                if (entry.Name != axis.Name || entry.Action == null) continue;

                axis.Input.InputAction = entry.Action;
                wired++;
                break;
            }

            // 반경 축("Orbit Scale")은 건드리지 않는다. 마우스에 물리면 거리가 멋대로 변한다.
            if (axis.Name.EndsWith(" X")) axis.Input.Gain = TppLookGain.x;
            else if (axis.Name.EndsWith(" Y")) axis.Input.Gain = TppLookGain.y;
        }

        if (wired == 0)
            Debug.LogWarning("TPP 카메라에 물릴 Input Action을 쿼터뷰에서 찾지 못했다. Inspector에서 "
                           + "Cinemachine Input Axis Controller의 Input Action을 직접 지정한다.");

        EditorUtility.SetDirty(controller);
    }

    // ── 쿼터뷰 ────────────────────────────────────────────────────

    /// 쿼터뷰를 세우고 마우스를 떼어 낸다. 궤도 축은 여기서 넣은 값에 그대로 멈춰 서므로
    /// 결과는 "고정 각도로 따라다니는 카메라"다 — 각을 바꾸려면 위 상수를 고치고 다시 돈다.
    ///
    /// 입력 컨트롤러는 꺼 두지 않고 지운다. 비활성 컴포넌트로 남겨 두면 언젠가 누가 다시
    /// 켜고, 그때부터 쿼터뷰가 조용히 마우스를 따라 돈다. 배선 자체는 TPP로 옮겨 뒀다.
    ///
    /// 가로 축은 건드리지 않는다. 이동 입력이 카메라 기준이라(`PlayerInputRouter.ToWorld`)
    /// 요를 돌리면 WASD가 가리키는 월드 방향까지 같이 돌아간다 — 각도가 아니라 조작이 바뀐다.
    static bool ConfigureQuarter(CinemachineCamera camera)
    {
        if (camera == null) return false;

        camera.Lens.FieldOfView = QuarterFieldOfView;

        var orbit = camera.GetComponent<CinemachineOrbitalFollow>();
        if (orbit != null)
        {
            orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            orbit.Radius = QuarterRadius;

            orbit.VerticalAxis.Range = QuarterPitchRange;
            orbit.VerticalAxis.Center = QuarterPitch;
            orbit.VerticalAxis.Value = QuarterPitch;
        }

        EditorUtility.SetDirty(camera);

        var controller = camera.GetComponent<CinemachineInputAxisController>();
        if (controller == null) return false;

        Object.DestroyImmediate(controller);
        return true;
    }

    // ── 도우미 ────────────────────────────────────────────────────

    static T Ensure<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    static void Remove<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        if (component != null) Object.DestroyImmediate(component);
    }
}
