using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

/// 캐릭터의 현재 포즈만 굽는다. 복제본에는 입력·물리·네트워크 컴포넌트가 없다.
public sealed class TeleportAfterimage : MonoBehaviour
{
    static readonly int Opacity = Shader.PropertyToID("_Opacity");
    readonly List<Mesh> baked = new();
    Material material;
    Tween fade;

    public static void Play(Transform model, Transform player, Vector3 origin, Quaternion rotation,
        Material sourceMaterial, float seconds, float rise)
    {
        if (sourceMaterial == null)
        {
            CDebug.LogError("전송 잔상 머티리얼이 연결되지 않았다.", player);
            return;
        }
        var root = new GameObject(nameof(TeleportAfterimage));
        // 플레이어가 씬을 이동하더라도 잔상은 출발 씬과 함께 정리된다.
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, player.gameObject.scene);
        root.transform.SetPositionAndRotation(origin, rotation);
        var effect = root.AddComponent<TeleportAfterimage>();
        effect.material = new Material(sourceMaterial);
        foreach (var source in model.GetComponentsInChildren<Renderer>())
        {
            if (!source.enabled || !source.gameObject.activeInHierarchy) continue;
            Mesh mesh;
            if (source is SkinnedMeshRenderer skin)
            {
                mesh = new Mesh { name = "TeleportPose" };
                skin.BakeMesh(mesh, false);
                effect.baked.Add(mesh);
            }
            else if (source is MeshRenderer && source.TryGetComponent<MeshFilter>(out var filter))
                mesh = filter.sharedMesh;
            else continue;
            if (mesh == null) continue;

            var part = new GameObject(source.name) { layer = source.gameObject.layer };
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = Quaternion.Inverse(player.rotation) * (source.transform.position - player.position);
            part.transform.localRotation = Quaternion.Inverse(player.rotation) * source.transform.rotation;
            part.transform.localScale = source.transform.lossyScale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            var materials = new Material[mesh.subMeshCount];
            for (var i = 0; i < materials.Length; i++) materials[i] = effect.material;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        effect.fade = DOVirtual.Float(1f, 0f, Mathf.Max(0.01f, seconds), alpha =>
        {
            effect.material.SetFloat(Opacity, alpha);
            root.transform.position = origin + Vector3.up * ((1f - alpha) * rise);
        }).SetEase(Ease.InQuad).SetLink(root).OnComplete(() => Destroy(root));
    }

    void OnDestroy()
    {
        fade?.Kill();
        foreach (var mesh in baked) if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }
}
