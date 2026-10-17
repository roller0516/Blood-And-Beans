using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine.U2D;
using TMPro;

// 제작 범위의 리소스·참조 확인. 전체 프로젝트 QA나 멀티플레이 검수를 대신하지 않는다.
public static class ValidateLuigiProductionUI
{
    public static string Main()
    {
        var errors=new List<string>();var records=new List<object>();var paths=Directory.GetFiles("Assets/Art/UI/Prefabs","*.prefab",SearchOption.AllDirectories).Select(x=>x.Replace('\\','/')).ToArray();int surfaces=0;
        foreach(var path in paths)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab==null){errors.Add(path+": missing asset");continue;}
            foreach(var transform in prefab.GetComponentsInChildren<Transform>(true))if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject)>0)errors.Add(path+": missing script @"+transform.name);
            foreach(var scaler in prefab.GetComponentsInChildren<CanvasScaler>(true))if(scaler.referenceResolution!=new Vector2(1920,1080)||scaler.screenMatchMode!=CanvasScaler.ScreenMatchMode.Expand)errors.Add(path+": scaler mismatch");
            foreach(var text in prefab.GetComponentsInChildren<TMP_Text>(true))if(text.font==null||text.fontSharedMaterial==null||text.font.atlasTexture==null)errors.Add(path+": missing TMP resource @"+text.name);
            foreach(var surface in prefab.GetComponentsInChildren<UISurfacePart>(true)){surfaces++;if(surface.Graphic==null||surface.Graphic.sprite==null||surface.Graphic.raycastTarget)errors.Add(path+": invalid shared surface");}
            foreach(var behaviour in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(behaviour==null)continue;var type=behaviour.GetType();if(!type.Name.StartsWith("UI")||type.Assembly.GetName().Name!="BB.Client")continue;
                var so=new SerializedObject(behaviour);var it=so.GetIterator();while(it.NextVisible(true))if(it.propertyType==SerializedPropertyType.ObjectReference&&it.objectReferenceValue==null&&it.objectReferenceEntityIdValue!=EntityId.None)errors.Add(path+": broken reference "+type.Name+"."+it.propertyPath);
            }
            records.Add(new{path,buttons=prefab.GetComponentsInChildren<Button>(true).Length,texts=prefab.GetComponentsInChildren<TMP_Text>(true).Length,sharedSurfaces=prefab.GetComponentsInChildren<UISurfacePart>(true).Length});
        }
        var crew=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/UI/Prefabs/Screen/UICharacterSelectScreen.prefab").GetComponent<UICharacterSelectScreen>();var crewSo=new SerializedObject(crew);var roster=crewSo.FindProperty("rosterCards");if(roster.arraySize!=CharacterStage.MaxSeats)errors.Add("Roster seat count mismatch");for(int i=0;i<roster.arraySize;i++)if(roster.GetArrayElementAtIndex(i).objectReferenceValue==null)errors.Add("Missing roster card "+i);
        var resources=Resources.Load<ResourceManager>(ResourceManager.AssetName);var resourceSo=new SerializedObject(resources);var entries=resourceSo.FindProperty("crewSprites");var ids=new HashSet<int>();for(int i=0;i<entries.arraySize;i++)ids.Add(entries.GetArrayElementAtIndex(i).FindPropertyRelative("id").intValue);foreach(var character in CharacterCatalog.All)if(!ids.Contains((int)character.Id))errors.Add("Missing current character portrait "+character.Id);
        var atlasPath="Assets/Art/UI/Atlases/LuigiCommon.spriteatlasv2";var importer=(SpriteAtlasImporter)AssetImporter.GetAtPath(atlasPath);var platform=importer.GetPlatformSettings("Standalone");if(!platform.overridden||platform.format!=TextureImporterFormat.BC7)errors.Add("PC atlas compression mismatch");if(importer.packingSettings.enableRotation||importer.packingSettings.enableTightPacking||importer.textureSettings.generateMipMaps)errors.Add("UI atlas packing mismatch");
        var fontRecords=new List<object>();foreach(var role in new[]{"Title","Body","Number","TitleFallback","BodyFallback"}){var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/Luigi"+role+" SDF.asset");if(font==null||font.material==null||font.atlasTexture==null)errors.Add("Missing font "+role);else{if(!role.EndsWith("Fallback")&&font.atlasPopulationMode!=AtlasPopulationMode.Static)errors.Add("Primary font must be static: "+role);fontRecords.Add(new{role,mode=font.atlasPopulationMode.ToString(),glyphs=font.characterTable.Count,pages=font.atlasTextures.Length,width=font.atlasWidth,height=font.atlasHeight});}}
        if(Resources.Load<UIThemeConfig>(UIThemeConfig.AssetName).FontScale!=1)errors.Add("Unexpected font scale");
        var stage=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Environment/Prefabs/CharacterStage.prefab");if(stage.GetComponentsInChildren<Renderer>(true).Any(r=>r.sharedMaterials.Any(m=>m==null)))errors.Add("UI character stage has empty material slots");
        var result=new{date="2026-10-18",passed=errors.Count==0,prefabs=paths.Length,sharedSurfaceInstances=surfaces,errors,atlas=new{format=platform.format.ToString(),maxSize=platform.maxTextureSize,rotation=importer.packingSettings.enableRotation,mipmaps=importer.textureSettings.generateMipMaps},fonts=fontRecords,records};
        var json=Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented);File.WriteAllText("../00__Docs/02__Art/02__UI/luigi-pc-ui-v2/ui-unity-implementation-validation.json",json);if(errors.Count>0)throw new Exception(string.Join("\n",errors));return json;
    }
}
