using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// 방 시안의 배치·글자·팀 파츠를 기존 프리팹에 저장하는 재현 도구.
public static class RefineLuigiRoom
{
    const string Parts="Assets/Art/UI/Prefabs/Parts/", Fonts="Assets/Art/Fonts/";
    const string Screen="Assets/Art/UI/Prefabs/Screen/UIRoomScreen.prefab";
    const string Mats="Assets/Art/Environment/Materials/";
    static readonly Vector2 TL=new(0,1), C=new(.5f,.5f), TC=new(.5f,1), BC=new(.5f,0), BR=new(1,0), TR=Vector2.one;
    static readonly Color White=Hex("FFF9FF"), Ink=Hex("1B1730"), Plum=Hex("40275D",.975f), Lime=Hex("ADFA77");
    static TMP_FontAsset font;
    static Material light, ink, glass, selected, plain, lime;
    static Color Hex(string h,float a=1){ColorUtility.TryParseHtmlString("#"+h,out var c);c.a=a;return c;}
    static T Ref<T>(UnityEngine.Object o,string field) where T:UnityEngine.Object => new SerializedObject(o).FindProperty(field).objectReferenceValue as T;
    static void Field(UnityEngine.Object o,string field,UnityEngine.Object value){var s=new SerializedObject(o);s.FindProperty(field).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    static void Edit(string path,Action<GameObject> edit){var g=PrefabUtility.LoadPrefabContents(path);try{edit(g);PrefabUtility.SaveAsPrefabAsset(g,path);}finally{PrefabUtility.UnloadPrefabContents(g);}}
    static void Place(Transform t,Vector2 anchor,float x,float y,float w,float h,Vector2 pivot){var r=(RectTransform)t;r.anchorMin=r.anchorMax=anchor;r.pivot=pivot;r.anchoredPosition=new(x,y);r.sizeDelta=new(w,h);}
    static void Stretch(Transform t,float l=0,float top=0,float r=0,float b=0){var rt=(RectTransform)t;rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=new(l,b);rt.offsetMax=new(-r,-top);}
    static void Band(Transform t,bool top,float left,float right,float offset,float height){var rt=(RectTransform)t;rt.anchorMin=new(0,top?1:0);rt.anchorMax=new(1,top?1:0);rt.pivot=new(.5f,top?1:0);rt.offsetMin=new(left,top?-offset-height:offset);rt.offsetMax=new(-right,top?-offset:offset+height);}
    static void Free(Transform t){foreach(var l in t.GetComponents<LayoutGroup>())l.enabled=false;foreach(var f in t.GetComponents<ContentSizeFitter>())f.enabled=false;var le=t.GetComponent<LayoutElement>();if(le!=null)le.ignoreLayout=true;}
    static UnityEngine.UI.Image Image(Transform parent,string name){var t=parent.Find(name);if(t==null){t=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image)).transform;t.SetParent(parent,false);}return t.GetComponent<UnityEngine.UI.Image>();}
    static TMP_Text Text(Transform parent,string name,string value,float size){var t=parent.Find(name);if(t==null){t=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)).transform;t.SetParent(parent,false);}var text=t.GetComponent<TMP_Text>();text.text=value;Style(text,size);return text;}
    static void Style(TMP_Text t,float size,bool dark=false){t.font=font;t.fontSharedMaterial=dark?ink:light;t.fontSize=size;t.fontStyle=FontStyles.Normal;t.color=dark?Ink:White;t.enableAutoSizing=false;t.enableVertexGradient=false;t.characterSpacing=0;t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Ellipsis;t.raycastTarget=false;t.margin=Vector4.zero;}
    static Material Mat(string name,Color rim,float width,float highlight=0,float fill=1){var path=Mats+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("BB/UI Rounded Rect"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_Color",Color.white);m.SetColor("_BorderColor",rim);m.SetFloat("_BorderWidth",width);m.SetFloat("_FillAlpha",fill);m.SetFloat("_GlassHighlight",highlight);EditorUtility.SetDirty(m);return m;}
    static void Round(UnityEngine.UI.Image im,Material material,Color color,float radius){im.enabled=true;im.sprite=null;im.type=UnityEngine.UI.Image.Type.Simple;im.material=material;im.color=color;im.raycastTarget=false;var round=im.GetComponent<UIRoundImage>()??im.gameObject.AddComponent<UIRoundImage>();round.enabled=true;var so=new SerializedObject(round);so.FindProperty("radiusOverride").floatValue=radius;so.FindProperty("bottomBrightness").floatValue=.84f;so.ApplyModifiedPropertiesWithoutUndo();round.Apply();}
    static TMP_FontAsset BuildFont(string name,bool dynamic,string chars){var path=Fonts+name+" SDF.asset";var f=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);if(f!=null)return f;f=TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(Fonts+"ONE-Mobile-POP.ttf"),50,5,GlyphRenderMode.SDFAA,dynamic?1024:2048,dynamic?1024:2048,AtlasPopulationMode.Dynamic,dynamic);f.name=name;
        if(!dynamic){f.TryAddCharacters(chars);f.atlasPopulationMode=AtlasPopulationMode.Static;}
        AssetDatabase.CreateAsset(f,path);foreach(var tx in f.atlasTextures){tx.name=name+" Atlas";AssetDatabase.AddObjectToAsset(tx,f);}f.material.name=name+" Base";AssetDatabase.AddObjectToAsset(f.material,f);var so=new SerializedObject(f);so.FindProperty("m_ClearDynamicDataOnBuild").boolValue=dynamic;so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(f);return f;}
    static Material FontMat(string name,bool dark){var path=Fonts+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(font.material);AssetDatabase.CreateAsset(m,path);}m.CopyPropertiesFromMaterial(font.material);m.SetColor("_FaceColor",Color.white);m.SetFloat("_FaceDilate",0);m.SetFloat("_OutlineWidth",dark?0:.045f);m.SetColor("_OutlineColor",Hex("382141"));m.SetColor("_UnderlayColor",Hex("382141",dark?0:.65f));m.SetFloat("_UnderlayOffsetX",0);m.SetFloat("_UnderlayOffsetY",-.13f);m.SetFloat("_UnderlaySoftness",.03f);m.DisableKeyword("BEVEL_ON");if(!dark){m.EnableKeyword("UNDERLAY_ON");m.EnableKeyword("OUTLINE_ON");}else{m.DisableKeyword("UNDERLAY_ON");m.DisableKeyword("OUTLINE_ON");}EditorUtility.SetDirty(m);return m;}
    static void Load(){font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Fonts+"LuigiLettering SDF.asset");light=AssetDatabase.LoadAssetAtPath<Material>(Fonts+"LuigiLetteringLight.mat");ink=AssetDatabase.LoadAssetAtPath<Material>(Fonts+"LuigiLetteringInk.mat");glass=Mat("UI_LuigiRoomGlass",Hex("BC8EE8",.7f),.07f,.23f);selected=Mat("UI_LuigiRoomSelection",Hex("CBE3B9",.55f),.045f,0,0);plain=Mat("UI_LuigiRoomPlain",Color.clear,0,.15f);lime=Mat("UI_LuigiRoomLime",Hex("ECFFD8",.8f),.045f,.5f);}
    public static string Main(){if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop first");
        AssetDatabase.ImportAsset(Fonts+"ONE-Mobile-POP.ttf",ImportAssetOptions.ForceSynchronousImport);
        var existing=new[]{"Title","Body"}.SelectMany(r=>AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Fonts+"Luigi"+r+" SDF.asset").characterTable).Select(c=>(char)c.unicode);
        var chars=new string(existing.Concat("함께할 크루팀을 확인하고 준비해 주세요준비 완료중참가 대기12345678✓뒤로Esc").Distinct().ToArray());
        font=BuildFont("LuigiLettering",false,chars);var fallback=BuildFont("LuigiLetteringFallback",true,"");font.fallbackFontAssetTable=new System.Collections.Generic.List<TMP_FontAsset>{fallback,AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Fonts+"Pretendard-Regular SDF.asset")};EditorUtility.SetDirty(font);
        light=FontMat("LuigiLetteringLight",false);ink=FontMat("LuigiLetteringInk",true);Load();
        Edit(Parts+"UIRoomTeamCard.prefab",Team);Edit(Screen,Room);AssetDatabase.SaveAssets();
        return "Existing UIRoomScreen / UIRoomTeamCard updated, ONE Mobile POP static + dynamic fallback; callbacks and GUIDs retained";
    }
    static void Team(GameObject g){var t=g.transform;var card=g.GetComponent<UIRoomTeamCard>();Round(t.Find("UISurfacePart").GetComponent<UnityEngine.UI.Image>(),glass,Plum,23);Stretch(t.Find("UISurfacePart"));
        var frame=Ref<GameObject>(card,"selectedFrame");Round(frame.GetComponent<UnityEngine.UI.Image>(),selected,Color.white,23);Stretch(frame.transform);frame.transform.SetAsFirstSibling();
        var plate=Ref<UnityEngine.UI.Image>(card,"teamPlate");Round(plate,lime,Color.white,14);Place(plate.transform,TL,38,-22,56,56,TL);
        var num=Text(plate.transform,"Number","1",38);Stretch(num.transform);num.alignment=TextAlignmentOptions.Center;Field(card,"teamNumber",num);
        var heading=Ref<TMP_Text>(card,"heading");Band(heading.transform,true,112,20,22,56);Style(heading,35);heading.alignment=TextAlignmentOptions.MidlineLeft;
        var accent=Image(t,"TeamAccent");Round(accent,plain,Color.white,7);Place(accent.transform,TL,0,-1,16,102,TL);Field(card,"teamAccent",accent);
        var members=t.Find("Members");Place(members,TC,0,-82,448,272,TC);var grid=members.GetComponent<GridLayoutGroup>();grid.cellSize=new(224,272);grid.spacing=Vector2.zero;grid.padding=new RectOffset();grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=2;grid.childAlignment=TextAnchor.UpperCenter;
        foreach(var roster in members.GetComponentsInChildren<UICrewRosterCard>(true)){
            foreach(var surface in roster.GetComponentsInChildren<UISurfacePart>(true))surface.Graphic.enabled=false;
            Ref<GameObject>(roster,"selfMark").GetComponent<UnityEngine.UI.Image>().enabled=false;Ref<UnityEngine.UI.Image>(roster,"teamStripe").enabled=false;
            var crop=roster.transform.Find("PortraitCrop");Band(crop,true,0,0,0,198);var portrait=Ref<UnityEngine.UI.Image>(roster,"portrait");Place(portrait.transform,C,0,8,242,218,C);portrait.preserveAspect=true;portrait.material=null;portrait.color=Color.white;
            var name=Ref<TMP_Text>(roster,"playerName");Band(name.transform,false,5,5,67,34);Style(name,25);name.alignment=TextAlignmentOptions.Center;
            var statusBack=Image(roster.transform,"ReadyStatusBackground");Round(statusBack,glass,Lime,21);Band(statusBack.transform,false,25,25,18,42);statusBack.transform.SetAsFirstSibling();
            var status=Text(roster.transform,"ReadyStatus","준비 완료",23);Band(status.transform,false,63,26,19,41);status.alignment=TextAlignmentOptions.Center;status.fontSharedMaterial=ink;Field(roster,"readyStatus",status);Field(roster,"readyStatusBackground",statusBack);
            var mark=Ref<GameObject>(roster,"readyMark");var markImage=mark.GetComponent<UnityEngine.UI.Image>();markImage.enabled=true;markImage.material=null;markImage.sprite=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/UI/Sprites/Common/LuigiSettingsKit.png").OfType<Sprite>().Single(s=>s.name=="SettingCheck");markImage.color=Hex("173B25");Place(mark.transform,new(0,0),43,39,25,25,C);var check=mark.GetComponentInChildren<TMP_Text>(true);check.enabled=false;
        }
        var button=g.GetComponent<UnityEngine.UI.Button>();button.transition=Selectable.Transition.ColorTint;button.targetGraphic=t.Find("UISurfacePart").GetComponent<UnityEngine.UI.Image>();var colors=ColorBlock.defaultColorBlock;colors.highlightedColor=new(1.12f,1.08f,1.15f,1);colors.disabledColor=Color.white;button.colors=colors;
    }
    static void Room(GameObject g){var t=g.transform;var room=g.GetComponent<UIRoomScreen>();var panel=t.Find("Panel");Free(panel);Stretch(panel);panel.GetComponent<UnityEngine.UI.Image>().enabled=false;panel.Find("LuigiSurface").GetComponent<UnityEngine.UI.Image>().enabled=false;
        var scale=g.GetComponent<UIFontScale>();var so=new SerializedObject(scale);var roots=so.FindProperty("fixedSizeRoots");roots.arraySize=1;roots.GetArrayElementAtIndex(0).objectReferenceValue=panel;so.ApplyModifiedPropertiesWithoutUndo();
        var backdropPath="Assets/Art/UI/Sprites/Title/LuigiRoomBackdrop.png";AssetDatabase.ImportAsset(backdropPath,ImportAssetOptions.ForceSynchronousImport);var importer=(TextureImporter)AssetImporter.GetAtPath(backdropPath);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.mipmapEnabled=false;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();t.Find("Backdrop").GetComponent<UnityEngine.UI.Image>().sprite=AssetDatabase.LoadAssetAtPath<Sprite>(backdropPath);
        var title=panel.Find("LobbyHeading").GetComponent<TMP_Text>();Band(title.transform,true,52,400,43,88);Style(title,78);title.text="함께할 크루";title.enableVertexGradient=true;title.colorGradient=new VertexGradient(Color.white,Color.white,Hex("F1DFC5"),Hex("F1DFC5"));
        var roomTitle=Ref<TMP_Text>(room,"roomTitle");Free(roomTitle.transform);roomTitle.gameObject.SetActive(false);
        var status=Ref<TMP_Text>(room,"status");Free(status.transform);Band(status.transform,true,64,400,137,40);Style(status,27);status.text="팀을 확인하고 준비해 주세요";
        var teams=Ref<RectTransform>(room,"teamRow");Free(teams);Place(teams,C,0,-28,1010,736,C);var grid=teams.GetComponent<GridLayoutGroup>();grid.enabled=true;grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=2;grid.cellSize=new(494,359);grid.spacing=new(22,18);grid.padding=new RectOffset();grid.childAlignment=TextAnchor.UpperCenter;
        var count=Ref<TMP_Text>(room,"readyCount");Free(count.transform);Place(count.transform,TR,-50,-58,205,53,TR);Style(count,30);count.alignment=TextAlignmentOptions.Center;var countBack=Image(panel,"ReadyCounterBackground");Round(countBack,glass,Plum,20);Place(countBack.transform,TR,-45,-57,215,56,TR);countBack.transform.SetAsFirstSibling();
        var actions=panel.Find("Actions");Free(actions);Stretch(actions);
        foreach(var field in new[]{"startButton","readyButton"}){var b=Ref<UnityEngine.UI.Button>(room,field);Free(b.transform);Place(b.transform,BR,-50,52,269,79,BR);var surface=b.GetComponentInChildren<UISurfacePart>(true);Round(surface.Graphic,lime,Lime,38);Stretch(surface.transform);var label=b.GetComponentInChildren<TMP_Text>(true);Style(label,33,true);Stretch(label.transform,20,8,20,8);label.alignment=TextAlignmentOptions.Center;var cb=ColorBlock.defaultColorBlock;cb.disabledColor=new(.7f,.65f,.75f,.85f);b.transition=Selectable.Transition.ColorTint;b.targetGraphic=surface.Graphic;b.colors=cb;}
        var leave=Ref<UnityEngine.UI.Button>(room,"leaveButton");Free(leave.transform);Place(leave.transform,new(0,0),58,52,165,52,new(0,0));foreach(var surface in leave.GetComponentsInChildren<UISurfacePart>(true))surface.Graphic.enabled=false;var backText=leave.GetComponentInChildren<TMP_Text>(true);Style(backText,25);backText.text="뒤로";Stretch(backText.transform,72,0,0,0);backText.alignment=TextAlignmentOptions.MidlineLeft;
        var keyBack=Image(leave.transform,"EscapeKey");Round(keyBack,glass,White,9);Place(keyBack.transform,new(0,.5f),0,0,57,43,new(0,.5f));var key=Text(keyBack.transform,"Glyph","Esc",23);Stretch(key.transform);key.fontSharedMaterial=ink;key.color=Ink;key.alignment=TextAlignmentOptions.Center;
    }
    public static string PromoteFonts(){if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop first");Load();int count=0;
        var paths=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Art/UI/Prefabs"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p.Contains("/Parts/")?0:1).ToArray();
        foreach(var path in paths)Edit(path,g=>{foreach(var t in g.GetComponentsInChildren<TMP_Text>(true)){
            if(t.font==null||!new[]{"LuigiTitle","LuigiBody"}.Any(n=>t.font.name.Contains(n)))continue;
            bool dark=t.fontSharedMaterial!=null&&t.fontSharedMaterial.name.Contains("Ink");t.font=font;t.fontSharedMaterial=dark?ink:light;t.fontStyle=FontStyles.Normal;t.characterSpacing=0;t.enableAutoSizing=false;count++;
        }});AssetDatabase.SaveAssets();return count+" existing title/body TMP references now share ONE Mobile POP; number role retained";
    }
    public static string FinalizeAssets(){if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop first");Load();
        var number=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Fonts+"LuigiNumber SDF.asset");number.fallbackFontAssetTable=new System.Collections.Generic.List<TMP_FontAsset>{font};EditorUtility.SetDirty(number);
        Edit(Screen,g=>{var image=g.transform.Find("Backdrop").GetComponent<UnityEngine.UI.Image>();var fit=image.GetComponent<AspectRatioFitter>()??image.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;fit.aspectRatio=image.sprite.rect.width/image.sprite.rect.height;});
        Edit(Parts+"UIRoomTeamCard.prefab",g=>{g.transform.Find("UISurfacePart").SetAsFirstSibling();g.transform.Find("SelectedFrame").SetSiblingIndex(1);});
        AssetDatabase.SaveAssets();return "Number mixed-script fallback updated; room background keeps its aspect ratio; selected team uses the existing subtle outline";
    }
    public static string ShareStatusPart(){if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop first");Load();
        Edit(Parts+"UIRoomTeamCard.prefab",g=>{foreach(var seat in g.GetComponentsInChildren<UICrewRosterCard>(true)){
            foreach(var name in new[]{"ReadyStatus","ReadyStatusBackground"}){var child=seat.transform.Find(name);if(child==null||!PrefabUtility.IsAddedGameObjectOverride(child.gameObject))continue;
                Field(seat,name=="ReadyStatus"?"readyStatus":"readyStatusBackground",null);UnityEngine.Object.DestroyImmediate(child.gameObject);}
        }});
        Edit(Parts+"UICrewRosterCard.prefab",g=>{var roster=g.GetComponent<UICrewRosterCard>();
            var background=Image(g.transform,"ReadyStatusBackground");Round(background,glass,Lime,21);Band(background.transform,false,25,25,18,42);background.transform.SetAsFirstSibling();Field(roster,"readyStatusBackground",background);background.gameObject.SetActive(false);
            var status=Text(g.transform,"ReadyStatus","준비 완료",23);Band(status.transform,false,63,26,19,41);status.alignment=TextAlignmentOptions.Center;status.fontSharedMaterial=ink;Field(roster,"readyStatus",status);status.gameObject.SetActive(false);
        });
        Edit(Parts+"UIRoomTeamCard.prefab",g=>{Team(g);foreach(var seat in g.GetComponentsInChildren<UICrewRosterCard>(true)){
            seat.transform.Find("ReadyStatus").gameObject.SetActive(true);seat.transform.Find("ReadyStatusBackground").gameObject.SetActive(true);
        }});FinalizeAssets();AssetDatabase.SaveAssets();return "Status label/background now belong to the existing shared UICrewRosterCard prefab; team seats use nested prefab instances";
    }
}

