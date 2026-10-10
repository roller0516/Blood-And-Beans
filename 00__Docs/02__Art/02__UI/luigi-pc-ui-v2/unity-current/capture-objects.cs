// Read-only object reference capture; no scene or prefab is saved.
var destination = "C:/Users/eatyourmeat/Desktop/Blood_And_Beans/Blood-And-Beans/00__Docs/02__Art/02__UI/luigi-pc-ui-v2/unity-current";
System.IO.Directory.CreateDirectory(destination);
var config = UnityEngine.Resources.Load<ItemVisualConfig>(ItemVisualConfig.AssetName);
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var previousRT = UnityEngine.RenderTexture.active;
var asyncShader = UnityEditor.ShaderUtil.allowAsyncCompilation;
UnityEditor.ShaderUtil.allowAsyncCompilation=false;
var rows = new System.Collections.Generic.List<object>();
var render = new UnityEngine.RenderTexture(1920,1080,24);
try {
  var camObj=new UnityEngine.GameObject("Object reference camera");
  UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camObj,scene);
  var camera=camObj.AddComponent<UnityEngine.Camera>();camera.scene=scene;
  camObj.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
  camera.orthographic=true;camera.orthographicSize=6.5f;camera.aspect=1920f/1080f;
  camera.transform.position=new UnityEngine.Vector3(0,0,-30);camera.nearClipPlane=.1f;camera.farClipPlane=100;
  camera.clearFlags=UnityEngine.CameraClearFlags.SolidColor;camera.backgroundColor=new UnityEngine.Color(.17f,.20f,.19f,1);camera.targetTexture=render;
  foreach(var setting in new[]{new[]{35f,-35f,0f,2.5f},new[]{-20f,150f,0f,1.2f}}){
    var lightObj=new UnityEngine.GameObject("Reference light");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightObj,scene);
    var light=lightObj.AddComponent<UnityEngine.Light>();light.type=UnityEngine.LightType.Directional;light.intensity=setting[3];lightObj.transform.eulerAngles=new UnityEngine.Vector3(setting[0],setting[1],setting[2]);
  }
  var views=new System.Collections.Generic.List<CarryView>();
  foreach(var menu in System.Enum.GetValues(typeof(MenuId)).Cast<MenuId>().Where(x=>x!=MenuId.None))views.Add(new CarryView{Menu=menu,IsProduct=true,Ingredient=Ingredient.None,HasDish=true});
  foreach(var ingredient in System.Enum.GetValues(typeof(Ingredient)).Cast<Ingredient>().Where(x=>x!=Ingredient.None))views.Add(CarryView.Of(ingredient));
  views.Add(new CarryView{HasDish=true,Ingredient=Ingredient.None,Menu=MenuId.None});
  views.Add(new CarryView{HasDish=true,DishIsPlate=true,Ingredient=Ingredient.None,Menu=MenuId.None});
  views.Add(new CarryView{HasDish=true,Ingredient=Ingredient.None,IsProduct=true,Menu=MenuId.CafeMocha,Burnt=true});
  var font=UnityEngine.Resources.GetBuiltinResource<UnityEngine.Font>("LegacyRuntime.ttf");
  for(var i=0;i<views.Count;i++){
    var view=views[i];var reference=config.ReferenceFor(view);if(reference==null)continue;
    var path=UnityEditor.AssetDatabase.GUIDToAssetPath(reference.AssetGUID);var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
    var go=(UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab,scene);go.SetActive(true);
    go.transform.rotation=UnityEngine.Quaternion.Euler(20,155,0);
    if(view.Burnt)foreach(var r in go.GetComponentsInChildren<UnityEngine.Renderer>(true))r.sharedMaterials=r.sharedMaterials.Select(m=>config.StateOf(m,false,true,false)).ToArray();
    var bounds=new UnityEngine.Bounds();var first=true;foreach(var r in go.GetComponentsInChildren<UnityEngine.Renderer>(true)){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
    var scale=1.35f/UnityEngine.Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);go.transform.localScale*=scale;
    bounds=new UnityEngine.Bounds();first=true;foreach(var r in go.GetComponentsInChildren<UnityEngine.Renderer>(true)){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
    var cell=rows.Count;var center=new UnityEngine.Vector3((cell%6-2.5f)*3f,(1.5f-cell/6)*2.4f+.3f,0);go.transform.position+=center-bounds.center;
    var label= new UnityEngine.GameObject("Object label");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(label,scene);
    var text=label.AddComponent<UnityEngine.TextMesh>();text.font=font;text.GetComponent<UnityEngine.MeshRenderer>().sharedMaterial=font.material;text.characterSize=.11f;text.fontSize=40;text.anchor=UnityEngine.TextAnchor.MiddleCenter;text.color=new UnityEngine.Color(.93f,.90f,.8f);text.text=view.Burnt?"Burnt CafeMocha":view.IsProduct?view.Menu.ToString():view.HasDish?(view.DishIsPlate?"Empty plate":"Empty cup"):view.Ingredient.ToString();label.transform.position=center+new UnityEngine.Vector3(0,-.93f,-2);
    rows.Add(new{index=i,label=text.text,path,burnt=view.Burnt});
  }
  camera.Render();UnityEngine.RenderTexture.active=render;
  var texture=new UnityEngine.Texture2D(1920,1080,UnityEngine.TextureFormat.RGBA32,false);texture.ReadPixels(new UnityEngine.Rect(0,0,1920,1080),0,0);texture.Apply();
  System.IO.File.WriteAllBytes(System.IO.Path.Combine(destination,"implemented-objects.png"),UnityEngine.ImageConversion.EncodeToPNG(texture));UnityEngine.Object.DestroyImmediate(texture);
  System.IO.File.WriteAllText(System.IO.Path.Combine(destination,"implemented-objects.json"),Newtonsoft.Json.JsonConvert.SerializeObject(rows,Newtonsoft.Json.Formatting.Indented));
  camera.targetTexture=null;
}finally{UnityEditor.ShaderUtil.allowAsyncCompilation=asyncShader;UnityEngine.RenderTexture.active=previousRT;UnityEngine.Object.DestroyImmediate(render);UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
return Newtonsoft.Json.JsonConvert.SerializeObject(new{count=rows.Count,folder=destination});
