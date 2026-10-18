using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TMPro;

/// 기존 프리팹의 배치·표면·글자와 직렬화 연결을 읽기 전용으로 기록한다.
public static class InspectLightLuigi
{
    const string PrefabRoot = "Assets/Art/UI/Prefabs";
    static string Relative(Transform root, Transform child) => AnimationUtility.CalculateTransformPath(child, root);
    public static string Main(string fileName = "prefab-before.json")
    {
        var paths = AssetDatabase.FindAssets("t:Prefab", new[]{PrefabRoot}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p).ToArray();
        var prefabs = paths.Select(path => {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return new {
                path, guid=AssetDatabase.AssetPathToGUID(path),
                nodes=root.GetComponentsInChildren<Transform>(true).Select(t=> {
                    var rt=t as RectTransform;var image=t.GetComponent<UnityEngine.UI.Image>();var text=t.GetComponent<TMP_Text>();
                    return new {name=Relative(root.transform,t),active=t.gameObject.activeSelf,
                        components=t.GetComponents<Component>().Where(c=>c!=null).Select(c=>c.GetType().Name).ToArray(),
                        size=rt==null?null:new[]{rt.sizeDelta.x,rt.sizeDelta.y},position=rt==null?null:new[]{rt.anchoredPosition.x,rt.anchoredPosition.y},
                        min=rt==null?null:new[]{rt.anchorMin.x,rt.anchorMin.y},max=rt==null?null:new[]{rt.anchorMax.x,rt.anchorMax.y},
                        sprite=image!=null&&image.sprite!=null?image.sprite.name:null,texture=image!=null&&image.sprite!=null?AssetDatabase.GetAssetPath(image.sprite):null,material=image!=null&&image.material!=null?image.material.name:null,
                        color=image==null?null:new[]{image.color.r,image.color.g,image.color.b,image.color.a},
                        text=text!=null?text.text:null,font=text!=null&&text.font!=null?text.font.name:null,fontSize=text!=null?text.fontSize:0,textMaterial=text!=null&&text.fontSharedMaterial!=null?text.fontSharedMaterial.name:null};
                }).ToArray(),
                references=root.GetComponentsInChildren<MonoBehaviour>(true).Where(c=>c!=null&&c.GetType().Assembly.GetName().Name=="BB.Client").Select(c=>{
                    var so=new SerializedObject(c);var p=so.GetIterator();var refs=new System.Collections.Generic.Dictionary<string,string>();
                    while(p.NextVisible(true))if(p.propertyType==SerializedPropertyType.ObjectReference&&p.objectReferenceValue!=null){
                        var obj=p.objectReferenceValue;var tr=obj is Component comp?comp.transform:obj is GameObject go?go.transform:null;
                        refs[p.propertyPath]=tr!=null&&tr.IsChildOf(root.transform)?Relative(root.transform,tr):AssetDatabase.GetAssetPath(obj);
                    }
                    return new {node=Relative(root.transform,c.transform),type=c.GetType().Name,refs};
                }).ToArray()
            };
        }).ToArray();
        var output=Path.GetFullPath("../00__Docs/02__Art/02__UI/luigi-pc-ui-v2/light-ui-preview/"+fileName);
        File.WriteAllText(output,Newtonsoft.Json.JsonConvert.SerializeObject(new{prefabs,theme=AssetDatabase.GetAssetPath(UITheme.Config),roundMaterial=AssetDatabase.GetAssetPath(UITheme.Config.RoundedMaterial)},Newtonsoft.Json.Formatting.Indented));
        return "Read-only inventory: "+paths.Length+" prefabs; "+output;
    }
}
