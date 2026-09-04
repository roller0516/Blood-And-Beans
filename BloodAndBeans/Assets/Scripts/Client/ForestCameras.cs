using UnityEngine;

/// 숲을 어느 카메라에 그릴지 정한다 (`ForestInstances`, `ForestGrass`).
///
/// 두 렌더러는 `[ExecuteAlways]`로 `beginCameraRendering`에 붙어 있어서, 거르지 않으면
/// **URP가 그리는 모든 카메라**에 숲이 제출된다. 거기에는 게임·씬 뷰만이 아니라
/// 프리팹 스테이지의 씬 뷰와 머티리얼·에셋 썸네일을 굽는 프리뷰 카메라가 함께 들어 있다.
/// 그래서 프리팹을 격리 모드로 열어도, 머티리얼 미리보기를 봐도 숲이 나왔다.
///
/// 프리팹 모드의 「Context」 설정과는 무관하다 — 그 설정은 씬 오브젝트를 보여 줄지만
/// 정하고, 여기서 그리는 것은 씬 오브젝트가 아니라 코드가 직접 제출하는 인스턴스다.
public static class ForestCameras
{
    /// 이 카메라에 `owner`가 속한 씬의 숲을 그려야 하는가.
    ///
    /// 프리뷰 카메라는 통째로 거른다. 씬 뷰·게임 카메라는 `Camera.scene`으로 가른다 —
    /// 프리뷰 씬(프리팹 스테이지) 전용 카메라만 그 값이 유효하고, 일반 씬 뷰와 게임
    /// 카메라는 비어 있다.
    public static bool Renders(Camera camera, GameObject owner)
    {
        if (camera == null || owner == null) return false;

        if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView)
            return false;

        var only = camera.scene;
        return !only.IsValid() || only == owner.scene;
    }
}
