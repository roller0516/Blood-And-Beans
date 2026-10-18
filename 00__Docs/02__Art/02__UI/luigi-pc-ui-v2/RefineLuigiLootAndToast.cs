using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
public static class RefineLuigiLootAndToast {
 static readonly Color Ink=new Color(.16f,.085f,.035f);
 static Sprite Sprite(string name)=>new[]{"LuigiMenuKit","LuigiMenuBook","LuigiPlayKit"}.SelectMany(n=>AssetDatabase.LoadAllAssetsAtPath("Assets/Art/UI/Sprites/Common/"+n+".png")).OfType<Sprite>().First(x=>x.name==name);
 static void Edit(string path,Action<GameObject> action){var g=PrefabUtility.LoadPrefabContents(path);try{action(g);PrefabUtility.SaveAsPrefabAsset(g,path);}finally{PrefabUtility.UnloadPrefabContents(g);}}
 public static string Main(){
  if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first");
  Edit("Assets/Art/UI/Prefabs/Parts/UIBoxLootSlot.prefab",g=>Slot(g.GetComponent<UIBoxLootSlot>()));
  Edit("Assets/Art/UI/Prefabs/Popup/UIBoxLootPopup.prefab",g=>{
   var panel=g.transform.Find("Stage/Panel");var face=panel.Find("LuigiSurface").GetComponent<Image>();face.sprite=Sprite("PlayBoard");face.type=Image.Type.Sliced;face.pixelsPerUnitMultiplier=3;
   var heading=panel.Find("LootHeadingPlate");face=heading.Find("LuigiSurface").GetComponent<Image>();face.sprite=Sprite("MenuGreen");face.type=Image.Type.Sliced;face.pixelsPerUnitMultiplier=5;
   heading.Find("LootHeading/LuigiSurface").gameObject.SetActive(false);
   foreach(var slot in g.GetComponentsInChildren<UIBoxLootSlot>(true))Slot(slot);
  });
  Edit("Assets/Art/UI/Prefabs/Popup/UIReturnResultPopup.prefab",g=>{
   var so=new SerializedObject(g.GetComponent<UIReturnResultPopup>());so.FindProperty("success").colorValue=new Color(.20f,.36f,.14f);so.FindProperty("warning").colorValue=new Color(.52f,.29f,.04f);so.FindProperty("failure").colorValue=new Color(.62f,.10f,.06f);so.ApplyModifiedPropertiesWithoutUndo();
  });
  return "Only loot popup/slot and return toast presentation: remove stacked heading surface; readable paper labels and severity colours.";
 }
 static void Slot(UIBoxLootSlot slot){
  var so=new SerializedObject(slot);so.FindProperty("rarityTextColor").colorValue=Ink;so.FindProperty("readyColor").colorValue=Color.white;
  var badge=(TMP_Text)so.FindProperty("rarityBadge").objectReferenceValue;Text(badge,"Title",22);var rect=badge.rectTransform;rect.anchorMin=new Vector2(0,0);rect.anchorMax=new Vector2(1,0);rect.offsetMin=new Vector2(8,8);rect.offsetMax=new Vector2(-8,36);
  var tooltip=(GameObject)so.FindProperty("tooltip").objectReferenceValue;var image=tooltip.GetComponent<Image>();image.sprite=Sprite("MenuRegistration");image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=5;image.color=Color.white;
  foreach(var text in tooltip.GetComponentsInChildren<TMP_Text>(true))Text(text,text.name=="Name"?"Title":"Body",text.name=="Name"?23:20);
  so.ApplyModifiedPropertiesWithoutUndo();
 }
 static void Text(TMP_Text text,string role,float size){text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/Luigi"+role+" SDF.asset");text.fontSharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Fonts/LuigiMenu"+role+"Ink.mat");text.enableVertexGradient=false;text.color=Ink;text.fontSize=size;}
}
