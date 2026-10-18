using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.Rendering.Universal;

/// 병합된 3D 카페를 보존하고 이미지 덮개 없이 캐릭터 선택 화면에 연결한다.
public static class RecoverLuigiCharacterStage
{
    const string PrefabPath = "Assets/Art/Environment/Prefabs/CharacterStage.prefab";

    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var stage = root.GetComponent<CharacterStage>();
            if (stage == null) throw new InvalidOperationException("CharacterStage is missing.");
            var serialized = new SerializedObject(stage);
            serialized.FindProperty("selectedCharacterOnly").boolValue = true;
            serialized.FindProperty("selectedViewport").rectValue = new Rect(0f, 0f, 1f, 1f);
            var camera = serialized.FindProperty("stageCamera").objectReferenceValue as Camera;
            var vcam = serialized.FindProperty("stageVCam").objectReferenceValue as CinemachineCamera;
            if (camera == null || vcam == null) throw new InvalidOperationException("Stage camera reference is missing.");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.fieldOfView = 40f;
            camera.transform.localPosition = Vector3.zero;
            camera.transform.localRotation = Quaternion.Euler(7f, 0f, 0f);
            var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData != null) cameraData.renderPostProcessing = true;
            var composer = vcam.GetComponent<CinemachinePositionComposer>();
            if (composer == null) throw new InvalidOperationException("Stage position composer is missing.");
            var serializedComposer = new SerializedObject(composer);
            serializedComposer.FindProperty("CameraDistance").floatValue = 5.4f;
            serializedComposer.FindProperty("Composition.ScreenPosition").vector2Value = new Vector2(-.235f, -.045f);
            serializedComposer.ApplyModifiedPropertiesWithoutUndo();
            foreach (var name in new[] { "Cafe_Lobby", "CrewDecor" })
            {
                var child = root.transform.Find(name);
                if (child == null) throw new InvalidOperationException(name + " is missing.");
                child.gameObject.SetActive(true);
            }
            var backdrop = root.transform.Find("MenuBackdropCanvas");
            if (backdrop != null) UnityEngine.Object.DestroyImmediate(backdrop.gameObject);
            // 새로 연결된 설비·바닥도 전용 무대 카메라의 레이어로 그린다.
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = root.layer;
            if (root.GetComponentsInChildren<Transform>(true).Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) != 0))
                throw new InvalidOperationException("Missing script in CharacterStage.");
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return "Existing live 3D cafe/decor and stage post-processing enabled; menu image Canvas removed. Full viewport and left character composition retained. Meshes/materials/lighting profiles/seats/GUID preserved.";
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
