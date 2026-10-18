using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
public static class WireLuigiCrewPreview {
 public static string Main(){
  var guard=Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText("../../output/ui-nonhud-guard/protected.json"));
  var paths=new[]{"Assets/Art/UI/Prefabs/Screen/UICharacterSelectScreen.prefab","Assets/Art/Environment/Prefabs/CharacterStage.prefab"};
  foreach(var path in paths)if(guard.Any(x=>(string)x["path"]==path))throw new Exception("REFUSE protected HUD dependency "+path);
  // Menu-stage-local background only. No world scene, shared renderer, camera rig or light assets are edited.
  var g=PrefabUtility.LoadPrefabContents(paths[0]);try{
   var stage=g.transform.Find("Stage");foreach(var name in new[]{"CharacterPreview","CrewArtwork"}){var t=stage.Find(name);if(t!=null)UnityEngine.Object.DestroyImmediate(t.gameObject);}
   foreach(var name in new[]{"CrewBackdropRight","CrewBackdropTop","CrewBackdropBottom"})stage.Find(name).gameObject.SetActive(false);
   var header=(RectTransform)stage.Find("GameObject");header.anchoredPosition=new Vector2(-65,-25);header.sizeDelta=new Vector2(330,55);header.SetAsLastSibling();
   PrefabUtility.SaveAsPrefabAsset(g,paths[0]);
  }finally{PrefabUtility.UnloadPrefabContents(g);}
  g=PrefabUtility.LoadPrefabContents(paths[1]);try{
   foreach(var name in new[]{"Cafe_Lobby","CrewDecor","PP"})g.transform.Find(name).gameObject.SetActive(false);
   var camera=g.GetComponentInChildren<Camera>(true);camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
   var s=new SerializedObject(g.GetComponent<CharacterStage>());s.FindProperty("selectedViewport").rectValue=new Rect(0,0,1,1);s.ApplyModifiedPropertiesWithoutUndo();
   var composer=g.GetComponentInChildren<Unity.Cinemachine.CinemachinePositionComposer>(true);var composition=composer.Composition;composition.ScreenPosition=new Vector2(-.235f,-.045f);composer.Composition=composition;composer.CameraDistance=5.4f;
   const string matPath="Assets/Art/UI/PrototypeKit/UI_CrewBackdrop.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(mat==null){mat=new Material(Shader.Find("BloodAndBeans/UI/CrewBackdrop"));AssetDatabase.CreateAsset(mat,matPath);}
   var back=g.transform.Find("MenuBackdropCanvas");if(back==null){back=new GameObject("MenuBackdropCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler)).transform;back.SetParent(g.transform,false);}
   var canvas=back.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.farClipPlane*.85f;canvas.sortingOrder=-100;
   var scaler=back.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
   var image=back.Find("Artwork");if(image==null){image=new GameObject("Artwork",typeof(RectTransform),typeof(Image)).transform;image.SetParent(back,false);}var r=(RectTransform)image;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;var im=image.GetComponent<Image>();im.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Sprites/Title/LuigiTitleBackdrop.png");im.material=mat;im.color=new Color(.7f,.7f,.7f);im.raycastTarget=false;
   foreach(var t in back.GetComponentsInChildren<Transform>(true))t.gameObject.layer=g.layer;
   PrefabUtility.SaveAsPrefabAsset(g,paths[1]);
  }finally{PrefabUtility.UnloadPrefabContents(g);}
  return "Only crew screen and its private stage prefab updated. Existing 3D stage camera draws model in front of UI artwork; no RT or shared renderer edits.";
 }
}
