using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine.U2D;

/// 팀원이 분리한 HUD 배선과 이번에 추가한 공유 자산의 읽기 전용 검사.
public static class ValidateLuigiReuse {
 public static string Main(){
  var errors=new List<string>();
  var hud=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/UI/Prefabs/Screen/UIMatchHudScreen.prefab");
  var root=new SerializedObject(hud.GetComponent<UIMatchHudScreen>());
  foreach(var name in new[]{"day","night"})if(root.FindProperty(name)?.objectReferenceValue==null)errors.Add("HUD missing "+name);
  foreach(var name in new[]{"CommonRoot","DayRoot","NightRoot"})if(hud.transform.Find(name)==null)errors.Add("HUD missing "+name);
  if(hud.GetComponentsInChildren<UIMatchHudDay>(true).Length!=1||hud.GetComponentsInChildren<UIMatchHudNight>(true).Length!=1)errors.Add("HUD phase component count");
  var variants=new[]{"Board","Book","Brown","Clipboard","Cream","Green","Red","Registration"}.Select(n=>"Assets/Art/UI/Prefabs/Parts/UISurfaceMenu"+n+".prefab").ToArray();
  foreach(var path in variants){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab==null||PrefabUtility.GetPrefabAssetType(prefab)!=PrefabAssetType.Variant)errors.Add("Not variant: "+path);}
  const string atlasPath="Assets/Art/UI/Atlases/LuigiCommon.spriteatlasv2";
  var packables=AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath).GetPackables().Select(AssetDatabase.GetAssetPath).ToArray();
  foreach(var name in new[]{"LuigiMenuKit","LuigiMenuBook","LuigiPlayKit"})if(!packables.Contains("Assets/Art/UI/Sprites/Common/"+name+".png"))errors.Add("Atlas missing "+name);
  var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/Shaders/UICrewBackdrop.shader");
  if(shader==null)errors.Add("Missing crew backdrop shader");
  var messages=shader==null?Array.Empty<ShaderMessage>():ShaderUtil.GetShaderMessages(shader);
  foreach(var message in messages)if(message.severity.ToString()=="Error")errors.Add(message.message);
  var report=new{date="2026-10-18",passed=errors.Count==0,errors,hudRoots=new[]{"CommonRoot","DayRoot","NightRoot"},surfaceVariants=variants,atlasPackables=packables,shaderMessages=messages.Select(x=>new{x.message,severity=x.severity.ToString()})};
  var json=Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented);File.WriteAllText("../00__Docs/02__Art/02__UI/luigi-pc-ui-v2/ui-prefab-reuse-validation.json",json);if(errors.Count>0)throw new Exception(string.Join("\n",errors));return json;
 }
}
