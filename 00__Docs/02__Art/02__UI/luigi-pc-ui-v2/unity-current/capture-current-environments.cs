// Actual project environment and character assets, in unsaved preview scenes.
var destination="C:/Users/eatyourmeat/Desktop/Blood_And_Beans/Blood-And-Beans/00__Docs/02__Art/02__UI/luigi-pc-ui-v2/unity-current";
var asyncShader=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false;
var previousRT=UnityEngine.RenderTexture.active;
var records=new System.Collections.Generic.List<object>();
try{
  foreach(var kind in new[]{"cafe","crew"}){
    var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();var render=new UnityEngine.RenderTexture(1920,1080,24);
    try{
      var path=kind=="cafe"?"Assets/Art/Environment/Prefabs/Cafe.prefab":"Assets/Art/Environment/Prefabs/CharacterStage.prefab";
      var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
      var set=(UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab,scene);set.SetActive(true);
      if(kind=="crew")set.transform.position-=new UnityEngine.Vector3(0,-1000,0);
      foreach(var cam in set.GetComponentsInChildren<UnityEngine.Camera>(true))cam.enabled=false;
      var cameraGo=new UnityEngine.GameObject("Implementation reference camera");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraGo,scene);var camera=cameraGo.AddComponent<UnityEngine.Camera>();cameraGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();camera.scene=scene;camera.targetTexture=render;camera.fieldOfView=38;camera.aspect=1920f/1080f;camera.nearClipPlane=.1f;camera.farClipPlane=3000;camera.clearFlags=UnityEngine.CameraClearFlags.SolidColor;camera.backgroundColor=new UnityEngine.Color(.04f,.06f,.06f);
      UnityEngine.Vector3 target;
      if(kind=="crew"){
        // Keep the reference focused on the currently wired character models.
        // Stage legacy built-in materials are not compatible with this URP preview.
        foreach(var r in set.GetComponentsInChildren<UnityEngine.Renderer>(true))r.enabled=false;
        var seats=set.GetComponentsInChildren<UnityEngine.Transform>(true).Where(t=>System.Text.RegularExpressions.Regex.IsMatch(t.name,"^Seat[0-7]$")).OrderBy(t=>t.name).ToArray();
        var config=UnityEngine.Resources.Load<CharacterVisualConfig>(CharacterVisualConfig.AssetName);
        for(var i=0;i<seats.Length;i++){
          var id=CharacterCatalog.All[i%CharacterCatalog.All.Length].Id;var reference=config.ReferenceFor(id);var modelPath=UnityEditor.AssetDatabase.GUIDToAssetPath(reference.AssetGUID);var model=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(modelPath);var go=(UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(model,scene);go.transform.SetParent(seats[i],false);config.ApplyTransform(id,go.transform);go.SetActive(true);go.GetComponent<CharacterModel>()?.Tint(TeamColors.Of(i/2),1);foreach(var a in go.GetComponentsInChildren<UnityEngine.Animator>(true)){a.Rebind();a.Update(0);}go.transform.rotation=UnityEngine.Quaternion.identity;foreach(var r in go.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true))r.updateWhenOffscreen=true;
        }
        target=seats.Select(t=>t.position).Aggregate(UnityEngine.Vector3.zero,(a,b)=>a+b)/seats.Length+UnityEngine.Vector3.up;
        camera.transform.position=target+new UnityEngine.Vector3(0,3,17);camera.transform.LookAt(target);
      }else{
        var rs=set.GetComponentsInChildren<UnityEngine.Renderer>(false);var bounds=rs[0].bounds;foreach(var r in rs)bounds.Encapsulate(r.bounds);target=bounds.center;target.y=1;
        camera.transform.position=target+new UnityEngine.Vector3(0,18,-12);camera.transform.LookAt(target);
      }
      var lightGo=new UnityEngine.GameObject("Reference daylight");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightGo,scene);var light=lightGo.AddComponent<UnityEngine.Light>();light.type=UnityEngine.LightType.Directional;light.intensity=1.3f;light.transform.eulerAngles=new UnityEngine.Vector3(45,-30,0);
      camera.Render();UnityEngine.RenderTexture.active=render;var png=new UnityEngine.Texture2D(1920,1080,UnityEngine.TextureFormat.RGBA32,false);png.ReadPixels(new UnityEngine.Rect(0,0,1920,1080),0,0);png.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(destination,"environment-"+kind+".png"),UnityEngine.ImageConversion.EncodeToPNG(png));UnityEngine.Object.DestroyImmediate(png);camera.targetTexture=null;records.Add(new{kind,path,target=target.ToString(),camera=camera.transform.position.ToString(),projection="perspective 38 degrees",source="actual prefab; gallery preview camera; crew stage meshes omitted because legacy materials do not support this preview"});
    }finally{UnityEngine.Object.DestroyImmediate(render);UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
  }
  System.IO.File.WriteAllText(System.IO.Path.Combine(destination,"current-environments.json"),Newtonsoft.Json.JsonConvert.SerializeObject(records,Newtonsoft.Json.Formatting.Indented));
}finally{UnityEditor.ShaderUtil.allowAsyncCompilation=asyncShader;UnityEngine.RenderTexture.active=previousRT;}
return Newtonsoft.Json.JsonConvert.SerializeObject(records);

