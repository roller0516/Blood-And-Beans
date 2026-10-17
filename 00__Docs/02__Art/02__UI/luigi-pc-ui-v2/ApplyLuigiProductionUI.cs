using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using Object = UnityEngine.Object;

// 에디터에서만 실행하는 프리팹 제작 레시피. 런타임은 기존 Presenter와 UIView를 사용한다.
public static class ApplyLuigiProductionUI
{
    const string Base="Assets/Art/UI/Prefabs/", Fonts="Assets/Art/Fonts/";
    static readonly Color Cream=new Color(.98f,.92f,.77f), Ink=new Color(.19f,.13f,.08f), Green=new Color(.075f,.18f,.14f), Gold=new Color(.83f,.65f,.29f);
    static readonly Vector2 TL=new Vector2(0,1), TR=Vector2.one, C=new Vector2(.5f,.5f), BC=new Vector2(.5f,0), TC=new Vector2(.5f,1);
    static TMP_FontAsset title,body,number;
    static Sprite S(string name) => AssetDatabase.FindAssets(name+" t:Sprite",new[]{"Assets/Art/UI/Sprites/Common"}).Select(AssetDatabase.GUIDToAssetPath).SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<Sprite>().FirstOrDefault(x=>x.name==name) ?? AssetDatabase.LoadAllAssetsAtPath("Assets/Art/UI/Sprites/Common/"+(name=="LuigiPanel"?"LuigiPanel.png":name.StartsWith("LuigiBag")?"LuigiBagStates.png":"LuigiControls.png")).OfType<Sprite>().First(x=>x.name==name);
    public static string Main()
    {
        title=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Fonts+"LuigiTitle SDF.asset");body=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Fonts+"LuigiBody SDF.asset");number=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Fonts+"LuigiNumber SDF.asset");
        Theme();
        foreach(var path in Directory.GetFiles(Base+"Parts","*.prefab").OrderBy(x=>x))
        {
            if(Path.GetFileName(path).StartsWith("UISurfacePart")||Path.GetFileName(path)=="UICrewRosterCard.prefab")continue;
            Edit(path,Part);
        }
        Edit(Base+"Screen/UITitleMenuScreen.prefab",Title);
        Edit(Base+"Screen/UIRoomListScreen.prefab",Rooms);
        Edit(Base+"Screen/UIRoomScreen.prefab",Room);
        Edit(Base+"Screen/UICharacterSelectScreen.prefab",Crew);
        Edit(Base+"Screen/UIMatchHudScreen.prefab",Hud);
        Edit(Base+"Screen/UIDaySettlementScreen.prefab",Settlement);
        foreach(var path in Directory.GetFiles(Base+"Popup","*.prefab"))Edit(path,Popup);
        Edit("Assets/Art/Environment/Prefabs/CharacterStage.prefab",g=>{
            var stage=g.GetComponent<CharacterStage>();Value(stage,"selectedCharacterOnly",true);var so=new SerializedObject(stage);so.FindProperty("selectedViewport").rectValue=new Rect(0,.12f,.55f,.76f);so.ApplyModifiedPropertiesWithoutUndo();
            var composer=g.GetComponentInChildren<Unity.Cinemachine.CinemachinePositionComposer>(true);composer.CameraDistance=4.8f;composer.TargetOffset=new Vector3(0,1.25f,0);var composition=composer.Composition;composition.ScreenPosition=new Vector2(0,-.02f);composer.Composition=composition;
            const string stageMatPath="Assets/Art/UI/PrototypeKit/UI_StageFixture.mat";var fixture=AssetDatabase.LoadAssetAtPath<Material>(stageMatPath);if(fixture==null){fixture=new Material(Shader.Find("Universal Render Pipeline/Lit"));fixture.SetColor("_BaseColor",new Color(.46f,.42f,.30f));fixture.SetFloat("_Smoothness",.15f);AssetDatabase.CreateAsset(fixture,stageMatPath);}
            foreach(var renderer in g.GetComponentsInChildren<Renderer>(true)){var mats=renderer.sharedMaterials;bool changed=false;for(int i=0;i<mats.Length;i++)if(mats[i]==null||AssetDatabase.GetAssetPath(mats[i]).EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)){mats[i]=fixture;changed=true;}if(changed)renderer.sharedMaterials=mats;}
        },false);
        AssetDatabase.SaveAssets();
        return "Updated existing screen/popup/part prefabs in place; serialized gameplay bindings and asset GUIDs preserved.";
    }
    static void Edit(string path,Action<GameObject> action,bool common=true)
    {
        path=path.Replace('\\','/');
        var g=PrefabUtility.LoadPrefabContents(path);
        try{if(common)Common(g);action(g);PrefabUtility.SaveAsPrefabAsset(g,path);}finally{PrefabUtility.UnloadPrefabContents(g);}
    }
    static void Theme()
    {
        var theme=AssetDatabase.LoadAssetAtPath<UIThemeConfig>("Assets/Resources/UIThemeConfig.asset");var so=new SerializedObject(theme);
        so.FindProperty("fontScale").floatValue=1;so.FindProperty("panel").colorValue=Green;so.FindProperty("panelDeep").colorValue=new Color(.055f,.12f,.10f);
        so.FindProperty("cream").colorValue=Cream;so.FindProperty("gold").colorValue=Gold;so.FindProperty("paperInk").colorValue=Ink;
        so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(theme);
    }
    static void Common(GameObject g)
    {
        foreach(var round in g.GetComponentsInChildren<UIRoundImage>(true)){round.enabled=false;var im=round.GetComponent<Graphic>();if(im!=null)im.material=null;}
        foreach(var t in g.GetComponentsInChildren<TMP_Text>(true))
        {
            if(t.font!=null&&t.font.name.StartsWith("Luigi"))continue;
            var role=t.name.IndexOf("Timer",StringComparison.OrdinalIgnoreCase)>=0||t.name=="Revenue"||t.name=="Total"||t.name=="Clock"||t.name=="ExpectedPrice"?"Number":t.fontSize>=28||t.name.Contains("Title")||t.name.Contains("Heading")?"Title":"Body";
            Font(t,role,false);t.fontSize=Mathf.Clamp(t.fontSize*1.2f,18,100);t.enableAutoSizing=false;t.raycastTarget=t.GetComponentInParent<TMP_InputField>()!=null;
        }
        foreach(var scaler in g.GetComponentsInChildren<CanvasScaler>(true)){scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;}
        foreach(var button in g.GetComponentsInChildren<Button>(true))
        {
            // 상태를 배경색으로 표현하는 카드는 자체 Image를 유지한다.
            if(button.GetComponent<UICharacterCard>()!=null||button.GetComponent<UITeamSlot>()!=null)continue;
            Button(button.transform,button.name=="Confirm"||button.name=="Apply"||button.name=="btn_shutdown"?"Red":"Green");
        }
    }
    static RectTransform F(Transform root,string path){var t=root.Find(path);if(t==null)throw new Exception(root.name+" missing "+path);return (RectTransform)t;}
    static RectTransform F(GameObject root,string path)=>F(root.transform,path);
    static T Ref<T>(Component owner,string field) where T:Object {return new SerializedObject(owner).FindProperty(field).objectReferenceValue as T;}
    static void Wire(Object owner,string field,Object obj){var so=new SerializedObject(owner);var p=so.FindProperty(field);if(p==null)throw new Exception(field);p.objectReferenceValue=obj;so.ApplyModifiedPropertiesWithoutUndo();}
    static void Value(Object owner,string field,bool value){var so=new SerializedObject(owner);so.FindProperty(field).boolValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    static void Tint(Object owner,string field,Color value){var so=new SerializedObject(owner);so.FindProperty(field).colorValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    static void Place(Transform t,Vector2 anchor,float x,float y,float w,float h,Vector2? pivot=null)
    {var r=(RectTransform)t;r.anchorMin=r.anchorMax=anchor;r.pivot=pivot??C;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);r.localScale=Vector3.one;}
    static void Stretch(Transform t,float left=0,float top=0,float right=0,float bottom=0){var r=(RectTransform)t;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.pivot=C;r.offsetMin=new Vector2(left,bottom);r.offsetMax=new Vector2(-right,-top);r.localScale=Vector3.one;}
    static RectTransform New(Transform parent,string name){var existing=parent.Find(name);if(existing!=null)return (RectTransform)existing;var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return (RectTransform)go.transform;}
    static void DisableLayout(Transform t){foreach(var c in t.GetComponents<LayoutGroup>())c.enabled=false;var fitter=t.GetComponent<ContentSizeFitter>();if(fitter!=null)fitter.enabled=false;}
    static LayoutElement Element(Transform t,float width=-1,float height=-1){var e=t.GetComponent<LayoutElement>()??t.gameObject.AddComponent<LayoutElement>();if(width>=0){e.minWidth=width;e.preferredWidth=width;}if(height>=0){e.minHeight=height;e.preferredHeight=height;}e.flexibleWidth=0;e.flexibleHeight=0;return e;}
    static void Font(TMP_Text t,string role,bool ink){t.font=role=="Title"?title:role=="Number"?number:body;t.fontSharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Fonts+"Luigi"+role+(ink?"Ink":"Light")+".mat");t.color=ink?Ink:Cream;}
    static TMP_Text Text(Transform t,float size,bool ink=false,string role="Body",TextAlignmentOptions alignment=TextAlignmentOptions.MidlineLeft)
    {var label=t.GetComponent<TMP_Text>();Font(label,role,ink);label.fontSize=size;label.alignment=alignment;label.enableAutoSizing=false;return label;}
    static TMP_Text Label(Transform p,string name,string value,float size,bool ink=false,string role="Body")
    {var r=New(p,name);var t=r.GetComponent<TextMeshProUGUI>()??r.gameObject.AddComponent<TextMeshProUGUI>();t.text=value;t.raycastTarget=false;return Text(r,size,ink,role,TextAlignmentOptions.Center);}
    static void InkTexts(Transform root){foreach(var t in root.GetComponentsInChildren<TMP_Text>(true)){var button=t.GetComponentInParent<Button>();Font(t,t.font==number?"Number":t.font==title?"Title":"Body",button==null);}}
    static Image Surface(Transform parent,string variant="Paper",bool rim=false,float inset=0)
    {
        var old=parent.Find("LuigiSurface");GameObject go;
        if(old==null){go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Base+"Parts/UISurfacePart"+variant+".prefab"));go.name="LuigiSurface";go.transform.SetParent(parent,false);}else go=old.gameObject;
        Stretch(go.transform,inset,inset,inset,inset);go.transform.SetAsFirstSibling();var e=go.GetComponent<LayoutElement>();e.ignoreLayout=true;
        var image=go.GetComponent<Image>();image.sprite=S(variant=="Paper"?"LuigiPanel":"Luigi"+variant);image.color=Color.white;image.fillCenter=!rim;image.pixelsPerUnitMultiplier=rim?12:variant=="Paper"?3:4;image.raycastTarget=false;
        return image;
    }
    static void Panel(Transform t,string variant="Paper",bool ink=true)
    {var im=t.GetComponent<Image>();if(im!=null){im.color=Color.clear;im.material=null;}Surface(t,variant);if(ink)InkTexts(t);}
    static void Button(Transform t,string variant)
    {
        var button=t.GetComponent<Button>();var image=Surface(t,variant);button.targetGraphic=image;button.transition=Selectable.Transition.SpriteSwap;
        button.spriteState=new SpriteState{highlightedSprite=S("LuigiSelected"),pressedSprite=S("LuigiRed"),selectedSprite=S("LuigiSelected"),disabledSprite=S("LuigiDisabled")};
        var root=t.GetComponent<Image>();if(root!=null){root.color=Color.clear;root.material=null;}
        foreach(var label in t.GetComponentsInChildren<TMP_Text>(true))Font(label,"Title",false);
    }
    static VerticalLayoutGroup Vertical(Transform t,int pad=0,float spacing=8)
    {DisableLayout(t);foreach(var other in t.GetComponents<LayoutGroup>())if(!(other is VerticalLayoutGroup))Object.DestroyImmediate(other);var v=t.GetComponent<VerticalLayoutGroup>()??t.gameObject.AddComponent<VerticalLayoutGroup>();v.enabled=true;v.padding=new RectOffset(pad,pad,pad,pad);v.spacing=spacing;v.childControlHeight=true;v.childControlWidth=true;v.childForceExpandHeight=false;v.childForceExpandWidth=true;v.childAlignment=TextAnchor.UpperCenter;return v;}
    static void Backdrop(GameObject g,bool artwork=true)
    {var r=g.transform.Find("Backdrop");if(r==null)return;var im=r.GetComponent<Image>();if(artwork){im.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Sprites/Title/LuigiTitleBackdrop.png");im.color=new Color(.37f,.43f,.38f,1);im.type=Image.Type.Simple;}else im.color=new Color(.015f,.025f,.02f,.72f);}
    static void Part(GameObject g)
    {
        var t=g.transform;
        switch(g.name)
        {
            case "UICharacterCard":
                Surface(t,"Cream",true).transform.SetAsLastSibling();Element(t,148,178);((RectTransform)t).sizeDelta=new Vector2(148,178);t.GetComponent<Image>().color=Cream;
                Tint(g.GetComponent<UICharacterCard>(),"idleBackground",Cream);Tint(g.GetComponent<UICharacterCard>(),"idleLabel",Ink);Tint(g.GetComponent<UICharacterCard>(),"activeLabel",Cream);
                Place(F(t,"Portrait"),C,0,17,120,116);F(t,"Portrait").GetComponent<Image>().color=Color.white;
                Place(F(t,"Name"),BC,0,25,136,36);Text(F(t,"Name"),23,true,"Title",TextAlignmentOptions.Center);break;
            case "UITeamSlot":Surface(t,"Green",true).transform.SetAsLastSibling();Element(t,118,56);Stretch(F(t,"Text (TMP)"),6,6,6,6);Text(F(t,"Text (TMP)"),22,false,"Title",TextAlignmentOptions.Center);break;
            case "UIStandingRowClock":
                F(t,"CardEdge").GetComponent<Image>().color=Color.clear;Surface(t,"Green",true).transform.SetAsLastSibling();F(t,"TopHighlight").gameObject.SetActive(false);
                Place(F(t,"BadgeEdge"),new Vector2(0,.5f),24,0,34,34);Place(F(t,"Cafe"),C,-8,0,100,34);Place(F(t,"Total"),TR,-12,-9,100,32,TR);Text(F(t,"Cafe"),20);Text(F(t,"Total"),25,false,"Number",TextAlignmentOptions.Right);
                var backing=F(t,"Background").GetComponent<Image>();backing.sprite=S("LuigiGreen");backing.type=Image.Type.Sliced;backing.pixelsPerUnitMultiplier=6;backing.color=Color.white;Element(t,250,49);break;
            case "UIStandingRow":Element(t,-1,48);Surface(t,"Green");break;
            case "UISettlementCard":Panel(t,"Green",false);F(t,"Fill").GetComponent<Image>().color=Color.clear;break;
            case "UISettlementLine":Element(t,-1,68);Text(F(t,"Label"),23,true);Text(F(t,"Value"),31,true,"Number",TextAlignmentOptions.Right).fontSharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Fonts+"LuigiNumberLight.mat");break;
            case "UIPanelTab":Button(t,"Green");break;
            case "UIPlayerNameplate":var nameRim=Surface(F(t,"View"),"Green",true);nameRim.pixelsPerUnitMultiplier=20;nameRim.transform.SetAsLastSibling();break;
            case "UINamedItemIcon":
                InkTexts(t);Element(F(t,"Plus"),12,28);var stack=F(t,"Stack");var sv=Vertical(stack,0,2);sv.childForceExpandWidth=false;sv.childAlignment=TextAnchor.MiddleCenter;Element(F(stack,"UIDayItemIcon"),44,44);Element(F(stack,"Name"),66,34);Text(F(stack,"Name"),16,true,"Body",TextAlignmentOptions.Center);F(stack,"UIDayItemIcon/Back").GetComponent<Image>().color=Color.clear;var itemBorder=F(stack,"UIDayItemIcon/Border").GetComponent<Image>();itemBorder.sprite=S("LuigiCream");itemBorder.type=Image.Type.Sliced;itemBorder.fillCenter=false;itemBorder.pixelsPerUnitMultiplier=32;break;
            case "UICustomerOrder":
                var view=F(t,"View");var back=F(view,"Back");var rim=New(view,"BubbleBrassRim");Stretch(rim,-3,-3,-3,-3);rim.SetAsFirstSibling();var rimImage=rim.GetComponent<Image>()??rim.gameObject.AddComponent<Image>();rimImage.sprite=back.GetComponent<Image>().sprite;rimImage.type=back.GetComponent<Image>().type;rimImage.color=Gold;rimImage.raycastTarget=false;Element(rim).ignoreLayout=true;
                var tab=F(view,"CoinTab");Surface(tab,"Green",true).transform.SetAsLastSibling();Text(F(tab,"ExpectedPrice"),24,false,"Number",TextAlignmentOptions.Center);Element(F(tab,"ExpectedPrice"),58,34);break;
            case "UIMakingCard":Making(g);break;
            case "UIRecipeRow":
                Panel(t,"Cream");Surface(t,"Cream").pixelsPerUnitMultiplier=10;Element(t,-1,94);var h=t.GetComponent<HorizontalLayoutGroup>();h.padding=new RectOffset(12,12,6,6);h.spacing=6;h.childControlWidth=h.childControlHeight=true;h.childForceExpandWidth=false;h.childForceExpandHeight=false;h.childAlignment=TextAnchor.MiddleLeft;
                foreach(var txt in g.GetComponentsInChildren<TMP_Text>(true))Text(txt.transform,txt.name=="BasePrice"?21:18,true,txt.name=="BasePrice"?"Number":"Body",txt.alignment);
                var product=F(t,"Product");DisableLayout(product);Element(product,214,82);var ps=F(product,"Stack");DisableLayout(ps);Stretch(ps);Place(F(ps,"UIDayItemIcon"),new Vector2(0,.5f),31,0,54,54);Place(F(ps,"Name"),TL,68,-8,141,40,TL);Text(F(ps,"Name"),18,true,"Body");Place(F(ps,"BasePrice"),new Vector2(0,0),68,5,135,27,new Vector2(0,0));Text(F(ps,"BasePrice"),22,true,"Number");F(ps,"UIDayItemIcon/Back").GetComponent<Image>().color=Color.clear;Element(F(t,"Equals"),16,28);var cells=F(t,"Cells");var ce=Element(cells);ce.flexibleWidth=1;var cellLayout=cells.GetComponent<HorizontalLayoutGroup>();cellLayout.spacing=4;cellLayout.childForceExpandWidth=false;cellLayout.childControlWidth=cellLayout.childControlHeight=true;cellLayout.childAlignment=TextAnchor.MiddleLeft;
                break;
            case "UIBoxLootSlot":
                t.GetComponent<Image>().sprite=S("LuigiCream");t.GetComponent<Image>().type=Image.Type.Sliced;t.GetComponent<Image>().pixelsPerUnitMultiplier=8;t.GetComponent<Image>().color=Color.white;
                Tint(g.GetComponent<UIBoxLootSlot>(),"readyColor",Color.white); // optional field checked by authoring validation
                break;
            case "UICompletionGauge":
                Panel(t,"Green",false);((RectTransform)t).sizeDelta=new Vector2(360,100);Text(F(t,"CompletionGauge(Clone)/Label"),23,false,"Title",TextAlignmentOptions.Center);break;
            case "UISkillSlot":
                Element(t,80,106);((RectTransform)t).sizeDelta=new Vector2(80,106);Surface(F(t,"KeyBadge"),"Green").pixelsPerUnitMultiplier=10;Text(F(t,"KeyBadge/Key"),18,false,"Number",TextAlignmentOptions.Center);break;
            case "UIVolumeSlider":
                foreach(var txt in g.GetComponentsInChildren<TMP_Text>(true))Text(txt.transform,25,true,txt.name=="Label"?"Body":"Number",txt.alignment);
                Panel(F(t,"Input"),"Cream");break;
        }
    }
    static void Making(GameObject g)
    {
        var t=g.transform;DisableLayout(t);((RectTransform)t).sizeDelta=new Vector2(290,310);var frame=t.GetComponent<Image>();frame.sprite=S("LuigiPanel");frame.type=Image.Type.Sliced;frame.fillCenter=false;frame.pixelsPerUnitMultiplier=4;
        var b=F(t,"Body");DisableLayout(b);Stretch(b,5,5,5,5);Panel(b);
        var header=F(b,"Header");DisableLayout(header);Stretch(header,18,24,18,66);
        var texts=F(header,"Texts");DisableLayout(texts);Stretch(texts);var row=F(texts,"NameRow");DisableLayout(row);Place(row,TC,0,-16,240,44);
        Stretch(F(row,"Name"),0,0,34,0);Text(F(row,"Name"),26,true,"Title",TextAlignmentOptions.Center);Place(F(row,"Check"),TR,-4,-5,30,30,TR);Text(F(row,"Check"),25,true,"Title",TextAlignmentOptions.Center);
        Place(F(texts,"Status"),BC,0,4,240,30);Text(F(texts,"Status"),19,true,"Body",TextAlignmentOptions.Center);
        var badge=F(header,"MenuBadge");Place(badge,C,0,-3,145,136);badge.GetComponent<Image>().color=Color.clear;Stretch(F(badge,"MenuIcon"),0,0,0,0);F(badge,"MenuIcon").GetComponent<Image>().preserveAspect=true;
        var parts=F(b,"Parts");Place(parts,BC,0,35,244,54);parts.GetComponent<Image>().color=new Color(.2f,.14f,.08f,.12f);var h=parts.GetComponent<HorizontalLayoutGroup>();h.childAlignment=TextAnchor.MiddleCenter;h.childForceExpandWidth=false;h.childControlWidth=true;h.spacing=4;Element(F(parts,"PartIcon"),38,38);Element(F(parts,"Plus"),14,24);
        // 실제 보드와 같은 클립 부품을 재사용한다. 회전/시차가 없는 HUD에는 투명 스프라이트가 적합하다.
        var clip=New(t,"ClipboardClip");Place(clip,TC,0,-8,74,38);var im=clip.GetComponent<Image>()??clip.gameObject.AddComponent<Image>();im.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/PrototypeKit/UI_Clip.png");im.preserveAspect=true;im.raycastTarget=false;
    }
    static void Title(GameObject g)
    {
        var art=F(g,"ArtFrame");var fit=art.GetComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;
        var bg=F(art,"ConceptBackground").GetComponent<Image>();bg.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Sprites/Title/LuigiTitleBackdrop.png");bg.color=Color.white;
        var layout=F(art,"layout");Place(layout,new Vector2(.23f,.34f),0,0,430,295);Vertical(layout,0,18);
        foreach(Transform child in layout){Element(child,430,86);foreach(var label in child.GetComponentsInChildren<TMP_Text>())Text(label.transform,34,false,"Title",TextAlignmentOptions.Center);}
        var logo=Label(art,"LuigiGameLogo","BLOOD\n<size=62>&</size> BEANS",88,false,"Title");Place(logo.transform,new Vector2(.24f,.72f),0,0,700,290);logo.lineSpacing=-18;
        var strap=Label(art,"LuigiTagline","커피는 함께, 밤은 대담하게.",25);Place(strap.transform,new Vector2(.235f,.53f),0,0,560,48);
    }
    static void Rooms(GameObject g)
    {
        Backdrop(g);var p=F(g,"Panel");DisableLayout(p);Place(p,C,0,0,1640,900);Panel(p);
        var labels=p.Cast<Transform>().Where(x=>x.GetComponent<TMP_Text>()!=null).ToArray();Place(labels[0],TL,65,-58,900,78,TL);Text(labels[0],48,true,"Title");Place(labels[1],TL,68,-146,940,48,TL);Text(labels[1],24,true);
        var rows=Ref<RectTransform>(g.GetComponent<UIRoomListScreen>(),"rows");Place(rows,TL,65,-218,970,540,TL);Panel(rows,"Cream");var v=Vertical(rows,12,10);var template=F(rows,"RoomRowTemplate");Element(template,-1,78);Text(F(template,"Label"),27,false,"Title");
        // Viewport와 Content를 분리해 방이 늘어나도 클리핑과 스크롤을 유지한다.
        var scroll=New(p,"RoomsScroll");Place(scroll,TL,65,-218,970,540,TL);var viewport=New(scroll,"Viewport");Stretch(viewport);if(viewport.GetComponent<RectMask2D>()==null)viewport.gameObject.AddComponent<RectMask2D>();
        rows.SetParent(viewport,false);rows.anchorMin=new Vector2(0,1);rows.anchorMax=Vector2.one;rows.pivot=TC;rows.anchoredPosition=Vector2.zero;rows.sizeDelta=Vector2.zero;var fitter=rows.GetComponent<ContentSizeFitter>()??rows.gameObject.AddComponent<ContentSizeFitter>();fitter.enabled=true;fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        Surface(scroll,"Cream");F(rows,"LuigiSurface").gameObject.SetActive(false);var sr=scroll.GetComponent<ScrollRect>()??scroll.gameObject.AddComponent<ScrollRect>();sr.viewport=viewport;sr.content=rows;sr.horizontal=false;sr.vertical=true;sr.movementType=ScrollRect.MovementType.Clamped;
        var details=New(p,"RoomDetails");Place(details,TR,-58,-212,500,550,TR);Panel(details,"Green",false);var heading=Label(details,"Heading","함께할 크루",37,false,"Title");Place(heading.transform,TC,0,-70,420,64);
        var note=Label(details,"Help","방을 선택하거나\n새 카페를 열어 주세요.",26);Place(note.transform,C,0,38,400,120);
        var teams=Ref<TMP_Text>(g.GetComponent<UIRoomListScreen>(),"teamCountLabel").transform.parent;teams.SetParent(details,false);Place(teams,BC,0,92,380,72);foreach(var txt in teams.GetComponentsInChildren<TMP_Text>())Text(txt.transform,27,false,"Title",TextAlignmentOptions.Center);
        var actions=F(p,"Actions");Place(actions,BC,0,64,1490,76);var h=actions.GetComponent<HorizontalLayoutGroup>();h.spacing=18;h.childForceExpandWidth=true;h.childControlHeight=true;foreach(Transform b in actions)Element(b,250,76);
    }
    static void Room(GameObject g)
    {
        Backdrop(g);var p=F(g,"Panel");Place(p,C,0,0,1420,900);Panel(p);var v=Vertical(p,60,20);
        var labels=p.Cast<Transform>().Where(x=>x.GetComponent<TMP_Text>()!=null).ToArray();foreach(var l in labels){Element(l,-1,55);Text(l,30,true,"Title");}
        Element(F(p,"Teams"),-1,74);Element(F(p,"Members"),-1,430);Panel(F(p,"Members"),"Cream");Element(F(p,"ReadyCount"),-1,45);Element(F(p,"Actions"),-1,80);
        foreach(var b in F(p,"Actions").GetComponentsInChildren<Button>())Element(b.transform,280,76);
    }
    static void Crew(GameObject g)
    {
        var t=F(g,"Stage");Stretch(t);F(t,"CardTray").gameObject.SetActive(false);F(t,"HeadingBar").gameObject.SetActive(false);
        foreach(var strip in new[]{"CrewBackdropRight","CrewBackdropTop","CrewBackdropBottom"}){var rect=New(t,strip);var graphic=rect.GetComponent<Image>()??rect.gameObject.AddComponent<Image>();graphic.color=new Color(.035f,.065f,.055f);graphic.raycastTarget=false;rect.SetAsFirstSibling();if(strip.EndsWith("Right")){rect.anchorMin=new Vector2(.55f,0);rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}else if(strip.EndsWith("Top")){rect.anchorMin=new Vector2(0,.88f);rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}else{rect.anchorMin=Vector2.zero;rect.anchorMax=new Vector2(1,.19f);rect.offsetMin=rect.offsetMax=Vector2.zero;}}
        Place(F(t,"Caption"),TL,70,-56,820,88,TL);Text(F(t,"Caption"),54,false,"Title");
        Place(F(t,"Brand"),TL,74,-28,700,26,TL);Text(F(t,"Brand"),18);Place(F(t,"GameObject"),TR,-66,-30,700,65,TR);Panel(F(t,"GameObject"),"Green",false);
        var grid=New(t,"CrewRoster");Place(grid,TR,-74,-126,700,326,TR);var layout=grid.GetComponent<GridLayoutGroup>()??grid.gameObject.AddComponent<GridLayoutGroup>();layout.cellSize=new Vector2(163,155);layout.spacing=new Vector2(14,14);layout.constraint=GridLayoutGroup.Constraint.FixedColumnCount;layout.constraintCount=4;
        var screen=g.GetComponent<UICharacterSelectScreen>();var so=new SerializedObject(screen);var array=so.FindProperty("rosterCards");array.arraySize=8;
        for(int i=0;i<8;i++){var r=grid.Find("Crew"+i);GameObject card=r!=null?r.gameObject:(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Base+"Parts/UICrewRosterCard.prefab"));card.name="Crew"+i;card.transform.SetParent(grid,false);array.GetArrayElementAtIndex(i).objectReferenceValue=card.GetComponent<UICrewRosterCard>();}
        so.ApplyModifiedPropertiesWithoutUndo();
        var wrap=F(t,"PanelWrap");DisableLayout(wrap);Place(wrap,TR,-74,-474,700,330,TR);var detail=F(wrap,"Detail");Stretch(detail);Panel(detail);InkTexts(detail);
        Place(F(detail,"SelectedCaption"),TL,30,-21,500,26,TL);Text(F(detail,"SelectedCaption"),17,true);
        Place(F(detail,"SelectedName"),TL,30,-49,630,54,TL);Text(F(detail,"SelectedName"),38,true,"Title");
        Place(F(detail,"DayGroup"),TL,32,-111,310,112,TL);Place(F(detail,"NightGroup"),TR,-30,-111,310,112,TR);
        foreach(var group in new[]{F(detail,"DayGroup"),F(detail,"NightGroup")}){var v=Vertical(group,0,3);foreach(Transform child in group){Element(child,-1,child.name.EndsWith("Effect")?60:25);Text(child,child.name.EndsWith("Effect")?20:22,true,child.name.EndsWith("Name")?"Title":"Body");}}
        Place(F(detail,"TeamCaption"),new Vector2(0,0),32,74,140,36,new Vector2(0,0));Text(F(detail,"TeamCaption"),20,true);Place(F(detail,"Nameplate"),BC,0,42,628,52);foreach(Transform slot in F(detail,"Nameplate"))Element(slot,140,52);
        Place(F(wrap,"PanelTab"),new Vector2(0,.5f),-19,0,38,90);F(wrap,"PanelTab").gameObject.SetActive(true);
        var cards=F(t,"Cards");Place(cards,BC,-80,118,820,178);var cg=cards.GetComponent<GridLayoutGroup>();cg.cellSize=new Vector2(148,178);cg.spacing=new Vector2(16,0);cg.constraint=GridLayoutGroup.Constraint.FixedRowCount;cg.constraintCount=1;
        Place(F(t,"Confirm"),new Vector2(1,0),-72,75,346,84,new Vector2(1,0));Text(F(t,"Confirm/Label"),33,false,"Title",TextAlignmentOptions.Center);
        Place(F(t,"Back"),new Vector2(0,0),64,59,190,64,new Vector2(0,0));var backLayout=F(t,"Back").GetComponent<HorizontalLayoutGroup>();backLayout.childControlWidth=backLayout.childControlHeight=true;backLayout.childForceExpandWidth=backLayout.childForceExpandHeight=false;backLayout.spacing=10;backLayout.padding=new RectOffset(12,12,10,10);Element(F(t,"Back/EscChip"),54,38);Element(F(t,"Back/BackLabel"),92,42);Text(F(t,"Back/BackLabel"),25,false,"Title",TextAlignmentOptions.Center);Place(F(t,"PanelHint"),new Vector2(1,0),-74,28,340,32,new Vector2(1,0));
        F(t,"StageHeading").gameObject.SetActive(false);F(t,"StageSubheading").gameObject.SetActive(false);
    }
    static void Hud(GameObject g)
    {
        var t=g.transform;var h=F(t,"DayHeader");Stretch(h);h.GetComponent<Image>().color=Color.clear;F(h,"RailHighlight").gameObject.SetActive(false);F(h,"RailFill").gameObject.SetActive(false);
        var standings=F(h,"TeamStandings");Place(standings,TL,28,-28,260,222,TL);Vertical(standings,0,7);foreach(Transform row in standings){Element(row,260,49);Surface(row,"Green",true).transform.SetAsLastSibling();var bg=F(row,"Background").GetComponent<Image>();bg.sprite=S("LuigiGreen");bg.type=Image.Type.Sliced;bg.pixelsPerUnitMultiplier=6;Place(F(row,"BadgeEdge"),new Vector2(0,.5f),26,0,32,32);Place(F(row,"BadgeEdge/Badge"),C,0,0,28,28);Text(F(row,"BadgeEdge/Badge/Rank"),20,false,"Number",TextAlignmentOptions.Center);Place(F(row,"Cafe"),new Vector2(0,.5f),52,0,90,32,new Vector2(0,.5f));Text(F(row,"Cafe"),21,false,"Title");Place(F(row,"Total"),new Vector2(1,.5f),-13,0,105,34,new Vector2(1,.5f));Text(F(row,"Total"),24,false,"Number",TextAlignmentOptions.Right);}
        var clock=F(h,"ClockBadge");Place(clock,TC,0,-28,240,130,TC);Panel(clock,"Green",false);F(clock,"Body").GetComponent<Image>().enabled=false;
        Place(F(clock,"Clock"),C,0,14,220,68);Text(F(clock,"Clock"),57,false,"Number",TextAlignmentOptions.Center);
        Place(F(clock,"DayLabel"),BC,-40,25,54,25);Text(F(clock,"DayLabel"),19,false,"Title",TextAlignmentOptions.Center);Place(F(clock,"DayCounter"),BC,34,25,86,27);Text(F(clock,"DayCounter"),24,false,"Number",TextAlignmentOptions.Center);
        Place(F(clock,"TimeGauge"),BC,0,7,188,7);F(clock,"TimeGauge").GetComponent<Image>().color=new Color(.63f,.83f,.3f);
        var sales=F(h,"SalesBlock");Place(sales,TR,-28,-28,310,133,TR);Panel(sales,"Green",false);
        Place(F(sales,"Coin"),TL,20,-26,40,40,TL);Place(F(sales,"Label"),TL,68,-20,180,22,TL);Text(F(sales,"Label"),18);Place(F(sales,"Revenue"),TR,-24,-39,210,43,TR);Text(F(sales,"Revenue"),37,false,"Number",TextAlignmentOptions.Right);
        Place(F(sales,"GoalLabel"),new Vector2(0,0),24,22,100,24,new Vector2(0,0));Text(F(sales,"GoalLabel"),18);Place(F(sales,"Goal"),new Vector2(1,0),-26,22,100,25,new Vector2(1,0));Text(F(sales,"Goal"),23,false,"Number",TextAlignmentOptions.Right);F(sales,"Divider").gameObject.SetActive(false);Place(F(sales,"RentMet"),BC,0,-18,150,30);Panel(F(sales,"RentMet"),"Selected",false);
        var top=F(t,"TopLeft");Place(top,TL,28,-28,290,72,TL);Panel(top,"Green",false);Stretch(F(top,"Revenue"),20,17,20,17);Text(F(top,"Revenue"),26,false,"Number");
        var center=F(t,"TopCenter");Place(center,TC,0,-28,360,92,TC);Panel(center,"Green",false);Place(F(center,"Day"),TL,18,-14,82,24,TL);Text(F(center,"Day"),21);Place(F(center,"Phase"),TR,-18,-14,210,24,TR);Text(F(center,"Phase"),21,false,"Title",TextAlignmentOptions.Right);Place(F(center,"Timer"),BC,0,24,320,42);Text(F(center,"Timer"),34,false,"Number",TextAlignmentOptions.Center);
        var bag=F(t,"BagIcon");Place(bag,new Vector2(1,0),-44,42,64,64,new Vector2(1,0));bag.GetComponent<Image>().sprite=S("LuigiBagClosed");bag.GetComponent<Image>().preserveAspect=true;
        var hud=g.GetComponent<UIMatchHudScreen>();Wire(hud,"bagClosedSprite",S("LuigiBagClosed"));Wire(hud,"bagOpenSprite",S("LuigiBagOpen"));
        Place(F(t,"Team"),TR,-28,-28,280,30,TR);Text(F(t,"Team"),25,false,"Title",TextAlignmentOptions.Right);Place(F(t,"Ping"),TR,-28,-68,280,25,TR);Text(F(t,"Ping"),18,false,"Body",TextAlignmentOptions.Right);Place(F(t,"Details"),TR,-28,-108,300,80,TR);
        foreach(var n in new[]{"BagPercent","BagBar","BagWeight"}){var r=(RectTransform)(top.Find(n)??bag.Find(n));r.SetParent(bag,false);if(n=="BagWeight"){Place(r,new Vector2(0,0),-12,3,140,29,new Vector2(1,0));Text(r,22,false,"Number",TextAlignmentOptions.Right);}else if(n=="BagBar")Place(r,new Vector2(0,0),-12,0,135,5,new Vector2(1,0));else {Place(r,new Vector2(0,1),-12,-2,140,24,new Vector2(1,1));Text(r,18,false,"Number",TextAlignmentOptions.Right);}}
        Place(F(t,"BottomLeft"),new Vector2(0,0),30,28,185,106,new Vector2(0,0));foreach(var slot in F(t,"BottomLeft").GetComponentsInChildren<UISkillSlot>(true))Element(slot.transform,80,106);
        Place(F(t,"RecipeTab"),new Vector2(1,0),-28,28,180,64,new Vector2(1,0));Text(F(t,"RecipeTab/Glyph"),25,false,"Number",TextAlignmentOptions.Center);Text(F(t,"RecipeTab/Caption"),23,false,"Title",TextAlignmentOptions.Center);
        var recipe=F(t,"RecipeWrap/RecipePanel");Place(recipe,C,0,-20,1280,920);Panel(recipe);Place(F(recipe,"Title"),TC,0,-48,1110,62);Text(F(recipe,"Title"),45,true,"Title",TextAlignmentOptions.Center);
        var scroll=F(recipe,"ScrollView");Stretch(scroll,46,115,46,44);foreach(var label in scroll.GetComponentsInChildren<TMP_Text>(true))Font(label,"Title",true);
        foreach(var column in new[]{F(scroll,"Viewport/RecipeColumns/CoffeeColumn"),F(scroll,"Viewport/RecipeColumns/DessertColumn")}){var col=column.GetComponent<VerticalLayoutGroup>();col.spacing=8;col.childForceExpandHeight=false;Element(F(column,"Category"),-1,36);}
        var prompt=F(t,"Prompt");Panel(prompt,"Green",false);Text(F(prompt,"Text"),24,false,"Body",TextAlignmentOptions.Center);
        var dock=New(t,"MakingCardDock");Place(dock,new Vector2(1,0),-50,184,290,310,new Vector2(1,0));Wire(hud,"makingCardDock",dock);
        Place(F(t,"UICompletionGauge"),C,0,-200,360,100);
    }
    static void Settlement(GameObject g)
    {
        Backdrop(g);var t=F(g,"Stage");Stretch(t);var cards=F(t,"Cards");DisableLayout(cards);Stretch(cards);
        var summary=F(cards,"SummaryCard");Place(summary,C,-220,0,510,700);Panel(summary);F(summary,"Fill").GetComponent<Image>().color=Color.clear;var v=Vertical(summary,48,24);Element(F(summary,"Title"),-1,86);Text(F(summary,"Title"),40,true,"Title",TextAlignmentOptions.Center);
        var standings=F(cards,"StandingsCard");Place(standings,C,475,150,750,360);Panel(standings,"Green",false);F(standings,"Fill").GetComponent<Image>().color=Color.clear;Vertical(standings,32,12);Element(F(standings,"Title"),-1,54);foreach(var row in standings.GetComponentsInChildren<UIStandingRow>(true))Element(row.transform,-1,48);
        var forecast=F(cards,"ForecastCard");Place(forecast,C,475,-185,750,270);Panel(forecast,"Green",false);F(forecast,"Fill").GetComponent<Image>().color=Color.clear;Vertical(forecast,30,10);
        var gems=F(cards,"UpgradeCard");Place(gems,BC,0,38,1710,158,BC);Panel(gems,"Green",false);F(gems,"Fill").GetComponent<Image>().color=Color.clear;DisableLayout(gems);Place(F(gems,"Title"),TL,30,-19,350,35,TL);Text(F(gems,"Title"),25,false,"Title");F(gems,"Note").gameObject.SetActive(false);var grid=F(gems,"Gems");Stretch(grid,30,60,30,15);var gl=grid.GetComponent<GridLayoutGroup>();gl.cellSize=new Vector2(265,78);gl.spacing=new Vector2(8,0);gl.constraint=GridLayoutGroup.Constraint.FixedRowCount;gl.constraintCount=1;
        Place(F(t,"DayHeading"),TL,80,-48,920,88,TL);Text(F(t,"DayHeading"),53,false,"Title");Place(F(t,"Caption"),TL,84,-20,800,26,TL);Text(F(t,"Caption"),18);Place(F(t,"Countdown"),TR,-85,-62,570,45,TR);Text(F(t,"Countdown"),27,false,"Title",TextAlignmentOptions.Right);
        Place(F(t,"CountdownTrack"),TR,-85,-117,500,6,TR);Place(F(t,"Bar"),TR,-85,-117,500,6,TR);F(t,"Rule").gameObject.SetActive(false);
    }
    static void Popup(GameObject g)
    {
        var t=g.transform;
        switch(g.name)
        {
            case "UISettingsPopup":Settings(g);break;
            case "UILoadingPopup":var p=F(t,"Panel");Place(p,BC,0,70,650,120,BC);Panel(p,"Green",false);F(p,"TopBand").gameObject.SetActive(false);Stretch(F(p,"Status"),30,22,30,22);Text(F(p,"Status"),31,false,"Title",TextAlignmentOptions.Center);break;
            case "UIPhaseCuePopup":F(t,"Band").GetComponent<Image>().color=Color.clear;Place(F(t,"Label"),C,0,0,1700,270);Text(F(t,"Label"),150,false,"Title",TextAlignmentOptions.Center);break;
            case "UIReturnResultPopup":var r=F(t,"Panel");Place(r,TC,0,-100,650,180,TC);Panel(r);Place(F(r,"Title"),TC,0,-35,580,48);Text(F(r,"Title"),36,true,"Title",TextAlignmentOptions.Center).fontSharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Fonts+"LuigiTitleLight.mat");Place(F(r,"Line"),C,0,-4,580,38);Text(F(r,"Line"),25,true,"Title",TextAlignmentOptions.Center).fontSharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Fonts+"LuigiTitleLight.mat");Place(F(r,"Detail"),BC,0,42,580,28);Text(F(r,"Detail"),20,true,"Body",TextAlignmentOptions.Center);break;
            case "UIBoxLootPopup":Loot(g);break;
            case "UIMatchResultPopup":Final(g);break;
        }
    }
    static void Settings(GameObject g)
    {
        var p=F(g,"Panel");Place(p,C,0,0,960,710);Panel(p);foreach(var n in new[]{"PanelTexture","TopBand","FrameRight","FrameLeft","FrameTop","FrameBottom","BoltTL","BoltTR","BoltBL","BoltBR"})F(p,n).gameObject.SetActive(false);
        var spine=New(p,"RedSpine");Place(spine,new Vector2(0,.5f),12,0,150,670,new Vector2(0,.5f));Panel(spine,"Red",false);spine.SetSiblingIndex(1);
        var titleR=(RectTransform)(p.Find("Title")??p.Find("RedSpine/Title"));titleR.SetParent(spine,false);Stretch(titleR,20,30,20,30);Text(titleR,49,false,"Title",TextAlignmentOptions.Center).text="설\n정";
        var volume=F(p,"Volumes");Stretch(volume,205,92,70,140);var v=Vertical(volume,0,22);foreach(Transform row in volume){Element(row,-1,row.name=="Fullscreen"?44:62);foreach(var txt in row.GetComponentsInChildren<TMP_Text>(true))Text(txt.transform,25,true,txt.name=="Label"?"Body":"Number",txt.alignment);var slider=row.GetComponentInChildren<Slider>();if(slider!=null){var handle=slider.handleRect;handle.sizeDelta=new Vector2(32,8);var hi=handle.GetComponent<Image>();hi.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Sprites/Common/Circle.png");hi.color=Gold;hi.preserveAspect=true;var bg=slider.transform.Find("Background").GetComponent<Image>();bg.sprite=S("LuigiGreen");bg.type=Image.Type.Sliced;bg.pixelsPerUnitMultiplier=12;bg.color=Color.white;}}
        Place(F(p,"Apply"),new Vector2(1,0),-60,44,270,76,new Vector2(1,0));Place(F(p,"Close"),TR,-25,-24,48,48,TR);
    }
    static void Loot(GameObject g)
    {
        var p=F(g,"Stage/Panel");Place(p,C,0,0,1000,262);DisableLayout(p);Panel(p,"Green",false);F(p,"SlotStrip").GetComponent<Image>().color=Color.clear;
        var slots=p.GetComponentsInChildren<UIBoxLootSlot>(true);for(int i=0;i<slots.Length;i++){var s=slots[i].transform;Place(s,C,(i-2)*174,0,150,160);var im=s.GetComponent<Image>();im.sprite=S("LuigiCream");im.type=Image.Type.Sliced;im.pixelsPerUnitMultiplier=6;Place(F(s,"Icon"),C,0,8,112,112);Place(F(s,"Blind"),C,0,8,112,112);Text(F(s,"Blind/Mark"),52,false,"Title",TextAlignmentOptions.Center);Place(F(s,"TierBadge"),BC,0,12,132,26);Text(F(s,"TierBadge"),19,true,"Title",TextAlignmentOptions.Center);}
        var plate=New(p,"LootHeadingPlate");Place(plate,TC,0,27,450,70);Surface(plate,"Header");var head=Label(p,"LootHeading","발견한 물품",32,false,"Title");head.transform.SetParent(plate,false);Stretch(head.transform,25,10,25,10);
    }
    static void Final(GameObject g)
    {
        Backdrop(g);var t=F(g,"Stage");Stretch(t);F(t,"DailyRevenue").gameObject.SetActive(false);F(t,"Rule").gameObject.SetActive(false);
        Place(F(t,"Caption"),TL,85,-75,740,42,TL);Text(F(t,"Caption"),25);Place(F(t,"Title"),new Vector2(.25f,.64f),0,0,790,240);Text(F(t,"Title"),77,false,"Title",TextAlignmentOptions.Center);Place(F(t,"Total"),new Vector2(.25f,.38f),0,0,750,130);Text(F(t,"Total"),57,false,"Number",TextAlignmentOptions.Center);
        var ranking=F(t,"FinalRanking");Place(ranking,TR,-65,-58,870,820,TR);Panel(ranking);Place(F(ranking,"Caption"),TC,0,-58,730,75);Text(F(ranking,"Caption"),44,true,"Title",TextAlignmentOptions.Center);F(ranking,"Rule").gameObject.SetActive(false);
        for(int i=0;i<4;i++){var row=F(ranking,"Rank"+i);Place(row,TC,0,-220-i*158,730,135);Panel(row,i==0?"Selected":"Cream",i!=0);F(row,"Portrait").gameObject.SetActive(false);F(row,"Rule").gameObject.SetActive(false);Place(F(row,"Rank"),new Vector2(0,.5f),48,8,70,52);Place(F(row,"Cafe"),new Vector2(0,.5f),110,15,280,45,new Vector2(0,.5f));Place(F(row,"Revenue"),new Vector2(1,.5f),-30,10,245,56,new Vector2(1,.5f));Place(F(row,"Bar"),BC,0,25,520,7);foreach(var txt in row.GetComponentsInChildren<TMP_Text>(true))Text(txt.transform,txt.name=="Revenue"?38:30,i!=0,txt.name=="Revenue"||txt.name=="Rank"?"Number":"Title",txt.name=="Revenue"?TextAlignmentOptions.Right:TextAlignmentOptions.MidlineLeft);}
        Place(F(t,"Lobby"),new Vector2(1,0),-65,54,390,80,new Vector2(1,0));F(t,"Rematch").gameObject.SetActive(false);
        Tint(g.GetComponent<UIMatchResultPopup>(),"win",Gold);Tint(g.GetComponent<UIMatchResultPopup>(),"lose",Cream);Tint(g.GetComponent<UIMatchResultPopup>(),"muted",Ink);
    }
}
