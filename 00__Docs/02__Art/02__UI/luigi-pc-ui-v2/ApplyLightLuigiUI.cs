using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// 승인된 light-ui-preview를 기존 파츠와 화면에 적용한다. 게임 데이터와 입력 배선은 보존한다.
public static class ApplyLightLuigiUI
{
    const string Root = "Assets/Art/UI/Prefabs/", Fonts = "Assets/Art/Fonts/";
    const string MatRoot = "Assets/Art/Environment/Materials/";
    const string Docs = "../00__Docs/02__Art/02__UI/luigi-pc-ui-v2/";
    static readonly Vector2 TL = new(0,1), TR = Vector2.one, TC = new(.5f,1), BC = new(.5f,0), C = new(.5f,.5f);
    static readonly Color Panel = H("58366F",.88f), Deep = H("2B1D40",.90f), White = H("FFF9FF"), Lime = H("B2F278"), Muted = H("D0B9E8");
    static Material panelMat, selectedMat, outlineMat, plainMat;
    static readonly HashSet<string> Surfaces = new(StringComparer.Ordinal) {
        "LuigiCream","MenuRegistration","CrewClipboard","PlayTiming","CrewSlot","LuigiGreen","MenuCream","LuigiPanel","PlayClipboard",
        "RoomNormal","RoomSelected","SettingField","LuigiRowPlaque","MenuBoard","MenuBook","MenuBrown","MenuClipboard","MenuGreen","MenuRed",
        "LuigiDisabled","LuigiHeader","LuigiRed","LuigiSelected","SettingTrack","SettingKnob","SettingFrame","SettingToggle","SettingApply","SettingClose",
        "CrewRegistration","CrewTeam","CrewConfirm","ReceiptPaper","ReceiptBoard","ReceiptFooter","PlayBoard","LuigiTimerPlaque","LuigiSalesPlaque",
        "RoomBrown","RoomRed","SettingJoin","Btn_Title","HudClockStopwatch","HudClockGauge","PlayClock","SkillRing","CrewSelection",
        "ReceiptGold","ReceiptSilver","ReceiptCopper","ReceiptPewter" };
    static int surfaces, texts;

    public static string Main()
    {
        if(EditorApplication.isPlaying || EditorApplication.isCompiling) throw new InvalidOperationException("Editor must be stopped and ready");
        Materials(); Typography(); Theme(); Backgrounds();
        // 기반 파츠 → Variant → 복합 파츠 → 화면 순으로 저장한다. 프리팹 연결은 풀지 않는다.
        var paths=AssetDatabase.FindAssets("t:Prefab",new[]{Root.TrimEnd('/')}).Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p=>p.EndsWith("/UISurfacePart.prefab")?0:Path.GetFileName(p).StartsWith("UISurface")?1:p.Contains("/Parts/")?2:3).ThenBy(p=>p).ToArray();
        foreach(var path in paths) Edit(path,g=> { General(g); Special(g,Path.GetFileNameWithoutExtension(path)); });
        AssetDatabase.SaveAssets();Polish();LayoutPolish();TeamSlots();TextFit();FinalBounds();
        return Newtonsoft.Json.JsonConvert.SerializeObject(new{prefabs=paths.Length,surfaces,texts,materials=6,background="Approved clean purple cafe; existing CharacterStage untouched"});
    }
    static Color H(string s,float a=1){ColorUtility.TryParseHtmlString("#"+s,out var c);c.a=a;return c;}
    static Material Material(string name,Color border,float width,float fill=1)
    {
        var path=MatRoot+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("BB/UI Rounded Rect"));AssetDatabase.CreateAsset(mat,path);}
        mat.SetColor("_Color",Color.white);mat.SetColor("_BorderColor",border);mat.SetFloat("_BorderWidth",width);mat.SetFloat("_FillAlpha",fill);EditorUtility.SetDirty(mat);return mat;
    }
    static void Materials(){panelMat=Material("UI_LuigiGlass",H("BBA0DB",.72f),.065f);selectedMat=Material("UI_LuigiSelected",Lime,.13f);outlineMat=Material("UI_LuigiSelectionOutline",Lime,.13f,0);plainMat=Material("UI_LuigiSoftFill",Color.clear,0);}
    public static string Polish()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode first");
        Materials();var state=Material("UI_LuigiStateOutline",Color.white,.12f,0);state.SetFloat("_BorderTintInfluence",1);EditorUtility.SetDirty(state);
        var backdrop=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Sprites/Title/LuigiTitleBackdrop.png");
        foreach(var path in AssetDatabase.FindAssets("t:Prefab",new[]{Root.TrimEnd('/')}).Select(AssetDatabase.GUIDToAssetPath))Edit(path,g=>{
            foreach(var im in g.GetComponentsInChildren<Image>(true)){
                if(new[]{"SelectionFrame","SelfFrame","Selected","SelectedFrame"}.Contains(im.name)){
                    Round(im,Color.white,outlineMat,14);im.transform.SetAsLastSibling();im.raycastTarget=false;
                }
                if(im.sprite!=null&&im.sprite.name=="RoomPhoto"){
                    im.sprite=backdrop;im.type=Image.Type.Simple;im.color=Color.white;im.material=UITheme.Config.RoundedMaterial;im.preserveAspect=false;
                    var r=im.GetComponent<UIRoundImage>()??im.gameObject.AddComponent<UIRoundImage>();r.enabled=true;
                }
                if(im.sprite!=null&&im.sprite.name=="RoomPeople")im.color=White;
                if(im.name=="ReadyBadge"){im.color=Lime;foreach(var label in im.GetComponentsInChildren<TMP_Text>(true))label.color=Deep;}
                if(im.name.StartsWith("ReferenceHeading")&&im.GetComponent<Image>()!=null)im.GetComponent<Image>().color=Color.clear;
            }
            if(g.name.StartsWith("UIRoomListScreen")){
                Find(g.transform,"Panel/RoomDetails/OccupancyIcon").GetComponent<Image>().enabled=false;
                Find(g.transform,"Panel/RoomDetails/TeamCount").GetComponentsInChildren<Image>(true).Where(x=>x.sprite!=null&&x.sprite.name=="RoomPeople").ToList().ForEach(x=>x.enabled=false);
            }
            foreach(var card in g.GetComponentsInChildren<UIMakingCard>(true)){
                var frame=Ref<Image>(card,"frame");if(frame!=null)Round(frame,White,state);
                var glow=Ref<Image>(card,"glow");if(glow!=null){Round(glow,White,state);glow.enabled=true;}
            }
            foreach(var final in g.GetComponentsInChildren<UIMatchResultPopup>(true)){
                var so=new SerializedObject(final);foreach(var key in new[]{"rankText","rankRevenueText"})so.FindProperty(key).colorValue=White;
                so.FindProperty("myRankBar").colorValue=Lime;so.FindProperty("otherRankBar").colorValue=Muted;so.FindProperty("win").colorValue=Lime;so.ApplyModifiedPropertiesWithoutUndo();
            }
            if(g.name.StartsWith("UICharacterSelectScreen")){
                var team=Find(g.transform,"Stage/PanelWrap/Detail/Nameplate");var h=team.GetComponent<HorizontalLayoutGroup>();h.childControlWidth=true;h.childForceExpandWidth=true;h.spacing=10;h.padding=new RectOffset();
                foreach(var b in team.GetComponentsInChildren<UnityEngine.UI.Button>(true))foreach(var e in b.GetComponents<LayoutElement>()){e.minWidth=0;e.preferredWidth=125;e.flexibleWidth=1;}
            }
        });AssetDatabase.SaveAssets();return "Selection outlines, unframed thumbnails, ready marks, responsive team row and state-only manufacturing outlines saved";
    }
    public static string LayoutPolish()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode first");Materials();
        var rim=Material("UI_LuigiSoftOutline",Muted,.075f,0);
        var paths=AssetDatabase.FindAssets("t:Prefab",new[]{Root.TrimEnd('/')}).Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p=>p.Contains("/Parts/")?0:1).ToArray();
        foreach(var path in paths)Edit(path,g=>{
            foreach(var surface in g.GetComponentsInChildren<UISurfacePart>(true))surface.transform.SetAsFirstSibling();
            foreach(var slot in g.GetComponentsInChildren<UISkillSlot>(true)){
                var r=slot.transform.Find("ApprovedSkillRim");if(r!=null)Round(r.GetComponent<Image>(),Color.white,rim,42);
                var back=slot.transform.Find("Circle/Back");if(back!=null)back.GetComponent<Image>().color=Deep;
                var key=slot.transform.Find("KeyBadge");if(key!=null)Round(key.GetComponent<Image>(),Deep,panelMat,8);
            }
            foreach(var slider in g.GetComponentsInChildren<Slider>(true)){
                if(slider.handleRect!=null)Place(slider.handleRect,C,0,0,28,28,C);
                if(slider.fillRect!=null){var im=slider.fillRect.GetComponent<Image>();if(im!=null)im.color=Lime;}
            }
            foreach(var row in g.GetComponentsInChildren<UIStandingRow>(true)){
                var so=new SerializedObject(row);so.FindProperty("mineBadge").colorValue=Lime;so.FindProperty("mineRank").colorValue=Deep;
                so.FindProperty("otherBadge").colorValue=Deep;so.FindProperty("otherRank").colorValue=White;
                var colors=so.FindProperty("teamColors");for(int i=0;i<colors.arraySize;i++)colors.GetArrayElementAtIndex(i).colorValue=Color.Lerp(Deep,UITheme.Config.TeamColors[i%UITheme.Config.TeamColors.Length],.27f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach(var card in g.GetComponentsInChildren<UIMakingCard>(true))MakingLayout(card);
            if(g.name.StartsWith("UIMatchHudScreen"))CompactHud(g);
            if(g.name.StartsWith("UIDaySettlementScreen"))CompactSettlement(g);
        });AssetDatabase.SaveAssets();return "Native icon occlusion, slider handles, compact HUD, two-line manufacturing cards and four-panel settlement layout saved";
    }
    static TMP_Text Label(Transform parent,string name,string value,float size)
    {
        var t=parent.Find(name);if(t==null){var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);t=go.transform;}
        var text=t.GetComponent<TMP_Text>();text.text=value;text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Fonts+"LuigiTitle SDF.asset");text.fontSharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Fonts+"LuigiTitleLight.mat");text.color=White;text.raycastTarget=false;Text(t,size);return text;
    }
    static void MakingLayout(UIMakingCard card)
    {
        var t=card.transform;((RectTransform)t).sizeDelta=new(292,240);var header=Find(t,"Body/Header");Free(header);Stretch(header);
        var texts=Find(header,"Texts");Free(texts);Stretch(texts);
        var status=Ref<TMP_Text>(card,"statusText");Place(status.transform,TC,0,-9,250,42,TC);Text(status.transform,26,TextAlignmentOptions.Center);
        var state=Find(texts,"StatePlate");Place(state,TC,0,-8,252,44,TC);Round(state.GetComponent<Image>(),Deep,AssetDatabase.LoadAssetAtPath<Material>(MatRoot+"UI_LuigiStateOutline.mat"),20);
        var badge=Ref<GameObject>(card,"menuBadge");Place(badge.transform,TL,18,-70,110,110,TL);
        var name=Ref<TMP_Text>(card,"nameText");var nameRow=Find(texts,"NameRow");Free(nameRow);Place(nameRow,TL,132,-75,150,90,TL);Stretch(name.transform);Text(name.transform,21,TextAlignmentOptions.MidlineLeft);name.textWrappingMode=TextWrappingModes.Normal;
        var check=Ref<TMP_Text>(card,"check");if(check!=null){Place(check.transform,TR,-9,-53,35,35,TR);Text(check.transform,29,TextAlignmentOptions.Center);check.color=Lime;}
        var parts=Ref<RectTransform>(card,"partsRow");Place(parts,BC,0,13,250,44,BC);var template=Ref<Image>(card,"partTemplate");if(template!=null){((RectTransform)template.transform).sizeDelta=new(38,38);var le=template.GetComponent<LayoutElement>();if(le!=null){le.minWidth=le.preferredWidth=38;le.minHeight=le.preferredHeight=38;}}
        Off(t,"ClipboardClip");
    }
    static void CompactHud(GameObject g)
    {
        var t=g.transform;FixedFonts(g);
        var standings=Find(t,"DayRoot/DayHeader/TeamStandings");Free(standings);Place(standings,TL,25,-22,520,116,TL);var outer=Surface(standings);if(outer!=null)outer.color=Color.clear;
        var rows=standings.GetComponentsInChildren<UIStandingRow>(true);for(int i=0;i<rows.Length;i++){
            var row=rows[i];Free(row.transform);Place(row.transform,TL,i==0?0:(i-1)*170,i==0?0:-65,i==0?310:160,i==0?58:39,TL);
            var badge=Ref<Image>(row,"badge");if(badge!=null){if(badge.transform.parent.name=="BadgeEdge"){Place(badge.transform.parent,new(0,.5f),i==0?24:17,0,i==0?34:25,i==0?34:25,C);Stretch(badge.transform);}else Place(badge.transform,new(0,.5f),20,0,32,32,C);Text(Ref<TMP_Text>(row,"rank").transform,i==0?25:18,TextAlignmentOptions.Center);}
            var cafe=Ref<TMP_Text>(row,"cafe");Place(cafe.transform,new(0,.5f),i==0?49:34,0,i==0?84:42,34,new(0,.5f));Text(cafe.transform,i==0?28:17);
            var total=Ref<TMP_Text>(row,"total");Place(total.transform,new(1,.5f),-8,0,i==0?156:78,38,new(1,.5f));Text(total.transform,i==0?35:20,TextAlignmentOptions.Right);
        }
        var clock=Find(t,"DayRoot/DayHeader/ClockBadge");Place(clock,TC,0,-20,325,54,TC);Place(Find(clock,"Clock"),TC,0,-60,210,54,TC);Text(Find(clock,"Clock"),40,TextAlignmentOptions.Center);
        var clockBody=Find(clock,"Body").GetComponent<Image>();clockBody.enabled=true;Place(clockBody.transform,TC,0,-56,218,62,TC);Round(clockBody,Deep,panelMat,20);clockBody.transform.SetSiblingIndex(1);
        Place(Find(clock,"DayCounter"),C,-90,0,80,40,C);Text(Find(clock,"DayCounter"),26,TextAlignmentOptions.Center);var caption=Find(clock,"DayLabel");caption.gameObject.SetActive(true);Place(caption,C,45,0,180,40,C);Text(caption,25,TextAlignmentOptions.Center).text="일차 · 카페 운영";
        var sales=Find(t,"DayRoot/DayHeader/SalesBlock");Place(sales,TR,-25,-22,245,67,TR);Off(sales,"SalesSeparator");Off(sales,"SalesCaptionPlate");
        Place(Find(sales,"Revenue"),TR,-15,-7,160,52,TR);Text(Find(sales,"Revenue"),43,TextAlignmentOptions.Right);var coin=Find(sales,"Coin");coin.gameObject.SetActive(true);Place(coin,TL,12,-12,43,43,TL);
        Place(Find(sales,"Label"),TR,-100,-74,128,35,TR);Text(Find(sales,"Label"),21,TextAlignmentOptions.Right).text="임대료";Place(Find(sales,"Goal"),TR,0,-74,90,35,TR);Text(Find(sales,"Goal"),25,TextAlignmentOptions.Right);Place(Find(sales,"RentMet"),TR,0,-115,160,28,TR);
        var dock=Find(t,"DayRoot/MakingCardDock");Place(dock,BC,345,260,292,240,BC);
        var night=Find(t,"NightRoot/TopCenter");Place(night,TC,0,-25,425,60,TC);var oldPlate=Find(night,"ApprovedTimerPlate");var oldFace=Surface(oldPlate);if(oldFace!=null)oldFace.color=Color.clear;
        Place(Find(night,"Timer"),C,127,0,150,48,C);Text(Find(night,"Timer"),38,TextAlignmentOptions.Center);Place(Find(night,"Day"),C,-105,0,90,44,C);Text(Find(night,"Day"),25,TextAlignmentOptions.Center);Place(Find(night,"Phase"),C,-5,0,125,44,C);Text(Find(night,"Phase"),24,TextAlignmentOptions.Center);Place(Find(night,"Moon"),C,-177,0,30,30,C);Off(night,"Separator");
        var left=Find(t,"NightRoot/TopLeft");Place(left,TL,25,-25,250,84,TL);Place(Find(left,"Revenue"),TL,65,-4,172,52,TL);Text(Find(left,"Revenue"),35);Place(Find(left,"ApprovedCoin"),TL,10,-10,42,42,TL);Place(Find(left,"RevenueCaption"),TL,70,-54,170,25,TL);Text(Find(left,"RevenueCaption"),20);
    }
    static void CompactSettlement(GameObject g)
    {
        var t=g.transform;var summary=Find(t,"Stage/Cards/SummaryCard");Place(summary,TL,180,-210,570,355,TL);var day=Ref<TMP_Text>(g.GetComponent<UIDaySettlementScreen>(),"dayHeading").transform;day.SetParent(Find(t,"Stage"),false);Place(day,TL,180,-60,1450,110,TL);Text(day,74);
        var title=Find(summary,"Title");Place(title,TL,25,-15,520,62,TL);Text(title,40).text="오늘의 매출";
        var lines=new[]{"Sales","Rent","Payment","Streak"};for(int i=0;i<lines.Length;i++){var line=Find(summary,lines[i]);Place(line,TL,25,-82-i*63,520,60,TL);Text(Find(line,"Label"),23);Text(Find(line,"Value"),i==0?55:32,TextAlignmentOptions.Right);}
        var standing=Find(t,"Stage/Cards/StandingsCard");Place(standing,TL,775,-210,625,355,TL);Place(Find(standing,"Title"),TL,25,-15,575,60,TL);Text(Find(standing,"Title"),39);Stretch(Find(standing,"RankingRows"),22,84,22,18);
        var rowGroup=Find(standing,"RankingRows").GetComponent<VerticalLayoutGroup>();if(rowGroup!=null){rowGroup.childControlHeight=true;rowGroup.childForceExpandHeight=false;rowGroup.spacing=6;}
        foreach(var row in standing.GetComponentsInChildren<UIStandingRow>(true)){var le=row.GetComponent<LayoutElement>()??row.gameObject.AddComponent<LayoutElement>();le.minHeight=le.preferredHeight=56;le.flexibleHeight=0;var badge=Ref<Image>(row,"badge");var be=badge.GetComponent<LayoutElement>();if(be!=null){be.minWidth=be.preferredWidth=44;be.minHeight=be.preferredHeight=44;}Text(Ref<TMP_Text>(row,"cafe").transform,26);Text(Ref<TMP_Text>(row,"total").transform,36);Text(Ref<TMP_Text>(row,"rank").transform,25,TextAlignmentOptions.Center);}
        var upgrade=Find(t,"Stage/Cards/UpgradeCard");Place(upgrade,TL,180,-590,570,310,TL);Place(Find(upgrade,"Title"),TL,25,-18,520,55,TL);Text(Find(upgrade,"Title"),37).text="자동 보석 적용";Place(Find(upgrade,"Gems"),TL,25,-90,520,195,TL);
        var forecast=Find(t,"Stage/Cards/ForecastCard");Place(forecast,TL,775,-590,625,310,TL);Place(Find(forecast,"Title"),TL,25,-18,575,55,TL);Text(Find(forecast,"Title"),37);Place(Find(forecast,"CompositionBar"),TL,25,-95,575,40,TL);Place(Find(forecast,"Popular"),TL,25,-208,575,70,TL);
        var legend=Label(forecast,"RaceLegend","좀비 · 유령 · 해골 · 늑대 · 뱀파이어 · 마녀",22);Place(legend.transform,TL,25,-148,575,42,TL);
    }
    public static string TeamSlots()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode first");Materials();int count=0;
        foreach(var path in AssetDatabase.FindAssets("t:Prefab",new[]{Root.TrimEnd('/')}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(x=>x.Contains("/Parts/")?0:1)){
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(asset.GetComponentInChildren<UITeamSlot>(true)==null)continue;
            Edit(path,g=>{foreach(var slot in g.GetComponentsInChildren<UITeamSlot>(true)){
                var own=slot.GetComponent<Image>();if(own!=null)own.color=Color.clear;Surface(slot.transform);
                var accent=NativeImage(slot.transform,"TeamAccent");Stretch(accent.transform,10,0,10,5);var rt=(RectTransform)accent.transform;rt.anchorMax=new(1,0);rt.sizeDelta=new(-20,4);Round(accent,Muted,plainMat,2);
                var selection=NativeImage(slot.transform,"Selection");Stretch(selection.transform);Round(selection,Color.white,outlineMat,15);selection.gameObject.SetActive(false);
                var so=new SerializedObject(slot);so.FindProperty("background").objectReferenceValue=accent;so.FindProperty("selection").objectReferenceValue=selection.gameObject;so.ApplyModifiedPropertiesWithoutUndo();count++;
            }});
        }
        AssetDatabase.SaveAssets();return "Team slots use a small team-color stripe and a separate lime selection: "+count;
    }
    static Image NativeImage(Transform parent,string name)
    {
        var child=parent.Find(name);if(child==null){child=new GameObject(name,typeof(RectTransform),typeof(Image)).transform;child.SetParent(parent,false);}
        var image=child.GetComponent<Image>();image.raycastTarget=false;var element=child.GetComponent<LayoutElement>()??child.gameObject.AddComponent<LayoutElement>();element.ignoreLayout=true;return image;
    }
    static void Typography()
    {
        foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{Fonts.TrimEnd('/')})){
            var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if(!m.name.StartsWith("Luigi")&&!m.name.StartsWith("HudClock"))continue;
            if(!m.HasProperty("_FaceColor"))continue;
            m.SetColor("_FaceColor",White);m.SetColor("_OutlineColor",H("39204E"));m.SetFloat("_OutlineWidth",m.name.Contains("Body")?.035f:.085f);
            m.SetFloat("_FaceDilate",m.name.Contains("Body")?0:.025f);m.SetFloat("_OutlineSoftness",.01f);
            m.SetColor("_UnderlayColor",H("533078",.8f));m.SetFloat("_UnderlayOffsetX",0);m.SetFloat("_UnderlayOffsetY",-.20f);m.SetFloat("_UnderlayDilate",.025f);m.SetFloat("_UnderlaySoftness",.06f);
            m.DisableKeyword("BEVEL_ON");m.EnableKeyword("OUTLINE_ON");m.EnableKeyword("UNDERLAY_ON");EditorUtility.SetDirty(m);
        }
    }
    static void Theme()
    {
        var cfg=AssetDatabase.LoadAssetAtPath<UIThemeConfig>("Assets/Resources/UIThemeConfig.asset");var so=new SerializedObject(cfg);
        var palette=new Dictionary<string,Color>{{"ink",Deep},{"panel",Panel},{"panelDeep",Deep},{"cream",White},{"gold",Muted},{"goldLit",H("F7DB80")},{"green",Lime},{"red",H("FF7182")},{"blue",H("92D9F2")},{"purple",H("C68EF5")},{"ice",White},{"placeholder",Muted},{"paperInk",White}};
        foreach(var pair in palette)so.FindProperty(pair.Key).colorValue=pair.Value;
        so.FindProperty("cornerRadius").floatValue=18;so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(cfg);
    }
    static void Backgrounds()
    {
        // 이미지 자체를 교체하되 기존 GUID와 Sprite 참조를 보존한다. CharacterStage에는 사용하지 않는다.
        var source=Path.GetFullPath(Docs+"light-ui-preview/background.png");
        foreach(var name in new[]{"LuigiTitleBackdrop","LuigiLobbyBackdrop","LuigiSettlementBackdrop","LuigiFinalBackdrop"}){
            var path="Assets/Art/UI/Sprites/Title/"+name+".png";
            File.Copy(source,path,true);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        }
    }
    static void Edit(string path,Action<GameObject> change){var g=PrefabUtility.LoadPrefabContents(path);try{change(g);PrefabUtility.SaveAsPrefabAsset(g,path);}finally{PrefabUtility.UnloadPrefabContents(g);}}
    static void Round(Image im,Color color,Material mat=null,float radius=18)
    {
        im.sprite=null;im.type=Image.Type.Simple;im.preserveAspect=false;im.color=color;im.material=mat??panelMat;
        var r=im.GetComponent<UIRoundImage>()??im.gameObject.AddComponent<UIRoundImage>();r.enabled=true;
        var so=new SerializedObject(r);so.FindProperty("radiusOverride").floatValue=radius;so.FindProperty("bottomBrightness").floatValue=.73f;so.ApplyModifiedPropertiesWithoutUndo();im.SetVerticesDirty();surfaces++;
    }
    static void General(GameObject g)
    {
        foreach(var im in g.GetComponentsInChildren<Image>(true)){
            var n=im.sprite!=null?im.sprite.name:"";
            bool surface=im.GetComponent<UISurfacePart>()!=null;
            if(Surfaces.Contains(n)||surface){
                var selected=n=="CrewSelection";
                Round(im,selected?Color.white:Panel,selected?outlineMat:panelMat,selected?14:18);
                if(selected)im.transform.SetAsLastSibling();
                if(n=="SettingKnob")Round(im,White,plainMat,30);
                if(n=="SettingTrack")Round(im,Deep,plainMat,7);
                if(n=="SkillRing")Round(im,Panel,panelMat,45);
                if(n.StartsWith("Receipt")&&n!="ReceiptPaper"&&n!="ReceiptBoard"&&n!="ReceiptFooter")Round(im,Deep,panelMat,35);
            }
            // 기능이 없는 옛 금속 장식만 걷어내고 입력·데이터를 소유한 부모는 유지한다.
            if(im.name=="ClipboardClip"||im.name=="ServiceBell"||im.name=="BubbleBrassRim"||im.name=="AlarmClock")im.enabled=false;
            if(im.name.StartsWith("Stamp")||im.name=="TimingShine")im.enabled=false;
            if(n=="Circle"&&im.color.r>im.color.b*1.3f&&im.color.g>im.color.b*1.1f)im.color=Muted;
            if(im.GetComponent<Selectable>()==null&&im.GetComponent<Mask>()==null&&im.GetComponent<UISurfacePart>()==null)im.raycastTarget=false;
        }
        foreach(var t in g.GetComponentsInChildren<TMP_Text>(true)){
            var role=t.font!=null&&t.font.name.Contains("Number")?"Number":t.font!=null&&t.font.name.Contains("Body")?"Body":"Title";
            t.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Fonts+"Luigi"+role+" SDF.asset");
            t.fontSharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Fonts+"Luigi"+role+"Light.mat");
            t.enableVertexGradient=false;t.color=White;t.enableAutoSizing=false;t.extraPadding=true;t.raycastTarget=false;texts++;
        }
        foreach(var scaler in g.GetComponentsInChildren<CanvasScaler>(true)){
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        }
        foreach(var component in g.GetComponentsInChildren<MonoBehaviour>(true)){
            if(component==null)continue;var so=new SerializedObject(component);bool dirty=false;
            foreach(var key in new[]{"normalText","selectedText","idleLabel","activeLabel","salesTint","rentTint","clearStreakTint"}){
                var p=so.FindProperty(key);if(p!=null&&p.propertyType==SerializedPropertyType.Color){p.colorValue=White;dirty=true;}
            }
            foreach(var key in new[]{"paidTint"}){var p=so.FindProperty(key);if(p!=null&&p.propertyType==SerializedPropertyType.Color){p.colorValue=Lime;dirty=true;}}
            foreach(var key in new[]{"unpaidTint","missedStreakTint"}){var p=so.FindProperty(key);if(p!=null&&p.propertyType==SerializedPropertyType.Color){p.colorValue=H("FF7182");dirty=true;}}
            if(dirty)so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
    static Transform Find(Transform t,string path)=>t.Find(path)??throw new InvalidOperationException("Missing "+t.name+"/"+path);
    static T Ref<T>(Object obj,string name) where T:Object {var p=new SerializedObject(obj).FindProperty(name);return p!=null?p.objectReferenceValue as T:null;}
    static void Place(Transform t,Vector2 a,float x,float y,float w,float h,Vector2? p=null){var r=(RectTransform)t;r.anchorMin=r.anchorMax=a;r.pivot=p??a;r.anchoredPosition=new(x,y);r.sizeDelta=new(w,h);r.localScale=Vector3.one;}
    static void Stretch(Transform t,float l=0,float top=0,float right=0,float bottom=0){var r=(RectTransform)t;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new(l,bottom);r.offsetMax=new(-right,-top);}
    static void Free(Transform t){foreach(var l in t.GetComponents<LayoutGroup>())l.enabled=false;var f=t.GetComponent<ContentSizeFitter>();if(f!=null)f.enabled=false;}
    static void FixedFonts(GameObject g){foreach(var scale in g.GetComponentsInChildren<UIFontScale>(true)){var so=new SerializedObject(scale);var p=so.FindProperty("fixedSizeRoots");p.arraySize=1;p.GetArrayElementAtIndex(0).objectReferenceValue=scale.transform;so.ApplyModifiedPropertiesWithoutUndo();}}
    static TMP_Text Text(Transform t,float size,TextAlignmentOptions alignment=TextAlignmentOptions.Left){var x=t.GetComponent<TMP_Text>();if(x==null)return null;x.fontSize=size;x.alignment=alignment;x.margin=Vector4.zero;x.enableAutoSizing=false;x.textWrappingMode=TextWrappingModes.NoWrap;return x;}
    static Image Surface(Transform parent,bool primary=false){var s=parent.GetComponentsInChildren<UISurfacePart>(true).FirstOrDefault(x=>x.transform.parent==parent);if(s==null)return null;s.gameObject.SetActive(true);Round(s.Graphic,primary?H("325734",.96f):Panel,primary?selectedMat:panelMat);return s.Graphic;}
    static void Button(Transform t,bool primary=false,float size=32){Surface(t,primary);var text=t.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(x=>x.transform.parent==t);if(text!=null){Stretch(text.transform,20,8,20,8);Text(text.transform,size,TextAlignmentOptions.Center);}var b=t.GetComponent<UnityEngine.UI.Button>();if(b!=null){b.transition=Selectable.Transition.ColorTint;var cb=b.colors;cb.normalColor=Color.white;cb.highlightedColor=new(1.15f,1.1f,1.2f);cb.pressedColor=new(.8f,.8f,.8f);cb.disabledColor=new(.5f,.5f,.5f,.6f);b.colors=cb;}}
    public static string TextFit()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode first");Materials();
        foreach(var path in AssetDatabase.FindAssets("t:Prefab",new[]{Root.TrimEnd('/')}).Select(AssetDatabase.GUIDToAssetPath))Edit(path,g=>{
            foreach(var chip in g.GetComponentsInChildren<Transform>(true).Where(x=>x.name=="EscChip")){
                var parent=chip.parent;var button=parent.GetComponent<UnityEngine.UI.Button>();if(button==null)continue;
                Free(parent);Place(chip,new(0,.5f),12,0,58,38,new(0,.5f));var im=chip.GetComponent<Image>();if(im!=null)Round(im,Deep,panelMat,7);
                foreach(var text in chip.GetComponentsInChildren<TMP_Text>(true)){Stretch(text.transform,2,0,2,0);Text(text.transform,20,TextAlignmentOptions.Center);}
                var label=parent.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(x=>x.transform.parent==parent);if(label!=null){Stretch(label.transform,82,4,10,4);Text(label.transform,parent.name=="Lobby"?34:28,TextAlignmentOptions.MidlineLeft);}
            }
            if(g.name.StartsWith("UICharacterSelectScreen"))foreach(var name in new[]{"DayName","NightName"}){
                var text=g.GetComponentsInChildren<TMP_Text>(true).First(x=>x.name==name);var r=text.rectTransform;r.sizeDelta=new(r.sizeDelta.x,44);
            }
            foreach(var slot in g.GetComponentsInChildren<UITeamSlot>(true)){
                if(Surface(slot.transform)==null){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Parts/UISurfacePart.prefab");var s=(GameObject)PrefabUtility.InstantiatePrefab(prefab);s.transform.SetParent(slot.transform,false);Stretch(s.transform);s.transform.SetAsFirstSibling();Surface(slot.transform);}
            }
            if(g.name.StartsWith("UIDaySettlementScreen")){
                var gemRoot=Find(g.transform,"Stage/Cards/UpgradeCard/Gems");Free(gemRoot);var grid=gemRoot.GetComponent<GridLayoutGroup>()??gemRoot.gameObject.AddComponent<GridLayoutGroup>();grid.enabled=true;grid.cellSize=new(255,64);grid.spacing=new(10,1);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=2;grid.padding=new RectOffset();
                foreach(var row in gemRoot.GetComponentsInChildren<UIGemRow>(true)){
                    Free(row.transform);var icon=Find(row.transform,"UIGemIcon");Place(icon,TL,0,-10,44,44,TL);Free(Find(row.transform,"Texts"));Stretch(Find(row.transform,"Texts"),52,0,0,0);
                    var title=Ref<TMP_Text>(row,"title");Place(title.transform,TL,0,0,200,43,TL);Text(title.transform,17);title.textWrappingMode=TextWrappingModes.Normal;
                    var badge=Find(row.transform,"Texts/TurnBadge");Place(badge,new(0,0),0,0,130,22,new(0,0));Text(Ref<TMP_Text>(row,"turns").transform,15,TextAlignmentOptions.Center);
                }
                Text(Find(g.transform,"Stage/Cards/SummaryCard/Sales/Value"),52,TextAlignmentOptions.Right);
            }
        });AssetDatabase.SaveAssets();return "ESC chip and skill title text bounds corrected; shared team button surfaces enabled";
    }
    public static string FinalBounds()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode first");Materials();
        var state=AssetDatabase.LoadAssetAtPath<Material>(MatRoot+"UI_LuigiStateOutline.mat");int fits=0;
        foreach(var path in AssetDatabase.FindAssets("t:Prefab",new[]{Root.TrimEnd('/')}).Select(AssetDatabase.GUIDToAssetPath))Edit(path,g=>{
            foreach(var text in g.GetComponentsInChildren<TMP_Text>(true)){
                if(!new[]{"Cafe","Total","BasePrice","Title","TimingTitle","RevenueCaption"}.Contains(text.name))continue;
                var rt=text.rectTransform;float minimum=Mathf.Ceil(text.fontSize*1.35f);
                if(rt.anchorMin.y==rt.anchorMax.y&&rt.sizeDelta.y>0&&rt.sizeDelta.y<minimum){rt.sizeDelta=new(rt.sizeDelta.x,minimum);fits++;}
                var e=text.GetComponent<LayoutElement>();if(e!=null&&e.preferredHeight>0&&e.preferredHeight<minimum){e.preferredHeight=minimum;e.minHeight=Mathf.Max(e.minHeight,minimum);}
            }
            foreach(var slot in g.GetComponentsInChildren<UIBoxLootSlot>(true)){
                var so=new SerializedObject(slot);so.FindProperty("blindColor").colorValue=Deep;so.FindProperty("readyColor").colorValue=Panel;so.FindProperty("emptyColor").colorValue=H("2B1D40",.45f);so.FindProperty("plainEdgeColor").colorValue=Muted;so.FindProperty("rarityTextColor").colorValue=White;so.ApplyModifiedPropertiesWithoutUndo();
                foreach(var n in new[]{"Edge","Glow"}){var im=slot.transform.Find(n).GetComponent<Image>();Round(im,Muted,state,16);}
                var blind=slot.transform.Find("Blind").GetComponent<Image>();Round(blind,Deep,panelMat,14);
            }
            foreach(var card in g.GetComponentsInChildren<UICrewRosterCard>(true))Ref<TMP_Text>(card,"playerName").overflowMode=TextOverflowModes.Ellipsis;
            foreach(var row in g.GetComponentsInChildren<UIRoomListRow>(true))Ref<TMP_Text>(row,"roomName").overflowMode=TextOverflowModes.Ellipsis;
            foreach(var slot in g.GetComponentsInChildren<UISkillSlot>(true)){
                var key=Ref<TMP_Text>(slot,"key");if(key==null||key.text.Length<3)continue;
                var badge=slot.transform.Find("KeyBadge");if(badge!=null){Place(badge,C,0,-37,94,34,C);Stretch(key.transform,3,0,3,0);Text(key.transform,20,TextAlignmentOptions.Center);}
            }
        });AssetDatabase.SaveAssets();return "Fixed single-line height bounds: "+fits+"; loot slots use plum faces and shared rarity outlines";
    }
    static void Off(Transform t,string path){var c=t.Find(path);if(c!=null)c.gameObject.SetActive(false);}
    static void Special(GameObject g,string name)
    {
        var t=g.transform;
        if(name.StartsWith("UISurface"))return;
        if(name=="UIRoomListRow"){
            FixedFonts(g);Place(t,C,0,0,730,126);Place(Find(t,"Thumbnail"),new(0,.5f),16,0,210,96,new(0,.5f));Place(Find(t,"Label"),new(0,.5f),248,0,310,85,new(0,.5f));Text(Find(t,"Label"),36);Place(Find(t,"Occupancy"),new(1,.5f),-20,0,100,75,new(1,.5f));Text(Find(t,"Occupancy"),38,TextAlignmentOptions.Right);Place(Find(t,"OccupancyIcon"),new(1,.5f),-136,0,36,40,C);
        }
        if(name=="UICharacterCard"||name=="UICrewRosterCard"){
            FixedFonts(g);var portrait=t.Find(name=="UICharacterCard"?"Portrait":"PortraitCrop");if(portrait!=null)Stretch(portrait,8,8,8,39);
            var label=t.Find(name=="UICharacterCard"?"Name":"PlayerName");if(label!=null){Place(label,BC,0,5,145,32,BC);Text(label,23,TextAlignmentOptions.Center);}
        }
        if(name=="UITitleMenuScreen")Title(g);
        if(name=="UIRoomListScreen")Rooms(g);
        if(name=="UISettingsPopup")Settings(g);
        if(name=="UICharacterSelectScreen")Crew(g);
        if(name=="UIDaySettlementScreen")Settlement(g);
        if(name=="UIMatchResultPopup")Final(g);
        if(name=="UIMatchHudScreen")Hud(g);
        if(name=="UIMakingCard"){Off(t,"ClipboardClip");Off(t,"Glow");}
    }
    static void Title(GameObject g)
    {
        FixedFonts(g);var t=g.transform;var layout=Find(t,"ArtFrame/layout");Place(layout,TL,165,-455,365,225,TL);
        var v=layout.GetComponent<VerticalLayoutGroup>();if(v!=null){v.spacing=14;v.childControlHeight=v.childControlWidth=true;v.childForceExpandHeight=v.childForceExpandWidth=false;}
        foreach(var b in layout.GetComponentsInChildren<UnityEngine.UI.Button>(true)){var le=b.GetComponent<LayoutElement>();if(le!=null){le.preferredWidth=365;le.preferredHeight=64;le.minHeight=64;}Button(b.transform,b.name=="btn_gamestart",34);var own=b.GetComponent<Image>();if(own!=null&&b.transform.GetComponentsInChildren<UISurfacePart>().Length>0)own.color=Color.clear;}
        Place(Find(t,"ArtFrame/ApprovedLogo"),TL,120,-115,470,300,TL);Off(layout,"btn_gamestart/SelectionArrow");
    }
    static void Rooms(GameObject g)
    {
        FixedFonts(g);var t=g.transform;var clip=Find(t,"Panel/RoomClipboard");Place(clip,TL,72,-62,790,805,TL);var face=Surface(clip);if(face!=null)face.color=Color.clear;
        var title=Find(clip,"Label");Place(title,TL,18,0,650,125,TL);Text(title,88);Off(clip,"ClipboardBrand");
        var view=g.GetComponent<UIRoomListScreen>();var status=Ref<TMP_Text>(view,"status");Place(status.transform,TL,22,-128,700,48,TL);Text(status.transform,29);
        var scroll=Find(clip,"RoomsScroll");Place(scroll,TL,0,-225,750,565,TL);var empty=Find(clip,"EmptyState");Place(empty,TL,0,-225,750,565,TL);
        var info=Find(t,"Panel/RoomDetails");Place(info,TL,885,-275,480,568,TL);Surface(info);
        Place(Find(info,"RoomPreview"),TC,0,-23,432,240,TC);Place(Find(info,"SelectedRoom"),TC,0,-280,425,65,TC);Text(Find(info,"SelectedRoom"),39);
        Place(Find(info,"TeamCount"),BC,0,73,400,77,BC);Place(Find(info,"Help"),BC,0,24,420,35,BC);Text(Find(info,"Help"),23,TextAlignmentOptions.Center);
        Off(info,"OccupancyIcon");Off(info,"Occupancy");Place(Find(info,"TeamsCaption"),TL,28,-355,220,40,TL);Text(Find(info,"TeamsCaption"),27);
        var actions=Find(t,"Panel/Actions");var refresh=Ref<UnityEngine.UI.Button>(view,"refreshButton");var create=Ref<UnityEngine.UI.Button>(view,"createButton");var join=Ref<UnityEngine.UI.Button>(view,"joinButton");var back=Ref<UnityEngine.UI.Button>(view,"backButton");
        Place(back.transform,new(0,0),64,64,190,66,new(0,0));Place(refresh.transform,new(0,0),280,64,260,78,new(0,0));Place(create.transform,new(0,0),565,64,260,78,new(0,0));Place(join.transform,new(1,0),-65,64,380,96,new(1,0));
        foreach(var b in actions.GetComponentsInChildren<UnityEngine.UI.Button>(true)){Button(b.transform,b==join,b==join?44:32);Off(b.transform,"ActionIcon");Off(b.transform,"JoinArrow");}
    }
    static void Settings(GameObject g)
    {
        FixedFonts(g);var t=g.transform;var panel=Find(t,"Panel");Place(panel,C,0,0,960,760,C);Surface(panel);
        var spine=Find(panel,"RedSpine");Free(spine);Place(spine,TL,55,-34,500,100,TL);var heading=Find(spine,"Title");Stretch(heading);var label=Text(heading,65);label.text="설정";
        Place(Find(panel,"Volumes"),TL,64,-164,830,440,TL);var volumes=Find(panel,"Volumes");var layout=volumes.GetComponent<VerticalLayoutGroup>();if(layout!=null){layout.spacing=14;layout.childControlHeight=true;layout.childForceExpandHeight=false;}
        foreach(var row in volumes.Cast<Transform>()){
            var le=row.GetComponent<LayoutElement>();if(le!=null){le.minHeight=le.preferredHeight=70;}
            var l=row.Find("Label");if(l!=null){Place(l,new(0,.5f),0,0,210,60,new(0,.5f));Text(l,29);}
        }
        Place(Find(panel,"Apply"),BC,0,42,470,80,BC);Button(Find(panel,"Apply"),true,36);Place(Find(panel,"Close"),TR,-25,-25,52,52,TR);Button(Find(panel,"Close"),false,26);
    }
    static void Crew(GameObject g)
    {
        FixedFonts(g);var t=g.transform;Place(Find(t,"Stage/Caption"),TL,65,-55,950,135,TL);Text(Find(t,"Stage/Caption"),93);Place(Find(t,"Stage/CrewSubtitle"),TL,72,-195,930,60,TL);Text(Find(t,"Stage/CrewSubtitle"),29);Off(t,"Stage/Brand");Off(t,"Stage/SelectedCrewName");Off(t,"Stage/SelectedCrewCaptionPlate");
        Place(Find(t,"Stage/CrewRoster"),TR,-55,-122,635,345,TR);var grid=Find(t,"Stage/CrewRoster").GetComponent<GridLayoutGroup>();if(grid!=null){grid.cellSize=new(141,148);grid.spacing=new(12,16);grid.padding=new RectOffset(15,15,15,15);}
        Place(Find(t,"Stage/PanelWrap"),TR,-55,-525,635,345,TR);var detail=Find(t,"Stage/PanelWrap/Detail");Surface(detail);
        var view=g.GetComponent<UICharacterSelectScreen>();var name=Ref<TMP_Text>(view,"selectedName");if(name!=null){Place(name.transform,TL,28,-15,560,65,TL);Text(name.transform,43);}
        foreach(var caption in new[]{"SelectedCaption","RegistrationCaption","TeamCaption"})Off(detail,caption);
        foreach(var rule in detail.Cast<Transform>().Where(x=>x.name=="Rule"))rule.gameObject.SetActive(false);
        var groups=new[]{"DayGroup","NightGroup"};for(int i=0;i<groups.Length;i++){
            var group=Find(detail,groups[i]);Free(group);Place(group,TL,25,-90-i*95,585,85,TL);
            foreach(var child in group.GetComponentsInChildren<TMP_Text>(true)){
                var isEffect=child.name.EndsWith("Effect");var isName=child.name.EndsWith("Name");
                if(isName){Place(child.transform,TL,92,0,475,36,TL);Text(child.transform,29);}
                else if(isEffect){Place(child.transform,TL,92,-38,475,45,TL);Text(child.transform,22);child.textWrappingMode=TextWrappingModes.Normal;}
                else if(child.name.EndsWith("Caption")){Place(child.transform,TL,0,0,76,36,TL);Text(child.transform,25);child.text=i==0?"낮":"밤";}
            }
            Off(group,"PhaseLabel");
        }
        Place(Find(detail,"Nameplate"),BC,0,15,585,52,BC);
        Place(Find(t,"Stage/Cards"),BC,-90,42,625,170,BC);var cards=Find(t,"Stage/Cards").GetComponent<GridLayoutGroup>();if(cards!=null){cards.cellSize=new(141,164);cards.spacing=new(16,0);cards.padding=new RectOffset(0,0,0,0);}
        Off(t,"Stage/Cards/UISurfacePart");Place(Find(t,"Stage/Confirm"),new(1,0),-55,42,285,80,new(1,0));Button(Find(t,"Stage/Confirm"),true,35);Off(t,"Stage/Confirm/ServiceBell");
        Place(Find(t,"Stage/Back"),new(0,0),55,42,190,60,new(0,0));Button(Find(t,"Stage/Back"),false,28);
        var wait=Find(t,"Stage/GameObject");Place(wait,TR,-60,-50,360,52,TR);Surface(wait);
    }
    static void Settlement(GameObject g)
    {
        FixedFonts(g);var t=g.transform;Off(t,"Stage/SettlementBrand");var summary=Find(t,"Stage/Cards/SummaryCard");Place(summary,TL,80,-160,700,540,TL);Place(Find(summary,"Title"),TL,30,-23,630,82,TL);Text(Find(summary,"Title"),58);Place(Ref<TMP_Text>(g.GetComponent<UIDaySettlementScreen>(),"dayHeading").transform,TL,33,-108,630,45,TL);Text(Ref<TMP_Text>(g.GetComponent<UIDaySettlementScreen>(),"dayHeading").transform,30);
        var labels=new[]{"Sales","Rent","Payment","Streak"};for(int i=0;i<labels.Length;i++){var line=Find(summary,labels[i]);Place(line,TL,35,-180-i*77,630,70,TL);Off(line,"Coins");var val=line.Find("Value");if(val!=null)Text(val,labels[i]=="Sales"?61:40,TextAlignmentOptions.Right);}
        var standings=Find(t,"Stage/Cards/StandingsCard");Place(standings,TR,-80,-160,995,420,TR);var title=Find(standings,"Title");Place(title,TL,30,-20,900,60,TL);Text(title,39);Off(standings,"ReferenceHeading");
        var forecast=Find(t,"Stage/Cards/ForecastCard");Place(forecast,TR,-80,-620,995,300,TR);Place(Find(forecast,"Title"),TL,30,-18,920,55,TL);Text(Find(forecast,"Title"),36);Place(Find(forecast,"CompositionBar"),TL,35,-95,925,55,TL);Place(Find(forecast,"Popular"),TL,35,-185,925,75,TL);
        var upgrade=Find(t,"Stage/Cards/UpgradeCard");Place(upgrade,TL,80,-740,700,180,TL);Off(upgrade,"ReferenceHeading");Place(Find(upgrade,"Title"),TL,25,-12,640,43,TL);Place(Find(upgrade,"Gems"),TL,25,-62,650,95,TL);
    }
    static void Final(GameObject g)
    {
        FixedFonts(g);var t=g.transform;Place(Find(t,"Stage/FinalHeading"),TL,85,-100,900,145,TL);Text(Find(t,"Stage/FinalHeading"),100);Place(Find(t,"Stage/Title"),TL,90,-255,780,110,TL);Text(Find(t,"Stage/Title"),72);var ranking=Find(t,"Stage/FinalRanking");Place(ranking,TR,-70,-150,910,720,TR);Off(ranking,"ReferenceHeading");Place(Find(ranking,"Caption"),TL,30,-15,800,60,TL);Text(Find(ranking,"Caption"),43);Place(Find(t,"Stage/Lobby"),new(1,0),-70,65,400,85,new(1,0));Button(Find(t,"Stage/Lobby"),true,39);
    }
    static void Hud(GameObject g)
    {
        var t=g.transform;
        foreach(var n in new[]{"DayRoot/DayHeader/ClockBadge/AlarmClock","DayRoot/DayHeader/ClockBadge/Body","DayRoot/DayHeader/ClockBadge/TimeGauge"}){var r=t.Find(n);if(r!=null){var im=r.GetComponent<Image>();if(im!=null)im.enabled=false;}}
        // 실제 후방 카메라는 유지하고 HUD의 여백만 정리한다.
        Place(Find(t,"DayRoot/DayHeader/TeamStandings"),TL,25,-25,262,226,TL);
        Surface(Find(t,"DayRoot/DayHeader/ClockBadge"));Surface(Find(t,"NightRoot/TopCenter"));
    }
}
