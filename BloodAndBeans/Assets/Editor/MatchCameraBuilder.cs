using System.Collections.Generic;
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
    const string MenuPath = "Blood & Beans/매치 카메라 세우기";
    const string TppCameraName = "TppCamera";
    const string TppPivotName = "CameraPivot";

    /// 축이 플레이어 원점보다 얼마나 위인가. 눈높이다 (`CameraPivot.eyeHeight`).
    const float TppEyeHeight = 1.4f;

    /// 축에서 카메라까지의 거리.
    const float TppCameraDistance = 3.5f;

    /// 어깨 오프셋. 축 기준 로컬이라 카메라를 돌려도 어깨 쪽이 따라 바뀌지 않는다.
    static readonly Vector3 TppShoulder = new(0.6f, 0f, 0f);

    /// 손이 어깨보다 얼마나 위인가. 세로로 돌릴 때 캐릭터가 화면에서 오르내리는 폭을 잡는다.
    const float TppVerticalArmLength = 0.4f;

    /// 0이면 왼쪽 어깨, 1이면 오른쪽.
    const float TppCameraSide = 1f;

    /// 카메라가 목표를 쫓는 반응 시간(초). 카메라 로컬 축마다 따로다.
    static readonly Vector3 TppDamping = new(0.1f, 0.5f, 0.3f);



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


        var camera = tppProperty.objectReferenceValue as CinemachineCamera;
        if (camera == null) camera = Find(director.transform);
        if (camera == null) camera = Create(director.transform);

        ConfigureTpp(camera, so.FindProperty("idlePriority").intValue);
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
    /// 리그는 `CinemachineThirdPersonFollow` 하나다. 위치·회전·어깨 오프셋이 전부 그 안에
    /// 있어서, 예전의 궤도 + 회전 구성기 + 오프셋 + 디오클루더 넷이 하나로 줄었다.
    ///
    /// **Follow 대상은 여기서 정하지 않는다.** 어깨 추적기는 대상의 회전을 그대로 쓰는데,
    /// 그 대상은 런타임에 스폰되는 플레이어의 자식 `PlayerCameraRoot`다. 스폰 시점에
    /// `MatchCameraDirector`가 꽂는다.
    static void ConfigureTpp(CinemachineCamera camera, int idlePriority)
    {
        // 우선순위는 `MatchCameraDirector`가 매 전환마다 다시 정한다. 여기서는 재생 전에
        // 이 카메라가 브레인을 뺏지 않도록 쉬는 값으로만 둔다.
        camera.Priority = idlePriority;
        camera.Lens.FieldOfView = TppFieldOfView;

        // 궤도 리그를 걷어낸다. 넷이 하던 일을 ThirdPersonFollow 하나가 한다.
        Remove<CinemachineOrbitalFollow>(camera.gameObject);
        Remove<CinemachineRotationComposer>(camera.gameObject);
        Remove<CinemachineCameraOffset>(camera.gameObject);
        Remove<CinemachineDeoccluder>(camera.gameObject);

        // 마우스는 이제 플레이어의 카메라 축이 직접 읽는다 (`PlayerInputRouter` →
        // `PlayerCameraRoot`). Starter Assets와 같은 방식이라 Cinemachine 입력 컴포넌트가
        // 필요 없다. 감도도 축과 함께 산다.
        Remove<LookSensitivity>(camera.gameObject);
        Remove<CinemachineInputAxisController>(camera.gameObject);

        var follow = Ensure<CinemachineThirdPersonFollow>(camera.gameObject);
        follow.CameraDistance = TppCameraDistance;
        follow.ShoulderOffset = TppShoulder;
        follow.VerticalArmLength = TppVerticalArmLength;
        follow.CameraSide = TppCameraSide;
        follow.Damping = TppDamping;

        // 카메라는 충돌을 보지 않는다. 숲이 빽빽해서(나무 콜라이더 656개) 켜 두면 나무에
        // 닿지 않고 걷기만 해도 카메라가 계속 앞으로 당겨졌다 돌아온다 — 측정에서 정상
        // 보행 중 33% 프레임이 3.5m를 못 지켰고 최소 0.02m까지 파고들었다.
        follow.AvoidObstacles = new CinemachineThirdPersonFollow.ObstacleSettings { Enabled = false };

        // 대시 연출의 화면 흔들림은 임펄스로 온다(`DashVisuals`).
        Ensure<CinemachineImpulseListener>(camera.gameObject);

        EditorUtility.SetDirty(camera);
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
