// Gallery capture only: no scene, prefab, asset, material or camera rig is saved.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

public static class GalleryGameplayCapture
{
    const int Width=1920, Height=1080;
    public static string Main(string destination)
    {
        if(!EditorApplication.isPlaying || !EditorApplication.isPaused)
            throw new InvalidOperationException("Capture requires paused actual gameplay.");
        Directory.CreateDirectory(destination);
        var camera=Camera.main;
        var director=MatchDirector.Instance;
        var team=PlayerTeam.Local();
        var cafe=director.CafeOf(team);
        var player=Unity.Netcode.NetworkManager.Singleton.LocalClient.PlayerObject;
        var customers=UnityEngine.Object.FindObjectsByType<Customer>()
            .Where(c=>c.TeamId==team).OrderBy(c=>c.QueueIndex).ToArray();
        var menus=Menus.All.Select(m=>new {id=m.Id.ToString(),name=DisplayNames.Of(m.Id),price=m.BasePrice,parts=m.Parts.Select(p=>p.ToString()).ToArray(),partNames=m.Parts.Select(p=>DisplayNames.Of(p)).ToArray()}).ToArray();
        var orders=customers.Select(c=>{
            var marker=c.GetComponentInChildren<UICustomerOrder>(true);
            var point=camera.WorldToViewportPoint(marker!=null?marker.transform.position:c.transform.position);
            var menu=Menus.All.FirstOrDefault(m=>c.Accepts(Menus.TagsOf(m.Parts),m.Parts.Length));
            return new {kind=c.Kind.ToString(),menu=menu.Id.ToString(),name=DisplayNames.Of(menu.Id),price=UICustomerOrder.ExpectedPrice(menu,c.Kind,null)*Math.Max(1,c.Remaining),fill=1f-c.PatienceRatio,copies=c.Remaining,x=point.x,y=1f-point.y,depth=point.z};
        }).ToArray();
        var shelfMarkers=UnityEngine.Object.FindObjectsByType<UIIngredientBadge>()
            .Where(b=>Cafe.Of(b)==cafe).Select(b=>{
                var shelf=b.GetComponentInParent<IngredientShelf>();
                var point=camera.WorldToViewportPoint(b.transform.position);
                var item=shelf!=null?shelf.SlotItem(0):Ingredient.BreadBase;
                return new {item=item.ToString(),name=DisplayNames.Of(item),count=shelf!=null?shelf.SlotCountAt(0):-1,x=point.x,y=1f-point.y,depth=point.z};
            }).ToArray();
        var held=player.GetComponent<PlayerCarry>().View;
        var heldParts=Enumerable.Range(0,held.PartCount).Select(i=>held.PartAt(i).ToString()).ToArray();
        var metadata=new {capturedAt=DateTimeOffset.UtcNow.ToString("o"),source="Actual Battle_01 Play Mode, existing Main Camera, UI canvases temporarily disabled for base capture",width=Width,height=Height,scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
            camera=new{name=camera.name,position=new[]{camera.transform.position.x,camera.transform.position.y,camera.transform.position.z},rotation=new[]{camera.transform.eulerAngles.x,camera.transform.eulerAngles.y,camera.transform.eulerAngles.z},fov=camera.fieldOfView,aspect=camera.aspect},
            team,totalDays=DayPhases.TotalDays,day=director.Phase.Day,remaining=director.Phase.Remaining,sales=cafe.DaySales,bill=cafe.DayBill,
            revenues=Enumerable.Range(0,director.Board.TeamCount).Select(t=>director.Board.RevenueOf(t)).ToArray(),orders,shelves=shelfMarkers,menus,
            held=new{menu=held.Menu.ToString(),name=DisplayNames.Of(held.Menu),parts=heldParts,product=held.IsProduct,burnt=held.Burnt}};
        File.WriteAllText(Path.Combine(destination,"capture.json"),Newtonsoft.Json.JsonConvert.SerializeObject(metadata,Newtonsoft.Json.Formatting.Indented));
        var enabledCanvases=UnityEngine.Object.FindObjectsByType<Canvas>().Where(c=>c.enabled).ToArray();
        var previousTarget=camera.targetTexture; var previousActive=RenderTexture.active; var previousAspect=camera.aspect;
        var rt=new RenderTexture(Width,Height,24); Texture2D texture=null;
        try {
            foreach(var c in enabledCanvases)c.enabled=false;
            camera.targetTexture=rt;camera.aspect=previousAspect;
            camera.Render();RenderTexture.active=rt;
            texture=new Texture2D(Width,Height,TextureFormat.RGBA32,false);
            texture.ReadPixels(new Rect(0,0,Width,Height),0,0);texture.Apply();
            File.WriteAllBytes(Path.Combine(destination,"day-base.png"),ImageConversion.EncodeToPNG(texture));
        } finally {
            camera.targetTexture=previousTarget;camera.aspect=previousAspect;RenderTexture.active=previousActive;
            foreach(var c in enabledCanvases)if(c!=null)c.enabled=true;
            if(texture!=null)UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);
        }
        RenderIcons(destination);
        return Newtonsoft.Json.JsonConvert.SerializeObject(new{destination,orders=orders.Length,shelves=shelfMarkers.Length,camera=camera.name,canvasesRestored=enabledCanvases.All(c=>c==null||c.enabled)});
    }
    public static string Icons(string destination) {RenderIcons(destination);return "Actual prefab icons rendered; game capture unchanged.";}
    static void RenderIcons(string destination)
    {
        var folder=Path.Combine(destination,"icons");Directory.CreateDirectory(folder);
        var config=Resources.Load<ItemVisualConfig>(ItemVisualConfig.AssetName);
        var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var previousRT=RenderTexture.active;var shaderAsync=ShaderUtil.allowAsyncCompilation;
        var render=new RenderTexture(256,256,24,RenderTextureFormat.ARGB32);var paths=new List<object>();
        ShaderUtil.allowAsyncCompilation=false;
        try {
            var camObj=new GameObject("Gallery icon camera");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camObj,scene);
            var cam=camObj.AddComponent<Camera>();cam.scene=scene;
            var cameraData=camObj.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();cameraData.renderPostProcessing=false;cam.allowHDR=false;
            cam.orthographic=true;cam.orthographicSize=.8f;cam.aspect=1;cam.transform.position=new Vector3(0,0,-5);
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.clear;cam.targetTexture=render;
            foreach(var setting in new[]{new[]{35f,-35f,0f,2.5f},new[]{-20f,150f,0f,1.2f}}){
                var lightObj=new GameObject("Gallery icon light");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightObj,scene);
                var light=lightObj.AddComponent<Light>();light.type=LightType.Directional;light.intensity=setting[3];lightObj.transform.eulerAngles=new Vector3(setting[0],setting[1],setting[2]);
            }
            var views=new List<CarryView>();
            views.AddRange(Enum.GetValues(typeof(MenuId)).Cast<MenuId>().Where(m=>m!=MenuId.None).Select(m=>new CarryView{Menu=m,IsProduct=true,Ingredient=Ingredient.None,HasDish=true}));
            views.AddRange(Enum.GetValues(typeof(Ingredient)).Cast<Ingredient>().Where(i=>i!=Ingredient.None).Select(i=>CarryView.Of(i)));
            foreach(var view in views){
                var reference=config.ReferenceFor(view);if(reference==null)continue;
                var assetPath=AssetDatabase.GUIDToAssetPath(reference.AssetGUID);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);if(prefab==null)continue;
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);go.SetActive(true);go.transform.rotation=Quaternion.Euler(20,155,0);
                var renderers=go.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0){UnityEngine.Object.DestroyImmediate(go);continue;}
                var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);
                go.transform.localScale*=1.25f/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
                bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);go.transform.position-=bounds.center;
                // URP final blit in this preview forces alpha to 1. Export the actual
                // model with a coverage pass (black/white clears), without altering meshes.
                var tex=new Texture2D(256,256,TextureFormat.RGBA32,false);
                cam.backgroundColor=Color.black;cam.Render();RenderTexture.active=render;
                tex.ReadPixels(new Rect(0,0,256,256),0,0);tex.Apply();var black=tex.GetPixels();
                cam.backgroundColor=Color.white;cam.Render();RenderTexture.active=render;
                tex.ReadPixels(new Rect(0,0,256,256),0,0);tex.Apply();var white=tex.GetPixels();
                for(var p=0;p<black.Length;p++){
                    var delta=white[p]-black[p];
                    var alpha=Mathf.Clamp01(1f-Mathf.Max(delta.r,delta.g,delta.b));
                    black[p]=alpha>.002f?new Color(black[p].r/alpha,black[p].g/alpha,black[p].b/alpha,alpha):Color.clear;
                }
                tex.SetPixels(black);tex.Apply();
                var id=view.IsProduct?view.Menu.ToString():view.Ingredient.ToString();
                File.WriteAllBytes(Path.Combine(folder,id+".png"),ImageConversion.EncodeToPNG(tex));paths.Add(new{id,path=assetPath});
                UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(go);
            }
            cam.targetTexture=null;File.WriteAllText(Path.Combine(destination,"icon-sources.json"),Newtonsoft.Json.JsonConvert.SerializeObject(paths,Newtonsoft.Json.Formatting.Indented));
        } finally {ShaderUtil.allowAsyncCompilation=shaderAsync;RenderTexture.active=previousRT;UnityEngine.Object.DestroyImmediate(render);UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
    }
}
