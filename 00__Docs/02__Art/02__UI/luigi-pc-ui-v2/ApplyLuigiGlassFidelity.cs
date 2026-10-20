using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// 승인 시안의 유리 표면을 실제 공유 스프라이트로 적용. 프리팹 GUID/입력/데이터 참조 유지.
public static class ApplyLuigiGlassFidelity
{
    const string Kit="Assets/Art/UI/Sprites/Common/LuigiGlassKit.png";
    const string Root="Assets/Art/UI/Prefabs/";
    const string Fonts="Assets/Art/Fonts/";
    static readonly Vector2 TL=new(0,1),TC=new(.5f,1),TR=Vector2.one,C=new(.5f,.5f),BL=Vector2.zero,BR=new(1,0);
    static readonly Color Ink=new(.15f,.075f,.23f),White=new(1,.98f,1),Lime=new(.70f,.95f,.47f);
    static Dictionary<string,Sprite> sprites;
    static int textured;
    static SpriteRect Rect(string name,int x,int top,int width,int height,int border)
        =>new SpriteRect{name=name,rect=new Rect(x,1024-top-height,width,height),pivot=C,alignment=(int)SpriteAlignment.Center,
            border=new Vector4(border,border,border,border),spriteID=GUID.Generate()};
    public static string Import()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        AssetDatabase.ImportAsset(Kit,ImportAssetOptions.ForceSynchronousImport);
        var ti=(TextureImporter)AssetImporter.GetAtPath(Kit);
        ti.textureType=TextureImporterType.Sprite;ti.spriteImportMode=SpriteImportMode.Multiple;
        ti.spritePixelsPerUnit=100;ti.alphaIsTransparency=true;ti.mipmapEnabled=false;ti.isReadable=false;
        ti.textureCompression=TextureImporterCompression.Uncompressed;ti.maxTextureSize=2048;ti.filterMode=FilterMode.Bilinear;
        ti.SaveAndReimport();
        var factory=new SpriteDataProviderFactories();factory.Init();var dp=factory.GetSpriteEditorDataProviderFromObject(ti);dp.InitSpriteEditorDataProvider();
        var cap=dp.GetDataProvider<ISpriteFrameEditCapability>();if(cap==null)throw new Exception("No sprite editing capability");
        foreach(var c in new[]{EEditCapability.CreateAndDeleteSprite,EEditCapability.EditSpriteRect,EEditCapability.EditBorder,EEditCapability.EditPivot,EEditCapability.EditSpriteName})
            if(!cap.GetEditCapability().HasCapability(c))throw new Exception("Importer does not support "+c);
        var old=dp.GetSpriteRects().ToDictionary(s=>s.name);
        var rects=new[]{Rect("GlassPanel",14,62,423,228,76),Rect("GlassButton",431,105,354,172,72),
            Rect("LimeButton",784,104,350,173,72),Rect("HoverButton",1142,104,376,177,72),
            Rect("GlassCard",34,284,383,431,65),Rect("SelectedCard",431,285,384,431,65),
            Rect("SkillCircle",824,365,337,323,0),Rect("MoonOrb",1191,376,310,303,0),
            Rect("Backpack",52,716,317,285,0),Rect("Keycap",407,769,264,193,58),
            Rect("Nameplate",701,795,390,153,66),Rect("ProgressTrack",1084,795,447,153,66)};
        foreach(var rect in rects)if(old.TryGetValue(rect.name,out var previous))rect.spriteID=previous.spriteID;
        dp.SetSpriteRects(rects);var ids=dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if(ids==null)throw new Exception("No sprite file ID provider");ids.SetNameFileIdPairs(rects.Select(s=>new SpriteNameFileIdPair(s.name,s.spriteID)));
        dp.Apply();ti.SaveAndReimport();return "12 transparent production sprites imported with stable IDs and nine-slice borders";
    }
    static void Load()=>sprites=AssetDatabase.LoadAllAssetsAtPath(Kit).OfType<Sprite>().ToDictionary(s=>s.name);
    static void Edit(string path,Action<GameObject> action)
    {
        var g=PrefabUtility.LoadPrefabContents(path);try{action(g);PrefabUtility.SaveAsPrefabAsset(g,path);}finally{PrefabUtility.UnloadPrefabContents(g);}
    }
    static T Ref<T>(UnityEngine.Object o,string field) where T:UnityEngine.Object
        =>new SerializedObject(o).FindProperty(field)?.objectReferenceValue as T;
    static void Field(UnityEngine.Object o,string name,UnityEngine.Object value){var s=new SerializedObject(o);s.FindProperty(name).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    static Transform Find(Transform t,string path)=>t.Find(path)??throw new Exception(t.name+" missing "+path);
    static void Place(Transform t,Vector2 anchor,float x,float y,float w,float h,Vector2 pivot)
    {var r=(RectTransform)t;r.anchorMin=r.anchorMax=anchor;r.pivot=pivot;r.anchoredPosition=new(x,y);r.sizeDelta=new(w,h);r.localScale=Vector3.one;}
    static void Stretch(Transform t,float margin=0){var r=(RectTransform)t;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.pivot=C;r.offsetMin=Vector2.one*margin;r.offsetMax=-Vector2.one*margin;}
    static void Free(Transform t){foreach(var c in t.GetComponents<LayoutGroup>())c.enabled=false;foreach(var c in t.GetComponents<ContentSizeFitter>())c.enabled=false;}
    static Image Image(Transform parent,string name)
    {var t=parent.Find(name);if(t==null){var g=new GameObject(name,typeof(RectTransform),typeof(Image));g.transform.SetParent(parent,false);t=g.transform;}return t.GetComponent<Image>()??t.gameObject.AddComponent<Image>();}
    static TMP_Text Text(Transform parent,string name,string value,float size,bool ink=false)
    {var t=parent.Find(name);if(t==null){var g=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));g.transform.SetParent(parent,false);t=g.transform;}var text=t.GetComponent<TMP_Text>();text.text=value;text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Fonts+"LuigiTitle SDF.asset");text.fontSharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Fonts+"LuigiTitleLight.mat");Style(text,size,ink);return text;}
    static void Style(TMP_Text t,float size,bool ink=false)
    {t.fontSize=size;t.enableAutoSizing=false;t.color=ink?Ink:White;t.margin=Vector4.zero;t.alignment=TextAlignmentOptions.Center;t.textWrappingMode=TextWrappingModes.NoWrap;t.raycastTarget=false;t.enableVertexGradient=false;}
    static void Sprite(Image image,string name,float density=2.8f,bool outline=false)
    {
        if(image==null)return;var round=image.GetComponent<UIRoundImage>();if(round!=null)round.enabled=false;
        image.material=null;image.sprite=sprites[name];image.overrideSprite=null;image.type=image.sprite.border==Vector4.zero?UnityEngine.UI.Image.Type.Simple:UnityEngine.UI.Image.Type.Sliced;
        image.preserveAspect=image.type==UnityEngine.UI.Image.Type.Simple;image.pixelsPerUnitMultiplier=density;image.fillCenter=!outline;image.color=Color.white;
        image.SetMaterialDirty();image.SetVerticesDirty();textured++;
    }
    static void HideFace(Transform t){if(t==null)return;var i=t.GetComponent<Image>();if(i!=null)i.enabled=false;}
    static void Typography()
    {
        foreach(var p in AssetDatabase.FindAssets("t:Material",new[]{Fonts.TrimEnd('/')}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".mat"))){
            var m=AssetDatabase.LoadAssetAtPath<Material>(p);if(!m.HasProperty("_FaceColor"))continue;
            m.SetColor("_FaceColor",Color.white);m.SetColor("_OutlineColor",new Color(.19f,.09f,.30f,1));m.SetFloat("_OutlineWidth",.08f);
            m.SetFloat("_FaceDilate",.025f);m.EnableKeyword("UNDERLAY_ON");m.SetColor("_UnderlayColor",new Color(.14f,.035f,.25f,.85f));
            m.SetFloat("_UnderlayOffsetX",.25f);m.SetFloat("_UnderlayOffsetY",-.55f);m.SetFloat("_UnderlayDilate",.15f);m.SetFloat("_UnderlaySoftness",.18f);
            if(m.HasProperty("_Bevel")){m.EnableKeyword("BEVEL_ON");m.SetFloat("_Bevel",.16f);m.SetFloat("_BevelWidth",.5f);m.SetFloat("_BevelOffset",.12f);m.SetFloat("_Reflectivity",.18f);}
            EditorUtility.SetDirty(m);
        }
    }
    public static string Main()
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling)throw new Exception("Stop and ready first");Load();Typography();
        var paths=AssetDatabase.FindAssets("t:Prefab",new[]{Root.TrimEnd('/')}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>!p.EndsWith("/UIMessagePopup.prefab"))
            .OrderBy(p=>p.EndsWith("/UISurfacePart.prefab")?0:Path.GetFileName(p).StartsWith("UISurface")?1:p.Contains("/Parts/")?2:3).ThenBy(p=>p).ToArray();
        foreach(var path in paths)Edit(path,g=>{
            foreach(var part in g.GetComponentsInChildren<UISurfacePart>(true)){
                var i=part.Graphic as Image;if(i==null||!i.enabled||i.color.a<.01f)continue;
                var button=part.GetComponentInParent<Button>();var card=part.GetComponentInParent<UICharacterCard>();var roster=part.GetComponentInParent<UICrewRosterCard>();
                var name=button!=null?"GlassButton":card!=null||roster!=null?"GlassCard":"GlassPanel";
                if(g.name.StartsWith("UISurface")&&(g.name.Contains("Green")||g.name.Contains("Selected")))name="LimeButton";
                if(button!=null&&new[]{"Start","Ready","Apply","Create","Join","Confirm","Selected","Green"}.Any(x=>button.name.IndexOf(x,StringComparison.OrdinalIgnoreCase)>=0))name="LimeButton";
                Sprite(i,name);part.transform.SetAsFirstSibling();
                if(button!=null&&name=="LimeButton")foreach(var label in button.GetComponentsInChildren<TMP_Text>(true))label.color=Ink;
            }
            foreach(var i in g.GetComponentsInChildren<Image>(true)){
                if(new[]{"SelectionFrame","SelfFrame","Selected","SelectedFrame","Selection"}.Contains(i.name)&&i.color.a>.01f)Sprite(i,"SelectedCard",3,true);
                if(i.name=="KeyChip"||i.name=="KeyBadge")Key(i);
            }
            foreach(var slot in g.GetComponentsInChildren<UISkillSlot>(true))Skill(slot);
            foreach(var button in g.GetComponentsInChildren<Button>(true)){
                var c=button.colors;c.normalColor=Color.white;c.highlightedColor=new Color(1,1,1,1);c.pressedColor=new(.78f,.72f,.92f);c.disabledColor=new(.6f,.58f,.65f,.5f);button.colors=c;
            }
            foreach(var team in g.GetComponentsInChildren<UITeamSlot>(true)){var selection=Ref<GameObject>(team,"selection");if(selection!=null)Sprite(selection.GetComponent<Image>(),"LimeButton");var label=Ref<TMP_Text>(team,"label");if(label!=null)label.transform.SetAsLastSibling();}
            if(g.GetComponent<UIMatchHudScreen>()!=null)Night(g);
        });AssetDatabase.SaveAssets();return Newtonsoft.Json.JsonConvert.SerializeObject(new{prefabs=paths.Length,textured,atlas=Kit,screenPrefabDuplicates=0});
    }
    static void Key(Image i)
    {Free(i.transform);Sprite(i,"Keycap",3.8f);foreach(var effect in i.GetComponents<Shadow>())effect.enabled=false;foreach(var p in i.GetComponentsInChildren<UISurfacePart>(true))if(p.gameObject!=i.gameObject)HideFace(p.transform);foreach(var text in i.GetComponentsInChildren<TMP_Text>(true)){Stretch(text.transform);text.color=Ink;text.fontSharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Fonts+"LuigiBodyLight.mat");}}
    static void Skill(UISkillSlot slot)
    {
        var t=slot.transform;var rim=t.Find("ApprovedSkillRim");if(rim!=null){var i=rim.GetComponent<Image>();Sprite(i,"SkillCircle");Place(rim,C,0,17,106,106,C);rim.SetAsFirstSibling();}
        HideFace(t.Find("LuigiSurface"));HideFace(t.Find("Circle/Back"));
        var key=t.Find("KeyBadge");if(key!=null){Key(key.GetComponent<Image>());var text=Ref<TMP_Text>(slot,"key");bool wide=slot.name.Contains("Dash")||text.text=="SPACE";
            Place(key,C,0,-37,wide?92:43,34,C);Style(text,wide?18:21,true);}
        var icon=Ref<Image>(slot,"icon");if(icon!=null){Place(icon.transform,C,0,0,64,64,C);icon.color=Color.white;}
    }
    static void Night(GameObject g)
    {
        var t=g.transform;var night=g.GetComponentInChildren<UIMatchHudNight>(true);
        var top=Find(t,"NightRoot/TopCenter");Place(top,TC,0,-25,440,70,TC);
        var face=top.Find("LuigiSurface");Sprite(face.GetComponent<Image>(),"GlassButton",2.1f);Stretch(face,-6);
        HideFace(top.Find("ApprovedTimerPlate/LuigiSurface"));
        var moon=Find(top,"Moon");Sprite(moon.GetComponent<Image>(),"MoonOrb");Place(moon,C,-170,0,64,64,C);HideFace(moon.Find("Cutout"));
        Place(Find(top,"Day"),C,-89,0,86,46,C);Style(Find(top,"Day").GetComponent<TMP_Text>(),25);
        Place(Find(top,"Phase"),C,3,0,129,46,C);Style(Find(top,"Phase").GetComponent<TMP_Text>(),25);
        Place(Find(top,"Timer"),C,137,0,126,52,C);Style(Find(top,"Timer").GetComponent<TMP_Text>(),40);
        var left=Find(t,"NightRoot/TopLeft");Free(left);Place(left,TL,32,-28,315,130,TL);HideFace(left.Find("LuigiSurface"));
        var portrait=Image(left,"PlayerPortrait");Place(portrait.transform,TL,0,0,94,94,TL);portrait.preserveAspect=true;portrait.raycastTarget=false;Field(night,"playerPortrait",portrait);
        var portraitRing=Image(left,"PortraitRim");Sprite(portraitRing,"SkillCircle");Place(portraitRing.transform,TL,-8,8,110,110,TL);portraitRing.color=new Color(1,1,1,.55f);portraitRing.transform.SetAsFirstSibling();
        Place(Find(left,"RevenueCaption"),TL,113,-3,170,32,TL);var caption=Find(left,"RevenueCaption").GetComponent<TMP_Text>();Style(caption,26);caption.alignment=TextAlignmentOptions.Left;
        Place(Find(left,"Revenue"),TL,153,-37,153,43,TL);var rev=Find(left,"Revenue").GetComponent<TMP_Text>();Style(rev,35);rev.alignment=TextAlignmentOptions.Left;
        Place(Find(left,"ApprovedCoin"),TL,111,-41,32,32,TL);
        var summary=Find(left,"ApprovedBagSummary");Free(summary);Place(summary,TL,109,-83,260,36,TL);HideFace(summary.Find("LuigiSurface"));HideFace(summary.Find("Icon"));
        Place(Find(summary,"Caption"),TL,0,0,68,32,TL);var cap=Find(summary,"Caption").GetComponent<TMP_Text>();Style(cap,21);cap.text="적재량";
        Place(Find(summary,"Weight"),TL,75,0,160,32,TL);Style(Find(summary,"Weight").GetComponent<TMP_Text>(),22);
        var bag=Find(t,"NightRoot/BagIcon");Place(bag,BR,-217,44,102,102,BR);Sprite(bag.GetComponent<Image>(),"Backpack");
        Field(night,"bagClosedSprite",sprites["Backpack"]);Field(night,"bagOpenSprite",sprites["Backpack"]);
        var dock=Find(t,"NightRoot/ApprovedBagDock");Free(dock);Place(dock,BR,-30,30,190,72,BR);HideFace(dock.Find("LuigiSurface"));
        var weight=Find(dock,"BagWeight");Place(weight,BR,0,0,185,24,BR);Style(weight.GetComponent<TMP_Text>(),17);weight.GetComponent<TMP_Text>().color=new Color(.80f,.72f,.90f);
        var percent=bag.Find("BagPercent")??Find(dock,"BagPercent");percent.SetParent(dock,false);Place(percent,TR,0,0,65,30,TR);Style(percent.GetComponent<TMP_Text>(),24);
        var bar=bag.Find("BagBar")??Find(dock,"BagBar");bar.SetParent(dock,false);Place(bar,TL,0,-8,121,17,TL);bar.gameObject.SetActive(true);Sprite(bar.GetComponent<Image>(),"ProgressTrack",9);
        var fill=Find(bar,"Fill");Stretch(fill,3);((RectTransform)fill).pivot=new(0,.5f);
        var key=Image(dock,"BagKey");Key(key);Place(key.transform,TL,0,-30,38,34,TL);var keyText=Text(key.transform,"Text","B",21,true);Stretch(keyText.transform);
        var name=Text(dock,"BagLabel","가방",24);Place(name.transform,TL,43,-32,65,30,TL);
        var so=new SerializedObject(night);so.FindProperty("showNightBagCapacityDetails").boolValue=true;so.FindProperty("compactBagCapacity").boolValue=true;
        so.FindProperty("nightRevenueFormat").stringValue="{0:N0} G";so.ApplyModifiedPropertiesWithoutUndo();
        var team=Find(t,"CommonRoot/Team");team.gameObject.SetActive(true);Place(team,TR,-32,-30,126,50,TR);Style(team.GetComponent<TMP_Text>(),25);
        var teamPlate=Image(team,"TeamGlass");Sprite(teamPlate,"GlassButton",3);Stretch(teamPlate.transform,-7);teamPlate.transform.SetAsFirstSibling();
        var ping=Find(t,"CommonRoot/Ping");Place(ping,TR,-38,-87,140,27,TR);Style(ping.GetComponent<TMP_Text>(),17);
        var detail=Find(t,"CommonRoot/Details");Place(detail,TR,-36,-121,330,100,TR);detail.GetComponent<TMP_Text>().fontSize=18;
        var prompt=Find(t,"CommonRoot/Prompt");Free(prompt);Place(prompt,C,225,-225,250,78,C);Sprite(Find(prompt,"LuigiSurface").GetComponent<Image>(),"GlassButton",2.5f);
        var promptText=Find(prompt,"Text");Stretch(promptText,17);Style(promptText.GetComponent<TMP_Text>(),24);
        var cast=Find(t,"CommonRoot/CastBar");Place(cast,C,225,-251,190,15,C);Sprite(cast.GetComponent<Image>(),"ProgressTrack",9);var castFill=Find(cast,"Fill");Stretch(castFill,3);((RectTransform)castFill).pivot=new(0,.5f);
    }
    public static string Repair()
    {
        Load();foreach(var path in AssetDatabase.FindAssets("t:Prefab",new[]{Root.TrimEnd('/')}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>!p.EndsWith("/UIMessagePopup.prefab")))Edit(path,g=>{
            foreach(var i in g.GetComponentsInChildren<Image>(true))if(i.sprite!=null&&AssetDatabase.GetAssetPath(i.sprite)==Kit){var round=i.GetComponent<UIRoundImage>();if(round!=null)round.enabled=false;i.material=null;}
            foreach(var slot in g.GetComponentsInChildren<UISkillSlot>(true))Skill(slot);
            if(g.GetComponent<UIMatchHudScreen>()!=null)Night(g);
        });AssetDatabase.SaveAssets();return "Sprite material restoration, fixed-size keycaps and compact night HUD saved";
    }
    public static string Finish()
    {
        if(EditorApplication.isPlaying||EditorApplication.isCompiling)throw new Exception("Stopped ready editor required");Load();
        const string bags="Assets/Art/UI/Sprites/Common/LuigiBagStates.png";
        AssetDatabase.ImportAsset(bags,ImportAssetOptions.ForceSynchronousImport);
        var ti=(TextureImporter)AssetImporter.GetAtPath(bags);var factory=new SpriteDataProviderFactories();factory.Init();
        var dp=factory.GetSpriteEditorDataProviderFromObject(ti);dp.InitSpriteEditorDataProvider();
        var cap=dp.GetDataProvider<ISpriteFrameEditCapability>();if(cap==null)throw new Exception("Bag sprite editing unsupported");
        foreach(var c in new[]{EEditCapability.EditSpriteRect,EEditCapability.EditBorder,EEditCapability.EditPivot})if(!cap.GetEditCapability().HasCapability(c))throw new Exception("Bag importer cannot "+c);
        var rects=dp.GetSpriteRects();foreach(var r in rects){int index=r.name=="LuigiBagClosed"?0:r.name=="LuigiBagOpen"?1:r.name=="LuigiBagBuried"?2:throw new Exception("Unexpected bag sprite "+r.name);
            r.rect=new Rect(index*724,0,724,724);r.pivot=C;r.border=Vector4.zero;}
        dp.SetSpriteRects(rects);dp.Apply();ti.mipmapEnabled=false;ti.alphaIsTransparency=true;ti.textureCompression=TextureImporterCompression.Uncompressed;ti.SaveAndReimport();
        var bagSprites=AssetDatabase.LoadAllAssetsAtPath(bags).OfType<Sprite>().ToDictionary(s=>s.name);
        foreach(var path in AssetDatabase.FindAssets("t:Material",new[]{Fonts.TrimEnd('/')}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.Contains("Ink"))){
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);mat.SetColor("_FaceColor",Color.white);mat.SetFloat("_OutlineWidth",0);mat.DisableKeyword("BEVEL_ON");mat.DisableKeyword("UNDERLAY_ON");EditorUtility.SetDirty(mat);
        }
        foreach(var path in AssetDatabase.FindAssets("t:Prefab",new[]{Root.TrimEnd('/')}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>!p.EndsWith("/UIMessagePopup.prefab")))Edit(path,g=>{
            foreach(var i in g.GetComponentsInChildren<Image>(true))if(i.sprite!=null&&AssetDatabase.GetAssetPath(i.sprite)==Kit){
                var round=i.GetComponent<UIRoundImage>();if(round!=null)round.enabled=false;i.material=null;
                if(new[]{"SelectionFrame","SelfFrame","Selected","SelectedFrame"}.Contains(i.name))i.transform.SetSiblingIndex(Mathf.Min(1,i.transform.parent.childCount-1));
            }
            foreach(var button in g.GetComponentsInChildren<Button>(true)){
                var surface=button.GetComponentsInChildren<UISurfacePart>(true).Select(p=>p.Graphic).FirstOrDefault(i=>i.enabled&&i.sprite!=null&&i.sprite.name=="LimeButton");
                if(surface!=null)foreach(var text in button.GetComponentsInChildren<TMP_Text>(true))InkText(text);
            }
            foreach(var slot in g.GetComponentsInChildren<UISkillSlot>(true)){
                var label=Text(slot.transform,"SkillCaption",slot.name.Contains("Dash")?"대시":"",20);Place(label.transform,C,0,-65,130,28,C);Field(slot,"caption",label);
                var key=Ref<TMP_Text>(slot,"key");if(key!=null)InkText(key);
            }
            foreach(var team in g.GetComponentsInChildren<UITeamSlot>(true)){var text=Ref<TMP_Text>(team,"label");if(text!=null){text.fontSharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Fonts+"LuigiTitleInk.mat");text.transform.SetAsLastSibling();}}
            foreach(var card in g.GetComponentsInChildren<UICharacterCard>(true)){
                var mark=Ref<GameObject>(card,"selectedMark");var label=Ref<TMP_Text>(card,"label");if(mark==null||label==null)continue;
                var chip=Image(mark.transform,"SelectedName");Sprite(chip,"LimeButton",5);Place(chip.transform,new(.5f,0),0,1,((RectTransform)card.transform).sizeDelta.x+2,33,new(.5f,0));
                label.transform.SetAsLastSibling();label.fontSharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Fonts+"LuigiTitleInk.mat");
                var so=new SerializedObject(card);so.FindProperty("activeLabel").colorValue=Ink;so.FindProperty("idleLabel").colorValue=White;so.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach(var roster in g.GetComponentsInChildren<UICrewRosterCard>(true)){var name=Ref<TMP_Text>(roster,"playerName");if(name!=null)name.transform.SetAsLastSibling();}
            var hud=g.GetComponent<UIMatchHudScreen>();if(hud==null)return;
            var night=g.GetComponentInChildren<UIMatchHudNight>(true);Field(night,"bagClosedSprite",bagSprites["LuigiBagClosed"]);Field(night,"bagOpenSprite",bagSprites["LuigiBagOpen"]);
            var bag=Ref<RectTransform>(night,"bagIcon");bag.GetComponent<Image>().sprite=bagSprites["LuigiBagClosed"];
            var top=Find(g.transform,"NightRoot/TopLeft");var portrait=Find(top,"PlayerPortrait");var portraitImage=portrait.GetComponent<Image>();
            portraitImage.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Sprites/Common/Circle.png");portraitImage.material=null;portraitImage.color=Color.white;
            var mask=portrait.GetComponent<Mask>()??portrait.gameObject.AddComponent<Mask>();mask.showMaskGraphic=false;
            var face=Image(portrait,"Face");Place(face.transform,C,0,-30,172,172,C);face.preserveAspect=true;face.color=Color.white;Field(night,"playerPortrait",face);
            var weight=Find(top,"ApprovedBagSummary/Weight");Place(weight,TL,75,0,70,32,TL);weight.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.Left;
            var teamLabel=Find(g.transform,"CommonRoot/Team");var teamPlate=teamLabel.Find("TeamGlass")??g.transform.Find("CommonRoot/TeamGlass");
            teamPlate.SetParent(teamLabel.parent,false);Place(teamPlate,TR,-25,-23,140,64,TR);teamPlate.SetAsFirstSibling();teamLabel.gameObject.SetActive(true);teamLabel.GetComponent<TMP_Text>().color=White;
            var prompt=Find(g.transform,"CommonRoot/Prompt");Place(prompt,C,225,-225,230,90,C);
            var keyImage=Image(prompt,"InputKey");Key(keyImage);Place(keyImage.transform,new(0,.5f),20,8,44,43,new(0,.5f));
            var keyText=Text(keyImage.transform,"Glyph","F",23,true);Stretch(keyText.transform);InkText(keyText);Field(hud,"promptKey",keyImage.rectTransform);
            var promptText=Ref<TMP_Text>(hud,"promptText");Place(promptText.transform,C,25,9,152,38,C);Style(promptText,24);
            var cast=Ref<RectTransform>(hud,"castBar");Place(cast,C,225,-250,180,16,C);cast.SetAsLastSibling();
            var caption=Find(g.transform,"NightRoot/ApprovedBagDock/BagLabel").GetComponent<TMP_Text>();Place(caption.transform,TL,45,-32,65,30,TL);
            var bgKey=Find(g.transform,"NightRoot/ApprovedBagDock/BagKey/Text").GetComponent<TMP_Text>();InkText(bgKey);
            var bagWeight=Ref<TMP_Text>(night,"bagWeightText");Place(bagWeight.transform,BR,0,-19,185,23,BR);bagWeight.fontSize=16;
        });AssetDatabase.SaveAssets();return "Existing bag SpriteIDs preserved; night input key/progress, portrait crop, card selected chips, skill labels and ink contrast saved";
    }
    static void InkText(TMP_Text text)
    {
        string role=text.font!=null&&text.font.name.Contains("Number")?"Number":text.font!=null&&text.font.name.Contains("Body")?"Body":"Title";
        text.fontSharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Fonts+"Luigi"+role+"Ink.mat");text.color=Ink;text.enableVertexGradient=false;
    }
    static void Primary(Button button)
    {
        if(button==null)return;var surface=button.GetComponentsInChildren<UISurfacePart>(true).FirstOrDefault(p=>p.Graphic.enabled);
        if(surface!=null)Sprite(surface.Graphic,"LimeButton",2.8f);
        foreach(var text in button.GetComponentsInChildren<TMP_Text>(true))InkText(text);
    }
    static void Volume(Transform row)
    {
        var le=row.GetComponent<LayoutElement>();if(le!=null){le.minHeight=le.preferredHeight=84;le.flexibleHeight=0;}
        var bg=Image(row,"RowGlass");Sprite(bg,"GlassPanel",3.6f);bg.color=new Color(1,1,1,.40f);Stretch(bg.transform,-3);bg.raycastTarget=false;bg.transform.SetAsFirstSibling();
        var label=row.Find("Label");if(label!=null){Place(label,new(0,.5f),26,0,210,60,new(0,.5f));var text=label.GetComponent<TMP_Text>();text.fontSize=31;text.alignment=TextAlignmentOptions.Left;}
        var slider=row.GetComponentInChildren<Slider>(true);if(slider==null)return;Place(slider.transform,new(0,.5f),250,0,560,40,new(0,.5f));
        var track=slider.transform.Find("Background");if(track!=null){Place(track,C,0,0,560,18,C);Sprite(track.GetComponent<Image>(),"ProgressTrack",7);}
        if(slider.fillRect!=null){var area=slider.fillRect.parent as RectTransform;area.anchorMin=new(0,.5f);area.anchorMax=new(1,.5f);area.sizeDelta=new(-8,14);area.anchoredPosition=Vector2.zero;
            slider.fillRect.sizeDelta=new(0,0);slider.fillRect.GetComponent<Image>().color=Lime;}
        if(slider.handleRect!=null){var area=(RectTransform)slider.handleRect.parent;area.anchorMin=new(0,.5f);area.anchorMax=new(1,.5f);area.sizeDelta=new(-20,32);area.anchoredPosition=Vector2.zero;
            slider.handleRect.sizeDelta=new(32,0);var handle=slider.handleRect.GetComponent<Image>();var round=handle.GetComponent<UIRoundImage>();if(round!=null)round.enabled=false;
            handle.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Sprites/Common/Circle.png");handle.material=null;handle.color=White;handle.type=UnityEngine.UI.Image.Type.Simple;}
        var input=row.GetComponentInChildren<TMP_InputField>(true);if(input!=null){Place(input.transform,new(1,.5f),-20,0,118,65,new(1,.5f));var surface=input.GetComponentInChildren<UISurfacePart>(true);if(surface!=null)Sprite(surface.Graphic,"GlassButton",3.8f);}
    }
    public static string PolishScreens()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop first");Load();
        foreach(var path in AssetDatabase.FindAssets("t:Prefab",new[]{Root.TrimEnd('/')}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>!p.EndsWith("/UIMessagePopup.prefab")))Edit(path,g=>{
            foreach(var row in g.GetComponentsInChildren<UIVolumeSlider>(true))Volume(row.transform);
            var settings=g.GetComponent<UISettingsPopup>();if(settings!=null){
                var panel=Find(g.transform,"Panel");Place(panel,C,0,0,1120,800,C);var volumes=Find(panel,"Volumes");Place(volumes,TL,55,-170,1010,490,TL);
                var layout=volumes.GetComponent<VerticalLayoutGroup>();layout.spacing=14;layout.padding=new RectOffset();layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.childControlWidth=true;layout.childForceExpandWidth=true;
                var title=Find(panel,"RedSpine/Title").GetComponent<TMP_Text>();title.fontSize=70;
                var line=Image(panel,"HeadingLine");line.color=new Color(.82f,.70f,.93f,.32f);Place(line.transform,TL,55,-148,1010,2,TL);line.raycastTarget=false;
                var look=Ref<GameObject>(settings,"lookRow");Volume(look.transform);
                var full=Ref<Toggle>(settings,"fullscreen");var le=full.GetComponent<LayoutElement>();le.minHeight=le.preferredHeight=84;le.flexibleHeight=0;
                var bg=Image(full.transform,"RowGlass");Sprite(bg,"GlassPanel",3.6f);bg.color=new Color(1,1,1,.4f);Stretch(bg.transform,-3);bg.transform.SetAsFirstSibling();bg.raycastTarget=false;
                var label=Find(full.transform,"Label");Place(label,new(0,.5f),26,0,210,60,new(0,.5f));label.GetComponent<TMP_Text>().fontSize=31;label.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.Left;
                var checkBack=Find(full.transform,"Background");Place(checkBack,new(1,.5f),-25,0,60,60,new(1,.5f));Sprite(checkBack.GetComponent<Image>(),"GlassButton",4);
                if(full.graphic!=null)full.graphic.enabled=false;var tick=Text(checkBack,"Tick","✓",40);tick.enabled=true;Stretch(tick.transform);tick.color=Lime;full.graphic=tick;
                var hint=full.transform.Find("AvailabilityHint");if(hint!=null)hint.gameObject.SetActive(false);
                var apply=Ref<Button>(settings,"applyButton");Place(apply.transform,new(.5f,0),0,32,430,78,new(.5f,0));Primary(apply);
                var close=Ref<Button>(settings,"closeButton");Place(close.transform,TR,-32,-32,68,68,TR);var closeFace=close.GetComponentInChildren<UISurfacePart>(true);if(closeFace!=null)Sprite(closeFace.Graphic,"GlassButton",3.8f);
            }
            var rooms=g.GetComponent<UIRoomListScreen>();if(rooms!=null)Primary(Ref<Button>(rooms,"joinButton"));
            var room=g.GetComponent<UIRoomScreen>();if(room!=null){Primary(Ref<Button>(room,"readyButton"));Primary(Ref<Button>(room,"startButton"));}
            var titleMenu=g.GetComponent<UITitleMenuScreen>();if(titleMenu!=null)Primary(Ref<Button>(titleMenu,"lobbyButton"));
            var crew=g.GetComponent<UICharacterSelectScreen>();if(crew!=null){Primary(Ref<Button>(crew,"confirmButton"));foreach(var text in g.GetComponentsInChildren<TMP_Text>(true))if(text.text=="오늘의 크루")text.text="크루 선택";}
            var hud=g.GetComponent<UIMatchHudScreen>();if(hud!=null){
                Ref<RectTransform>(hud,"castFill").GetComponent<Image>().color=Lime;
                var common=Find(g.transform,"CommonRoot");var marker=Image(common,"TeamDot");marker.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Sprites/Common/Circle.png");marker.raycastTarget=false;Place(marker.transform,TR,-127,-47,17,17,TR);Field(hud,"teamMarker",marker);
                var label=Ref<TMP_Text>(hud,"teamText");Place(label.transform,TR,-28,-30,98,50,TR);
            }
        });AssetDatabase.SaveAssets();return "Reference settings rows, round handles, active primary actions, crew heading and team indicator saved";
    }
    public static string FinalDetails()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop first");Load();
        foreach(var path in AssetDatabase.FindAssets("t:Prefab",new[]{Root.TrimEnd('/')}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>!p.EndsWith("/UIMessagePopup.prefab")))Edit(path,g=>{
            foreach(var row in g.GetComponentsInChildren<UIStandingRow>(true)){
                var bg=Ref<Image>(row,"background");if(bg==null)continue;Sprite(bg,"GlassButton",3.2f);HideFace(row.transform.Find("LuigiSurface"));
                var so=new SerializedObject(row);so.FindProperty("preserveBackgroundArtwork").boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();
                HideFace(row.transform.Find("CardEdge"));HideFace(row.transform.Find("TopHighlight"));
            }
            foreach(var card in g.GetComponentsInChildren<UIMakingCard>(true)){
                HideFace(card.transform);var frame=Ref<Image>(card,"frame");if(frame!=null)Sprite(frame,"GlassButton",3.2f);
            }
            foreach(var input in g.GetComponentsInChildren<TMP_InputField>(true)){
                var surface=input.GetComponentInChildren<UISurfacePart>(true);if(surface!=null){surface.Graphic.enabled=true;Sprite(surface.Graphic,"GlassButton",3.8f);var own=input.GetComponent<Image>();if(own!=null&&own!=surface.Graphic)own.color=Color.clear;}
            }
            var hud=g.GetComponent<UIMatchHudScreen>();if(hud==null)return;
            var night=hud.GetComponentInChildren<UIMatchHudNight>(true);
            var caption=Find(g.transform,"NightRoot/TopLeft/RevenueCaption");((RectTransform)caption).sizeDelta=new(170,39);
            var bagLabel=Find(g.transform,"NightRoot/ApprovedBagDock/BagLabel");((RectTransform)bagLabel).sizeDelta=new(65,36);
            Sprite(Find(g.transform,"DayRoot/DayHeader/ClockBadge/Body").GetComponent<Image>(),"GlassButton",2.8f);
            var sales=Find(g.transform,"DayRoot/DayHeader/SalesBlock/Revenue").GetComponent<TMP_Text>();sales.color=White;
        });AssetDatabase.SaveAssets();return "Legacy plates beneath shared art removed; day standings and clock use same glass; night caption bounds extended";
    }
    public static string Pack()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop first");
        const string atlasPath="Assets/Art/UI/Atlases/LuigiCommon.spriteatlasv2";
        var atlas=AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>(atlasPath);
        var packed=UnityEditor.U2D.SpriteAtlasExtensions.GetPackables(atlas);
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Kit);
        if(!packed.Contains(texture)){var source=UnityEditor.U2D.SpriteAtlasAsset.Load(atlasPath);source.Add(new UnityEngine.Object[]{texture});UnityEditor.U2D.SpriteAtlasAsset.Save(source,atlasPath);AssetDatabase.ImportAsset(atlasPath,ImportAssetOptions.ForceSynchronousImport);}
        var importer=(UnityEditor.U2D.SpriteAtlasImporter)AssetImporter.GetAtPath(atlasPath);var platform=importer.GetPlatformSettings("Standalone");
        return Newtonsoft.Json.JsonConvert.SerializeObject(new{atlas=atlasPath,addedKit=true,pcFormat=platform.format.ToString(),maxSize=platform.maxTextureSize,rotation=importer.packingSettings.enableRotation,tightPacking=importer.packingSettings.enableTightPacking,mipmaps=importer.textureSettings.generateMipMaps});
    }
    public static string RestoreFontSourceMaterials()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop first");
        var baseline=Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText("../00__Docs/02__Art/02__UI/luigi-pc-ui-v2/light-ui-preview/font-source-material-baseline.json"));
        var props=new[]{"_FaceColor","_OutlineColor","_OutlineWidth","_FaceDilate","_UnderlayColor","_UnderlayOffsetX","_UnderlayOffsetY","_UnderlayDilate","_UnderlaySoftness","_Bevel","_BevelWidth","_BevelOffset","_Reflectivity"};
        foreach(var item in baseline){
            var mat=AssetDatabase.LoadAllAssetsAtPath((string)item["path"]).OfType<Material>().Single(m=>m.name==(string)item["name"]);
            var defaults=new Material(mat.shader);try{
                foreach(var prop in props){if(!mat.HasProperty(prop))continue;var value=item["values"][prop];
                    if(prop.EndsWith("Color")){var a=value as Newtonsoft.Json.Linq.JArray;mat.SetColor(prop,a!=null?new Color((float)a[0],(float)a[1],(float)a[2],(float)a[3]):defaults.GetColor(prop));}
                    else mat.SetFloat(prop,value!=null?(float)value:defaults.GetFloat(prop));
                }
                mat.shaderKeywords=item["keywords"].Values<string>().ToArray();EditorUtility.SetDirty(mat);
            }finally{UnityEngine.Object.DestroyImmediate(defaults);}
        }AssetDatabase.SaveAssets();return "7 embedded font source materials restored through Editor API; UI-specific .mat presets retained";
    }
    public static string InteractionStates()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop first");Load();int count=0;
        foreach(var path in AssetDatabase.FindAssets("t:Prefab",new[]{Root.TrimEnd('/')}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>!p.EndsWith("/UIMessagePopup.prefab")))Edit(path,g=>{
            foreach(var button in g.GetComponentsInChildren<Button>(true)){
                var face=button.GetComponentsInChildren<UISurfacePart>(true).Select(p=>p.Graphic).FirstOrDefault(i=>i.enabled&&i.sprite!=null&&AssetDatabase.GetAssetPath(i.sprite)==Kit);
                if(face==null)continue;button.targetGraphic=face;
                if(face.sprite.name=="LimeButton"){
                    button.transition=Selectable.Transition.ColorTint;var colors=button.colors;colors.normalColor=new Color(.94f,.94f,.94f);colors.highlightedColor=Color.white;colors.selectedColor=Color.white;colors.pressedColor=new Color(.70f,.66f,.77f);colors.disabledColor=new Color(.55f,.55f,.6f,.6f);button.colors=colors;
                }else{
                    button.transition=Selectable.Transition.SpriteSwap;var state=button.spriteState;state.highlightedSprite=sprites["HoverButton"];state.selectedSprite=sprites["HoverButton"];state.pressedSprite=sprites["GlassButton"];state.disabledSprite=sprites["GlassButton"];button.spriteState=state;
                }count++;
            }
            if(g.GetComponent<UIMatchHudScreen>()!=null){
                var info=Find(g.transform,"NightRoot/BagIcon/BagInfo/Text (TMP)").GetComponent<TMP_Text>();info.text="i";Style(info,17);Place(info.transform,C,0,0,26,30,C);
            }
        });AssetDatabase.SaveAssets();return "Existing Button targets now use shared art for hover/focus/press; bag info glyph fitted; targets="+count;
    }
    public static string LootFaces()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop first");Load();int count=0;
        foreach(var path in AssetDatabase.FindAssets("t:Prefab",new[]{Root.TrimEnd('/')}).Select(AssetDatabase.GUIDToAssetPath)){
            if(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<UIBoxLootSlot>(true)==null)continue;
            Edit(path,g=>{foreach(var slot in g.GetComponentsInChildren<UIBoxLootSlot>(true)){
                Sprite(Ref<Image>(slot,"frame"),"GlassCard",3.1f);
                var so=new SerializedObject(slot);so.FindProperty("readyColor").colorValue=Color.white;so.FindProperty("blindColor").colorValue=new Color(.72f,.68f,.86f,.8f);so.FindProperty("emptyColor").colorValue=new Color(.7f,.65f,.8f,.25f);so.ApplyModifiedPropertiesWithoutUndo();count++;
            }});
        }AssetDatabase.SaveAssets();return "Same glass sprite on existing loot-slot frames; state, rarity, click and flight references preserved; slots="+count;
    }
}
