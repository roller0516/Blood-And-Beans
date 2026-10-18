using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// 변경한 UI의 저장 참조·공유 리소스·셰이더만 확인한다. 네트워크 매치와 성능 검증은 별도다.
public static class ValidateLightLuigiUI
{
    public static string Main()
    {
        var errors=new List<string>();var records=new List<object>();int textCount=0,shared=0;
        var paths=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Art/UI/Prefabs"}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
        foreach(var path in paths){
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach(var t in root.GetComponentsInChildren<Transform>(true))if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)errors.Add(path+": missing script "+t.name);
            foreach(var c in root.GetComponentsInChildren<MonoBehaviour>(true)){
                if(c==null||c.GetType().Assembly.GetName().Name!="BB.Client")continue;
                var it=new SerializedObject(c).GetIterator();while(it.NextVisible(true))if(it.propertyType==SerializedPropertyType.ObjectReference&&it.objectReferenceValue==null&&it.objectReferenceEntityIdValue!=EntityId.None)errors.Add(path+": broken "+c.GetType().Name+"."+it.propertyPath);
            }
            foreach(var part in root.GetComponentsInChildren<UISurfacePart>(true)){
                shared++;var im=part.Graphic;
                if(im==null||im.material==null||im.material.shader.name!="BB/UI Rounded Rect"||im.GetComponent<UIRoundImage>()==null)errors.Add(path+": invalid shared glass "+part.name);
            }
            foreach(var text in root.GetComponentsInChildren<TMP_Text>(true)){
                textCount++;if(text.font==null||text.fontSharedMaterial==null||text.font.atlasTexture==null)errors.Add(path+": missing font "+text.name);
                if(text.enableAutoSizing)errors.Add(path+": AutoSize "+text.name);
            }
            records.Add(new{path,guid=AssetDatabase.AssetPathToGUID(path),buttons=root.GetComponentsInChildren<Button>(true).Length});
        }
        var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/Shaders/UIRoundedRect.shader");
        var messages=ShaderUtil.GetShaderMessages(shader);foreach(var m in messages)if(m.severity.ToString()=="Error")errors.Add(m.message);
        var mats=AssetDatabase.FindAssets("t:Material",new[]{"Assets/Art/Environment/Materials"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>Path.GetFileName(p).StartsWith("UI_Luigi")).ToArray();
        var fonts=new[]{"Title","Body","Number"}.Select(role=>{var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/Luigi"+role+" SDF.asset");return new{role,font=font.name,mode=font.atlasPopulationMode.ToString(),pages=font.atlasTextures.Length};}).ToArray();
        var result=new{date=DateTimeOffset.Now.ToString("o"),passed=errors.Count==0,prefabCount=paths.Length,textCount,sharedSurfaceInstances=shared,sharedMaterials=mats,fonts,errors,shaderMessages=messages.Select(m=>new{m.message,severity=m.severity.ToString()}).ToArray(),records};
        var json=Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText("../00__Docs/02__Art/02__UI/luigi-pc-ui-v2/light-ui-preview/native-validation.json",json);
        return json;
    }
}
