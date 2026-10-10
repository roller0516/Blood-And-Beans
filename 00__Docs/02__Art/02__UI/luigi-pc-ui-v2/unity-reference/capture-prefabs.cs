// Read-only prefab rendering. Everything lives in an unsaved preview scene.
var destination = "C:/Users/eatyourmeat/Desktop/Blood_And_Beans/output/unity-ui-reference";
System.IO.Directory.CreateDirectory(destination);
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var results = new System.Collections.Generic.List<object>();
try {
  var cameraObject = new UnityEngine.GameObject("UI reference camera");
  UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
  var camera = cameraObject.AddComponent<UnityEngine.Camera>();
  camera.scene = scene;
  camera.orthographic = true;
  camera.orthographicSize = 540;
  camera.aspect = 1920f/1080f;
  camera.clearFlags = UnityEngine.CameraClearFlags.SolidColor;
  camera.backgroundColor = new UnityEngine.Color(0.025f,0.03f,0.04f,1);
  camera.transform.position = new UnityEngine.Vector3(0,0,-100);
  camera.nearClipPlane = 0.1f;
  camera.farClipPlane = 1000;
  camera.cullingMask = 1 << 31;
  var root = new UnityEngine.GameObject("UI reference canvas", typeof(UnityEngine.RectTransform), typeof(UnityEngine.Canvas), typeof(UnityEngine.UI.CanvasScaler));
  UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
  var canvas = root.GetComponent<UnityEngine.Canvas>();
  canvas.renderMode = UnityEngine.RenderMode.WorldSpace;
  canvas.worldCamera = camera;
  canvas.planeDistance = 100;
  var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
  scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
  scaler.referenceResolution = new UnityEngine.Vector2(1920,1080);
  scaler.matchWidthOrHeight = 0.5f;
  scaler.enabled=false;
  root.GetComponent<UnityEngine.RectTransform>().sizeDelta=new UnityEngine.Vector2(1920,1080);
  root.transform.localScale=UnityEngine.Vector3.one;
  var render = new UnityEngine.RenderTexture(1920,1080,24);
  camera.targetTexture = render;
  foreach(var folder in new[] {"Screen", "Popup", "Parts"}) {
    foreach(var guid in UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[]{"Assets/Art/UI/Prefabs/"+folder}).SelectMany(g=>UnityEditor.AssetDatabase.GUIDToAssetPath(g).EndsWith("UIMatchHudScreen.prefab")?new[]{g+"|day",g+"|night",g+"|recipe"}:new[]{g})) {
      var pieces=guid.Split('|');var path=UnityEditor.AssetDatabase.GUIDToAssetPath(pieces[0]);
      var variant=pieces.Length>1?pieces[1]:"";
      var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
      var instance=(UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab,scene);
      instance.transform.SetParent(root.transform,false);
      instance.SetActive(true);
      foreach(var nested in instance.GetComponentsInChildren<UnityEngine.Canvas>(true)) {nested.renderMode=UnityEngine.RenderMode.WorldSpace;nested.worldCamera=camera;}
      var instanceRect=instance.GetComponent<UnityEngine.RectTransform>();
      instanceRect.anchorMin=folder=="Parts"?new UnityEngine.Vector2(.5f,.5f):UnityEngine.Vector2.zero;
      instanceRect.anchorMax=folder=="Parts"?new UnityEngine.Vector2(.5f,.5f):UnityEngine.Vector2.one;
      if(folder!="Parts")instanceRect.sizeDelta=UnityEngine.Vector2.zero;
      instanceRect.anchoredPosition=UnityEngine.Vector2.zero;
      instanceRect.localScale=UnityEngine.Vector3.one;
      if(prefab.name=="UIMatchHudScreen") {
        var hud=instance.GetComponent<UIMatchHudScreen>();
        var fieldFlags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        System.Action<string,bool> show=(name,on)=>{var obj=typeof(UIMatchHudScreen).GetField(name,fieldFlags).GetValue(hud); if(obj is UnityEngine.GameObject go)go.SetActive(on);else if(obj is UnityEngine.Component component)component.gameObject.SetActive(on);};
        foreach(var name in new[]{"recipePanel","promptBox","returnBox","castBar","completionBar"})show(name,false);
        foreach(var judgement in instance.GetComponentsInChildren<UICraftingJudgement>(true))judgement.gameObject.SetActive(false);
        var day=variant!="night";
        var model=new MatchHudModel {IsDay=day,RentMet=true,DayCounter="3/7",DaySales=340,DayBill=120,DayRemaining=84,DayTimeRatio=0.47f,Day="3일차",PhaseName="야간 탐색",Timer=day?"01:24":"01:37.926",Team="팀 1",Ping="핑 32ms",Revenue="팀 매출 1,240G",Details="",ShowBag=!day,BagPercent="가방 용량 42%",BagWeight="3.4 / 8.0 KG",BagRatio=0.42f,BagBand=0,ShowDash=!day,DashKey="SPACE",DashTime="",Standings=new[]{new MatchHudModel.Standing{Team=0,Name="우리",Revenue=1240,Mine=true},new MatchHudModel.Standing{Team=1,Name="B",Revenue=1180},new MatchHudModel.Standing{Team=2,Name="C",Revenue=980},new MatchHudModel.Standing{Team=3,Name="D",Revenue=720}}};
        hud.Render(model);
        if(variant=="recipe")show("recipePanel",true);
      }
      foreach(var trans in instance.GetComponentsInChildren<UnityEngine.Transform>(true))trans.gameObject.layer=31;
      root.layer=31;
      foreach(var group in instance.GetComponentsInChildren<UnityEngine.CanvasGroup>(true))group.alpha=1;
      UnityEngine.Canvas.ForceUpdateCanvases();
      UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(instance.GetComponent<UnityEngine.RectTransform>());
      foreach(var text in instance.GetComponentsInChildren<TMPro.TMP_Text>(true))text.ForceMeshUpdate(true,true);
      UnityEngine.Canvas.ForceUpdateCanvases();
      camera.Render();
      var previous=UnityEngine.RenderTexture.active;
      UnityEngine.RenderTexture.active=render;
      var png=new UnityEngine.Texture2D(1920,1080,UnityEngine.TextureFormat.RGBA32,false);
      png.ReadPixels(new UnityEngine.Rect(0,0,1920,1080),0,0);
      png.Apply();
      var captureName=prefab.name+(variant==""?"":"-"+variant);
      var save=System.IO.Path.Combine(destination,captureName+".png");
      System.IO.File.WriteAllBytes(save,UnityEngine.ImageConversion.EncodeToPNG(png));
      UnityEngine.RenderTexture.active=previous;
      var elements=new System.Collections.Generic.List<object>();
      foreach(var rect in instance.GetComponentsInChildren<UnityEngine.RectTransform>(true)) {
        var corners=new UnityEngine.Vector3[4];rect.GetWorldCorners(corners);
        var a=camera.WorldToViewportPoint(corners[0]);var b=camera.WorldToViewportPoint(corners[2]);
        var text=rect.GetComponent<TMPro.TMP_Text>();
        elements.Add(new {name=rect.name,active=rect.gameObject.activeInHierarchy,x=a.x*1920,y=(1-b.y)*1080,w=(b.x-a.x)*1920,h=(b.y-a.y)*1080,text=text==null?null:text.text,font=text==null?0:text.fontSize});
      }
      results.Add(new {name=captureName,path,save,elements});
      UnityEngine.Object.DestroyImmediate(png);
      UnityEngine.Object.DestroyImmediate(instance);
    }
  }
  camera.targetTexture=null;
  UnityEngine.Object.DestroyImmediate(render);
  System.IO.File.WriteAllText(System.IO.Path.Combine(destination,"prefab-layouts.json"),Newtonsoft.Json.JsonConvert.SerializeObject(results,Newtonsoft.Json.Formatting.Indented));
} finally {UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
return new {count=results.Count,folder=destination};
