using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;
public static class RenderApprovedSkillPart
{
    public static string Main()
    {
        var scene=EditorSceneManager.NewPreviewScene();var previous=RenderTexture.active;
        var data=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
        var states=data.rendererFeatures.Select(f=>f.isActive).ToArray();foreach(var feature in data.rendererFeatures)feature.SetActive(false);
        var rt=new RenderTexture(384,384,24);
        try
        {
            var cameraGo=new GameObject("UI part proof camera");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraGo,scene);
            var camera=cameraGo.AddComponent<Camera>();camera.scene=scene;camera.orthographic=true;camera.orthographicSize=88;camera.aspect=1;camera.transform.position=new Vector3(0,0,-100);camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.targetTexture=rt;
            var canvasGo=new GameObject("UI part proof",typeof(RectTransform),typeof(Canvas));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvasGo,scene);canvasGo.layer=31;
            var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;((RectTransform)canvasGo.transform).sizeDelta=new Vector2(176,176);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/UI/Prefabs/Parts/UISkillSlot.prefab");var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);instance.transform.SetParent(canvasGo.transform,false);
            var rect=(RectTransform)instance.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=Vector2.zero;rect.sizeDelta=new Vector2(108,132);
            foreach(var transform in instance.GetComponentsInChildren<Transform>(true))transform.gameObject.layer=31;
            var slot=instance.GetComponent<UISkillSlot>();slot.SetIcon(null);slot.SetKey("Q");
            foreach(var text in instance.GetComponentsInChildren<TMPro.TMP_Text>(true))text.ForceMeshUpdate(true,true);
            Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(rect);Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
            var png=new Texture2D(384,384,TextureFormat.RGBA32,false);png.ReadPixels(new Rect(0,0,384,384),0,0);png.Apply();
            const string output="../00__Docs/02__Art/02__UI/luigi-pc-ui-v2/approved-skill-prefab.png";System.IO.File.WriteAllBytes(output,png.EncodeToPNG());Object.DestroyImmediate(png);return output;
        }
        finally{RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);EditorSceneManager.ClosePreviewScene(scene);for(int i=0;i<states.Length;i++)data.rendererFeatures[i].SetActive(states[i]);}
    }
}
