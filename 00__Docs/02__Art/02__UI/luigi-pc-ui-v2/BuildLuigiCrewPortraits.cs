using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using UnityEngine.U2D;
using UnityEditor.AddressableAssets;
using Object=UnityEngine.Object;

// 현재 CharacterVisualConfig의 실제 모델로 UI 초상을 제작한다. 캐릭터 모델은 수정하지 않는다.
public static class BuildLuigiCrewPortraits
{
    public static string Main()
    {
        var config=Resources.Load<CharacterVisualConfig>(CharacterVisualConfig.AssetName);
        var scene=EditorSceneManager.NewPreviewScene();var data=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");var features=data.rendererFeatures.ToArray();var states=features.Select(x=>x.isActive).ToArray();foreach(var f in features)f.SetActive(false);
        var async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        var atlasPath="Assets/Art/UI/Atlases/CharacterSelect.spriteatlasv2";var atlasGuid=AssetDatabase.AssetPathToGUID(atlasPath);
        try
        {
            var light=new GameObject("PortraitKey");SceneManager.MoveGameObjectToScene(light,scene);var key=light.AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.1f;light.transform.rotation=Quaternion.Euler(35,155,0);
            var fillObject=new GameObject("PortraitFill");SceneManager.MoveGameObjectToScene(fillObject,scene);var fill=fillObject.AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.4f;fill.color=new Color(.8f,.88f,1);fill.transform.rotation=Quaternion.Euler(15,225,0);
            var camera=new GameObject("PortraitCamera");SceneManager.MoveGameObjectToScene(camera,scene);var cam=camera.AddComponent<Camera>();cam.scene=scene;cam.orthographic=true;cam.clearFlags=CameraClearFlags.SolidColor;cam.allowHDR=false;cam.nearClipPlane=.1f;cam.farClipPlane=100;cam.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var textures=new System.Collections.Generic.List<Object>();
            foreach(var character in CharacterCatalog.All)
            {
                var path=AssetDatabase.GUIDToAssetPath(config.ReferenceFor(character.Id).AssetGUID);var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),scene);config.ApplyTransform(character.Id,model.transform);
                foreach(var animator in model.GetComponentsInChildren<Animator>(true)){animator.Rebind();animator.Update(0);}
                foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))skin.updateWhenOffscreen=true;
                var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                cam.transform.position=bounds.center+new Vector3(0,bounds.size.y*.035f,8);cam.transform.LookAt(bounds.center);cam.orthographicSize=Mathf.Max(bounds.extents.y,bounds.extents.x)*1.12f;
                var rt=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32);rt.antiAliasing=4;rt.Create();cam.targetTexture=rt;var previous=RenderTexture.active;var png=new Texture2D(512,512,TextureFormat.RGBA32,false);
                try
                {
                    cam.backgroundColor=Color.black;cam.Render();RenderTexture.active=rt;png.ReadPixels(new Rect(0,0,512,512),0,0);png.Apply();var black=png.GetPixels();
                    cam.backgroundColor=Color.white;cam.Render();RenderTexture.active=rt;png.ReadPixels(new Rect(0,0,512,512),0,0);png.Apply();var white=png.GetPixels();
                    for(int i=0;i<black.Length;i++){var b=black[i].linear;var w=white[i].linear;var a=1-Mathf.Clamp01(((w.r-b.r)+(w.g-b.g)+(w.b-b.b))/3);var c=a>.001f?new Color(b.r/a,b.g/a,b.b/a,1).gamma:Color.clear;c.a=a;black[i]=c;}
                    png.SetPixels(black);png.Apply();var target="Assets/Art/UI/Sprites/CharacterSelect/LuigiCrew"+character.Id+".png";File.WriteAllBytes(target,png.EncodeToPNG());AssetDatabase.ImportAsset(target,ImportAssetOptions.ForceSynchronousImport);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(target);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.maxTextureSize=512;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();textures.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(target));
                }
                finally{cam.targetTexture=null;RenderTexture.active=previous;Object.DestroyImmediate(png);rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(model);}
            }
            var atlas=SpriteAtlasAsset.Load(atlasPath);var packed=AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath).GetPackables();atlas.Add(textures.Where(x=>!packed.Contains(x)).ToArray());SpriteAtlasAsset.Save(atlas,atlasPath);AssetDatabase.ImportAsset(atlasPath,ImportAssetOptions.ForceSynchronousImport);
            var resources=Resources.Load<ResourceManager>(ResourceManager.AssetName);var so=new SerializedObject(resources);var entries=so.FindProperty("crewSprites");entries.arraySize=CharacterCatalog.All.Length;
            for(int i=0;i<entries.arraySize;i++){var e=entries.GetArrayElementAtIndex(i);var id=CharacterCatalog.All[i].Id;e.FindPropertyRelative("id").intValue=(int)id;var reference=e.FindPropertyRelative("sprite");reference.FindPropertyRelative("atlasGuid").stringValue=atlasGuid;reference.FindPropertyRelative("spriteName").stringValue="LuigiCrew"+id;}
            so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(resources);
            var settings=AddressableAssetSettingsDefaultObject.Settings;var existing=settings.FindAssetEntry(atlasGuid);if(existing==null)throw new Exception("Character atlas is not addressable");
            var commonGuid=AssetDatabase.AssetPathToGUID("Assets/Art/UI/Atlases/LuigiCommon.spriteatlasv2");var shared=settings.CreateOrMoveEntry(commonGuid,existing.parentGroup);shared.address="LuigiCommon";EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();return "Rendered four actual model portraits; ResourceManager now uses current CharacterId keys; shared atlas added to existing UI addressable group.";
        }
        finally{ShaderUtil.allowAsyncCompilation=async;for(int i=0;i<features.Length;i++)features[i].SetActive(states[i]);EditorSceneManager.ClosePreviewScene(scene);}
    }
}
