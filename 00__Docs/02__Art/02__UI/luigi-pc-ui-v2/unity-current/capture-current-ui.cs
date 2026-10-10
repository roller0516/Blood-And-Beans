// Gallery-only read-only reconstruction from the project's current prefabs.
// Sample values are not a networked Play Mode session. No assets are saved.
var destination="C:/Users/eatyourmeat/Desktop/Blood_And_Beans/Blood-And-Beans/00__Docs/02__Art/02__UI/luigi-pc-ui-v2/unity-current";
System.IO.Directory.CreateDirectory(destination);
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
System.Func<object,string,object> field=(o,n)=>o.GetType().GetField(n,flags)?.GetValue(o);
System.Action<object,string,string> setText=(o,n,value)=>{if(field(o,n) is TMPro.TMP_Text t)t.text=value;};
System.Action<object,string,bool> show=(o,n,on)=>{var v=field(o,n);if(v is UnityEngine.GameObject go)go.SetActive(on);else if(v is UnityEngine.Component c)c.gameObject.SetActive(on);};
var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
var previousRT=UnityEngine.RenderTexture.active;var shaderAsync=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false;
var render=new UnityEngine.RenderTexture(1920,1080,24);var results=new System.Collections.Generic.List<object>();
var clonedFonts=new System.Collections.Generic.Dictionary<TMPro.TMP_FontAsset,TMPro.TMP_FontAsset>();
var clones=new System.Collections.Generic.List<UnityEngine.Object>();
try{
  var cameraGo=new UnityEngine.GameObject("Current UI reference camera");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraGo,scene);
  var camera=cameraGo.AddComponent<UnityEngine.Camera>();camera.scene=scene;camera.orthographic=true;camera.orthographicSize=540;camera.aspect=1920f/1080f;camera.transform.position=new UnityEngine.Vector3(0,0,-100);camera.nearClipPlane=.1f;camera.farClipPlane=1000;camera.cullingMask=1<<31;camera.clearFlags=UnityEngine.CameraClearFlags.SolidColor;camera.backgroundColor=new UnityEngine.Color(.06f,.08f,.08f);camera.targetTexture=render;
  var root=new UnityEngine.GameObject("Reference canvas",typeof(UnityEngine.RectTransform),typeof(UnityEngine.Canvas));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);root.layer=31;
  var canvas=root.GetComponent<UnityEngine.Canvas>();canvas.renderMode=UnityEngine.RenderMode.WorldSpace;canvas.worldCamera=camera;root.GetComponent<UnityEngine.RectTransform>().sizeDelta=new UnityEngine.Vector2(1920,1080);
  var backgrounds=new System.Collections.Generic.Dictionary<string,UnityEngine.Texture2D>();
  foreach(var name in new[]{"environment-cafe","environment-crew","Battle_01-camera"}){var tex=new UnityEngine.Texture2D(2,2);UnityEngine.ImageConversion.LoadImage(tex,System.IO.File.ReadAllBytes(System.IO.Path.Combine(destination,name+".png")));backgrounds[name]=tex;clones.Add(tex);}
  System.Action<string> backdrop=name=>{var go=new UnityEngine.GameObject("Actual object preview",typeof(UnityEngine.RectTransform),typeof(UnityEngine.UI.RawImage));go.transform.SetParent(root.transform,false);go.GetComponent<UnityEngine.UI.RawImage>().texture=backgrounds[name];var rt=go.GetComponent<UnityEngine.RectTransform>();rt.anchorMin=UnityEngine.Vector2.zero;rt.anchorMax=UnityEngine.Vector2.one;rt.sizeDelta=UnityEngine.Vector2.zero;};
  System.Action<UnityEngine.GameObject> prepare=go=>{
    foreach(var t in go.GetComponentsInChildren<UnityEngine.Transform>(true))t.gameObject.layer=31;
    foreach(var c in go.GetComponentsInChildren<UnityEngine.Canvas>(true)){c.enabled=true;c.renderMode=UnityEngine.RenderMode.WorldSpace;c.worldCamera=camera;}
    foreach(var s in go.GetComponentsInChildren<UnityEngine.UI.CanvasScaler>(true))s.enabled=false;
    foreach(var g in go.GetComponentsInChildren<UnityEngine.CanvasGroup>(true))g.alpha=1;
    foreach(var t in go.GetComponentsInChildren<TMPro.TMP_Text>(true)){
      if(t.font!=null){if(!clonedFonts.TryGetValue(t.font,out var clone)){clone=UnityEngine.Object.Instantiate(t.font);clone.atlasPopulationMode=TMPro.AtlasPopulationMode.Static;clonedFonts[t.font]=clone;clones.Add(clone);}t.font=clone;}
      t.ForceMeshUpdate(true,true);
    }
  };
  System.Func<string,string,UnityEngine.Vector2,UnityEngine.GameObject> part=(prefab,folder,position)=>{
    var p=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Art/UI/Prefabs/"+folder+"/"+prefab+".prefab");
    if(p==null)throw new System.Exception("Missing prefab "+prefab);
    var go=(UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(p,scene);go.transform.SetParent(root.transform,false);go.SetActive(true);
    var rt=go.GetComponent<UnityEngine.RectTransform>();rt.anchorMin=rt.anchorMax=new UnityEngine.Vector2(.5f,.5f);rt.anchoredPosition=position;rt.localScale=UnityEngine.Vector3.one;
    if(folder!="Parts"){rt.anchorMin=UnityEngine.Vector2.zero;rt.anchorMax=UnityEngine.Vector2.one;rt.sizeDelta=UnityEngine.Vector2.zero;}
    return go;
  };
  System.Action<string> save=id=>{
    prepare(root);UnityEngine.Canvas.ForceUpdateCanvases();foreach(var rt in root.GetComponentsInChildren<UnityEngine.RectTransform>(true))if(rt.gameObject.activeInHierarchy)UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);UnityEngine.Canvas.ForceUpdateCanvases();camera.Render();UnityEngine.RenderTexture.active=render;
    var png=new UnityEngine.Texture2D(1920,1080,UnityEngine.TextureFormat.RGBA32,false);png.ReadPixels(new UnityEngine.Rect(0,0,1920,1080),0,0);png.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(destination,"current-"+id+".png"),UnityEngine.ImageConversion.EncodeToPNG(png));UnityEngine.Object.DestroyImmediate(png);results.Add(new{id,source="Current Unity prefabs with sample data; editor preview",width=1920,height=1080});
  };
  var names=new[]{"JinKyu","Mina","Noah","Yuna","Haru","Sora","Leon","Nari"};
  var members=names.Select((n,i)=>new SteamLobby.RoomMember((ulong)(i+1),n,i/2,i==0,i==0,i<6,i%5)).ToArray();
  var stands=new[]{new MatchHudModel.Standing{Team=0,Name="우리",Revenue=1240,Mine=true},new MatchHudModel.Standing{Team=1,Name="B",Revenue=1180},new MatchHudModel.Standing{Team=2,Name="C",Revenue=980},new MatchHudModel.Standing{Team=3,Name="D",Revenue=720}};
  System.Func<bool,MatchHudModel> hudModel=day=>new MatchHudModel{IsDay=day,RentMet=true,DayRemaining=84,DayTimeRatio=.47f,DayCounter="3/7",DaySales=340,DayBill=120,Standings=stands,Day="3일차",PhaseName=day?"낮 영업":"야간 탐색",Timer=day?"01:24":"01:37.926",Team="팀 1",Ping="핑 32ms",Revenue="팀 매출 1,240G",Details="",ShowBag=!day,BagPercent="가방 용량 42%",BagWeight="3.4 / 8.0 KG",BagRatio=.42f,BagBand=0,ShowDash=!day,DashKey="SPACE",DashTime="",Prompt=day?"[F] 조리 완료":"[F] 상자 열기"};
  var spec=new[]{"title|UITitleMenuScreen|Screen","rooms|UIRoomListScreen|Screen","crew|UICharacterSelectScreen|Screen","room|UIRoomScreen|Screen","day|UIMatchHudScreen|Screen","recipe|UIMatchHudScreen|Screen","orders|UICustomerOrder|Parts","making|UIMakingCard|Parts","night|UIMatchHudScreen|Screen","loot|UIBoxLootPopup|Popup","night-parts|UIMatchHudScreen|Screen","return|UIReturnResultPopup|Popup","settlement|UIDaySettlementScreen|Screen","final|UIMatchResultPopup|Popup","settings|UISettingsPopup|Popup","loading|UILoadingPopup|Popup","cue|UIPhaseCuePopup|Popup","shared|UICharacterCard|Parts"};
  foreach(var item in spec){
    foreach(UnityEngine.Transform child in root.transform.Cast<UnityEngine.Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
    var s=item.Split('|');var id=s[0];
    if(id=="crew")backdrop("environment-crew");
    else if(new[]{"day","recipe","orders","settings"}.Contains(id))backdrop("environment-cafe");
    else if(new[]{"night","loot","night-parts","return","loading","cue"}.Contains(id))backdrop("Battle_01-camera");
    var go=part(s[1],s[2],UnityEngine.Vector2.zero);
    if(id=="title")go.GetComponent<UITitleMenuScreen>().Bind("BLOOD & BEANS",null,null,null);
    if(id=="rooms"){var ui=go.GetComponent<UIRoomListScreen>();ui.Bind(null,null,null,null,null,4);ui.Render("참여할 방을 골라주세요",new[]{new LobbyRoom(1,1,"유령 카페",6,8),new LobbyRoom(2,2,"밤의 원두",4,6),new LobbyRoom(3,3,"커피 한 잔",2,4)},0);}
    if(id=="crew"){var ui=go.GetComponent<UICharacterSelectScreen>();ui.Bind(System.Array.Empty<UICharacterSelectScreen.Claim>(),0,0,"JinKyu","밤 / 탐색 스킬","캐릭터와 팀을 고른 뒤 준비해 주세요.",null,null,null,null);ui.SetLobby(false,false,true,6,8);}
    if(id=="room"){var ui=go.GetComponent<UIRoomScreen>();ui.Bind(null,null,null,null);ui.BuildTeams(4);ui.Render("유령 카페","모두 준비하면 시작합니다",members,0,2,true,false,_=>2,_=>false,true,6,8);}
    if(id=="day"||id=="recipe"||id=="night"||id=="night-parts"){
      var ui=go.GetComponent<UIMatchHudScreen>();var day=id=="day"||id=="recipe";ui.Render(hudModel(day));
      foreach(var n in new[]{"recipePanel","returnBox","castBar","completionBar"})show(ui,n,false);
      foreach(var j in go.GetComponentsInChildren<UICraftingJudgement>(true))j.gameObject.SetActive(false);
      if(id=="recipe"){show(ui,"recipePanel",true);var rows=field(ui,"recipeRows") as UIRecipeRow[];if(rows!=null){var definitions=Menus.All;for(var i=0;i<rows.Length&&i<definitions.Length;i++){rows[i].gameObject.SetActive(true);rows[i].Bind(definitions[i]);}}}
      if(id=="day"){var gems=go.GetComponentsInChildren<UIGemIcon>(true);for(var i=0;i<gems.Length;i++)gems[i].Render(ResourceManager.Instance.IngredientSprite((Ingredient)((int)Ingredient.WindGem+i)),3-i%3);}
      if(id=="night-parts"){ui.SetCastProgress(.62f);show(ui,"returnBox",true);setText(ui,"returnText","귀환 위치로 돌아가세요");}
    }
    if(id=="orders"){
      var menus=new[]{MenuId.HotAmericano,MenuId.CafeLatte,MenuId.CafeMocha,MenuId.IcedAmericano};
      for(var i=0;i<4;i++){var card=i==0?go:part("UICustomerOrder","Parts",UnityEngine.Vector2.zero);card.GetComponent<UnityEngine.RectTransform>().anchoredPosition=new UnityEngine.Vector2((i-1.5f)*360,80);var bubble=card.GetComponentInChildren<UIBubbleFill>(true);bubble.Show(new[]{.22f,.56f,.88f,.38f}[i],i==2?UITheme.Red:i==1?UITheme.Gold:UITheme.Green);card.GetComponentInChildren<UINamedItemIcon>(true).Show(ResourceManager.Instance.MenuSprite(menus[i]),DisplayNames.Of(menus[i]),i==3?"×2":null,true,false);var ui=card.GetComponent<UICustomerOrder>();setText(ui,"expectedPrice",new[]{22,30,36,52}[i].ToString());}
    }
    if(id=="making"){
      for(var i=0;i<5;i++){var card=i==0?go:part("UIMakingCard","Parts",UnityEngine.Vector2.zero);card.GetComponent<UnityEngine.RectTransform>().anchoredPosition=new UnityEngine.Vector2((i-2)*345,120);var ingredients=i==0?new[]{Ingredient.Bean}:i==3?new[]{Ingredient.Bean,Ingredient.Milk,Ingredient.BloodBean}:new[]{Ingredient.Bean,Ingredient.Milk,Ingredient.Chocolate};var held=new HeldItem{Ingredient=Ingredient.Bean,HasDish=true,Recipe=ingredients,Menu=i>=2?MenuId.CafeMocha:MenuId.None,IsProduct=i==2||i==4,Burnt=i==4};card.GetComponent<UIMakingCard>().Render(CarryView.Of(held));}
      var gauge=part("UICompletionGauge","Parts",new UnityEngine.Vector2(0,-120));var uiGauge=gauge.GetComponent<UICompletionGauge>();if(field(uiGauge,"label") is TMPro.TMP_Text label)label.text="F · 6.3초";
      // Judgement is a nested prefab component, not a standalone prefab.
      var j=gauge.GetComponentInChildren<UICraftingJudgement>(true);if(j!=null){setText(j,"label","Perfect!");var rt=field(j,"view") as UnityEngine.RectTransform;if(rt!=null)rt.anchoredPosition+=new UnityEngine.Vector2(0,110);}
    }
    if(id=="loot"){var ui=go.GetComponent<UIBoxLootPopup>();setText(ui,"title","상자");setText(ui,"tierLabel","일반");setText(ui,"revealValue","3 / 5");setText(ui,"bagPercent","42%");setText(ui,"bagWeight","3.4 / 8.0 KG");var slots=go.GetComponentsInChildren<UIBoxLootSlot>(true);for(var i=0;i<slots.Length;i++){if(i<3){var ing=new[]{Ingredient.Bean,Ingredient.Milk,Ingredient.Chocolate}[i];slots[i].ShowItem(DisplayNames.Of(ing),"×2","0.4 KG",ResourceManager.Instance.IngredientSprite(ing),IngredientRarity.Common);}else slots[i].ShowHidden("IN 1s");}}
    if(id=="return"){var ui=go.GetComponent<UIReturnResultPopup>();ui.Bind(ReturnOutcome.Returned,8,0,50);save(id);ui.Bind(ReturnOutcome.PartialLoss,4,4,50);save(id+"-1");ui.Bind(ReturnOutcome.BagLost,0,8,50);save(id+"-2");continue;}
    if(id=="settlement"){var ui=go.GetComponent<UIDaySettlementScreen>();ui.Bind(3,340,120,120,0,stands.Select(x=>new UIDaySettlementScreen.StandingRow(x.Name,x.Revenue,x.Mine)).ToArray(),new[]{new UIDaySettlementScreen.GemRow(ResourceManager.Instance.IngredientSprite(Ingredient.UpgradePart),"불씨","조리 시간 감소",3,false)},new[]{3,2,2,1,1,1},new[]{ResourceManager.Instance.IngredientSprite(Ingredient.Milk),ResourceManager.Instance.IngredientSprite(Ingredient.Chocolate)});ui.SetRemaining(8,10);}
    if(id=="final")go.GetComponent<UIMatchResultPopup>().Bind(7,new[]{18420,16750,15200,12800},0,null,null,null);
    if(id=="loading"){var ui=go.GetComponent<UILoadingPopup>();ui.SetProgress(0,0);save(id);ui.SetProgress(6,8);save(id+"-1");continue;}
    if(id=="cue"){var ui=go.GetComponent<UIPhaseCuePopup>();var labels=new[]{"3","2","1","READY","GO!","마감!"};for(var i=0;i<labels.Length;i++){setText(ui,"label",labels[i]);save(i==0?id:id+"-"+i);}continue;}
    if(id=="shared"){
      var parts=new[]{"UICharacterCard","UICharacterNameplate","UIPlayerNameplate","UITeamSlot","UIStandingRow","UIStandingRowClock","UISettlementCard","UISettlementLine","UIGemRow","UIGemIcon","UIPanelTab","UIRecipeRow","UIVolumeSlider"};
      for(var i=0;i<parts.Length;i++){var card=i==0?go:part(parts[i],"Parts",UnityEngine.Vector2.zero);card.GetComponent<UnityEngine.RectTransform>().anchoredPosition=new UnityEngine.Vector2((i%3-1)*550,360-i/3*180);if(card.TryGetComponent<UICharacterCard>(out var cc)){cc.Bind("도깨비",ResourceManager.Instance.CrewSprite(CharacterId.Dokkaebi),null);cc.SetSelected(true,UITheme.Red);}if(card.TryGetComponent<UITeamSlot>(out var ts))ts.Bind("팀 1",UITheme.Red,null);}
    }
    save(id);
  }
  System.IO.File.WriteAllText(System.IO.Path.Combine(destination,"current-captures.json"),Newtonsoft.Json.JsonConvert.SerializeObject(results,Newtonsoft.Json.Formatting.Indented));camera.targetTexture=null;
}finally{
  UnityEditor.ShaderUtil.allowAsyncCompilation=shaderAsync;UnityEngine.RenderTexture.active=previousRT;UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);UnityEngine.Object.DestroyImmediate(render);foreach(var c in clones)UnityEngine.Object.DestroyImmediate(c);
}
return Newtonsoft.Json.JsonConvert.SerializeObject(new{count=results.Count,destination});
