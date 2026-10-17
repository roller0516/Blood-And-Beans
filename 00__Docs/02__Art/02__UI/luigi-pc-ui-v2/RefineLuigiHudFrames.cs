using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D;
using UnityEditor;
using UnityEditor.U2D;
using UnityEditor.U2D.Sprites;
using TMPro;
using Object=UnityEngine.Object;

/// 시안의 외곽 형태를 가진 실제 공용 스프라이트. 게임 데이터/카메라/입력은 건드리지 않는다.
public static class RefineLuigiHudFrames
{
    const string Sheet="Assets/Art/UI/Sprites/Common/LuigiHudTrim.png";
    const string Parts="Assets/Art/UI/Prefabs/Parts/";
    static readonly Vector2 C=new Vector2(.5f,.5f), TL=new Vector2(0,1), TC=new Vector2(.5f,1), TR=new Vector2(1,1), BC=new Vector2(.5f,0);
    static Sprite[] sprites;
    public static string Main()
    {
        Slice();sprites=AssetDatabase.LoadAllAssetsAtPath(Sheet).OfType<Sprite>().ToArray();
        Edit(Parts+"UISkillSlot.prefab",Skill);
        Edit("Assets/Art/UI/Prefabs/Screen/UIMatchHudScreen.prefab",Hud);
        const string atlasPath="Assets/Art/UI/Atlases/LuigiCommon.spriteatlasv2";
        var atlas=SpriteAtlasAsset.Load(atlasPath);
        var packables=AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath).GetPackables();
        var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(Sheet);
        if(!packables.Contains(tex)){atlas.Add(new Object[]{tex});SpriteAtlasAsset.Save(atlas,atlasPath);AssetDatabase.ImportAsset(atlasPath,ImportAssetOptions.ForceSynchronousImport);}
        AssetDatabase.SaveAssets();return "Applied bronze timer / sales / ranking / ability frames to production HUD; four stable-ID sprites added to shared Atlas.";
    }
    static Sprite S(string name)=>sprites.First(x=>x.name==name);
    static void Slice()
    {
        AssetDatabase.ImportAsset(Sheet,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(Sheet);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit=100;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
        importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;
        importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
        var factories=new SpriteDataProviderFactories();factories.Init();
        var provider=factories.GetSpriteEditorDataProviderFromObject(importer);
        if(provider==null)throw new InvalidOperationException("Sprite metadata provider missing");
        provider.InitSpriteEditorDataProvider();var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();
        if(capability==null)throw new InvalidOperationException("Sprite edit capability missing");
        foreach(var required in new[]{EEditCapability.CreateAndDeleteSprite,EEditCapability.EditSpriteRect,EEditCapability.EditBorder,EEditCapability.EditPivot})
            if(!capability.GetEditCapability().HasCapability(required))throw new InvalidOperationException("Unsupported sprite operation: "+required);
        var tex=new Texture2D(2,2,TextureFormat.RGBA32,false);tex.LoadImage(File.ReadAllBytes(Sheet));var px=tex.GetPixels32();
        var names=new[]{"LuigiTimerPlaque","LuigiRowPlaque","LuigiSalesPlaque","LuigiAbilityBezel"};
        var cells=new[]{new RectInt(0,0,870,400),new RectInt(870,0,tex.width-870,340),new RectInt(0,480,990,420),new RectInt(990,315,tex.width-990,625)};
        var previous=provider.GetSpriteRects();var result=new List<SpriteRect>();
        for(int i=0;i<names.Length;i++)
        {
            var cell=cells[i];int minX=tex.width,minY=tex.height,maxX=0,maxY=0;
            for(int y=cell.y;y<Mathf.Min(cell.yMax,tex.height);y++)for(int x=cell.x;x<Mathf.Min(cell.xMax,tex.width);x++)
                if(px[(tex.height-1-y)*tex.width+x].a>=48){minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);}
            if(maxX<=minX||maxY<=minY)throw new InvalidOperationException("Empty sprite "+names[i]);
            minX=Mathf.Max(cell.x,minX-3);minY=Mathf.Max(cell.y,minY-3);maxX=Mathf.Min(tex.width-1,maxX+3);maxY=Mathf.Min(tex.height-1,maxY+3);
            var old=previous.FirstOrDefault(x=>x.name==names[i]);
            result.Add(new SpriteRect{name=names[i],spriteID=old!=null?old.spriteID:GUID.Generate(),alignment=SpriteAlignment.Center,pivot=C,
                rect=new Rect(minX,tex.height-maxY-1,maxX-minX+1,maxY-minY+1),border=i==1?new Vector4(55,48,55,48):Vector4.zero});
        }
        Object.DestroyImmediate(tex);provider.SetSpriteRects(result.ToArray());
        var fileIds=provider.GetDataProvider<ISpriteNameFileIdDataProvider>();if(fileIds==null)throw new InvalidOperationException("Sprite file-ID provider missing");
        fileIds.SetNameFileIdPairs(result.Select(x=>new SpriteNameFileIdPair(x.name,x.spriteID)));provider.Apply();importer.SaveAndReimport();
    }
    static void Edit(string path,Action<GameObject> edit){var root=PrefabUtility.LoadPrefabContents(path);try{edit(root);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}}
    static Transform F(Transform t,string path){var r=t.Find(path);if(r==null)throw new Exception(t.name+" missing "+path);return r;}
    static void Place(Transform t,Vector2 anchor,float x,float y,float w,float h){var r=(RectTransform)t;r.anchorMin=r.anchorMax=anchor;r.pivot=anchor;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);}
    static void Center(Transform t,float x,float y,float w,float h){Place(t,C,x,y,w,h);}
    static void Stretch(Transform t){var r=(RectTransform)t;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    static Image Trim(Transform parent,string sprite,bool sliced=false)
    {
        var child=parent.Find("LuigiSurface");
        if(child==null){var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Parts+"UISurfacePartGreen.prefab"));go.name="LuigiSurface";go.transform.SetParent(parent,false);child=go.transform;}
        child.gameObject.SetActive(true);Stretch(child);child.SetAsFirstSibling();
        var img=child.GetComponent<Image>();img.sprite=S(sprite);img.type=sliced?Image.Type.Sliced:Image.Type.Simple;img.color=Color.white;img.raycastTarget=false;img.fillCenter=true;img.pixelsPerUnitMultiplier=3;
        var rootImage=parent.GetComponent<Image>();if(rootImage!=null){rootImage.color=Color.clear;rootImage.material=null;}
        return img;
    }
    static void Text(Transform t,float size,string role="Title",TextAlignmentOptions alignment=TextAlignmentOptions.Center)
    {
        var label=t.GetComponent<TMP_Text>();label.fontSize=size;label.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/Luigi"+role+" SDF.asset");label.fontSharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Fonts/Luigi"+role+"Light.mat");label.alignment=alignment;label.color=Color.white;
        label.enableVertexGradient=true;var a=new Color(1,.985f,.89f);var b=new Color(1,.84f,.48f);label.colorGradient=new VertexGradient(a,a,b,b);label.extraPadding=true;
    }
    static void Skill(GameObject root)
    {
        var t=root.transform;var rootRect=(RectTransform)t;
        rootRect.sizeDelta=rootRect.anchorMin!=rootRect.anchorMax?Vector2.zero:new Vector2(108,132);
        var layout=t.GetComponent<LayoutElement>();layout.minWidth=layout.preferredWidth=108;layout.minHeight=layout.preferredHeight=132;
        var circle=F(t,"Circle");var fitter=circle.GetComponent<AspectRatioFitter>();if(fitter!=null)fitter.enabled=false;Center(circle,0,7,55,55);
        var back=F(circle,"Back").GetComponent<Image>();back.color=new Color(.035f,.07f,.09f,.97f);Stretch(F(circle,"Icon"));
        var frame=Trim(t,"LuigiAbilityBezel");frame.transform.SetAsLastSibling();
        var key=F(t,"KeyBadge");Center(key,0,-48,52,27);var keyImage=key.GetComponent<Image>();if(keyImage!=null)keyImage.color=Color.clear;
        foreach(var layoutGroup in key.GetComponents<LayoutGroup>())layoutGroup.enabled=false;
        var keyFitter=key.GetComponent<ContentSizeFitter>();if(keyFitter!=null)keyFitter.enabled=false;
        var old=key.Find("LuigiSurface");if(old!=null)old.gameObject.SetActive(false);key.SetAsLastSibling();Text(F(key,"Key"),26,"Number");
        Stretch(F(key,"Key"));
        var keyText=F(key,"Key").GetComponent<TMP_Text>();keyText.enableAutoSizing=true;keyText.fontSizeMin=13;keyText.fontSizeMax=26;
        var count=F(circle,"Count");Text(count,25,"Number");count.SetAsLastSibling();
    }
    static void Hud(GameObject root)
    {
        var t=root.transform;var header=F(t,"DayHeader");var clock=F(header,"ClockBadge");
        Place(clock,TC,0,-26,410,100);Trim(clock,"LuigiTimerPlaque");
        Center(F(clock,"Clock"),10,0,170,65);Text(F(clock,"Clock"),56,"Number");
        Center(F(clock,"DayLabel"),131,16,75,29);Text(F(clock,"DayLabel"),20);
        Center(F(clock,"DayCounter"),131,-14,85,32);Text(F(clock,"DayCounter"),26,"Number");
        F(clock,"TimeGauge").gameObject.SetActive(false);
        var sales=F(header,"SalesBlock");Place(sales,TR,-28,-28,340,98);Trim(sales,"LuigiSalesPlaque");F(sales,"Coin").gameObject.SetActive(false);
        Center(F(sales,"Revenue"),-8,1,118,58);Text(F(sales,"Revenue"),42,"Number",TextAlignmentOptions.Right);
        Center(F(sales,"GoalLabel"),61,1,25,44);F(sales,"GoalLabel").GetComponent<TMP_Text>().text="/";Text(F(sales,"GoalLabel"),34,"Number");
        Center(F(sales,"Goal"),113,1,79,54);Text(F(sales,"Goal"),36,"Number",TextAlignmentOptions.Left);
        var caption=F(sales,"Label");Center(caption,28,-52,185,32);caption.GetComponent<TMP_Text>().text="매출 목표";Text(caption,21);
        var covered=caption.Find("LuigiSurface");if(covered!=null)covered.gameObject.SetActive(false);
        var captionFrame=sales.Find("SalesCaptionPlate");
        if(captionFrame==null){var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Parts+"UISurfacePartGreen.prefab"));go.name="SalesCaptionPlate";go.transform.SetParent(sales,false);captionFrame=go.transform;}
        Center(captionFrame,28,-52,185,32);captionFrame.SetAsFirstSibling();var captionImage=captionFrame.GetComponent<Image>();captionImage.sprite=S("LuigiRowPlaque");captionImage.type=Image.Type.Sliced;captionImage.pixelsPerUnitMultiplier=3;
        Place(F(sales,"RentMet"),BC,0,-65,160,28);
        var standings=F(header,"TeamStandings");Place(standings,TL,28,-28,264,231);
        var list=standings.GetComponent<VerticalLayoutGroup>();list.padding=new RectOffset(11,11,11,11);list.spacing=3;
        Trim(standings,"LuigiRowPlaque",true);var outer=standings.Find("LuigiSurface").GetComponent<LayoutElement>();outer.ignoreLayout=true;
        foreach(var row in standings.GetComponentsInChildren<UIStandingRow>(true))
        {
            var rt=row.transform;var element=rt.GetComponent<LayoutElement>();element.minHeight=element.preferredHeight=49;element.minWidth=element.preferredWidth=242;
            var rim=rt.Find("LuigiSurface");if(rim!=null)rim.gameObject.SetActive(false);
            var bg=F(rt,"Background").GetComponent<Image>();bg.sprite=S("LuigiRowPlaque");bg.type=Image.Type.Sliced;bg.pixelsPerUnitMultiplier=5;
            Center(F(rt,"BadgeEdge"),-99,0,32,32);Text(F(rt,"BadgeEdge/Badge/Rank"),22,"Number");
            Center(F(rt,"Cafe"),-40,0,83,35);Text(F(rt,"Cafe"),23,"Title",TextAlignmentOptions.Left);
            Center(F(rt,"Total"),73,0,85,35);Text(F(rt,"Total"),27,"Number",TextAlignmentOptions.Right);
        }
        var bottom=F(t,"BottomLeft");Place(bottom,new Vector2(0,0),24,24,240,132);
        foreach(var slot in bottom.GetComponentsInChildren<UISkillSlot>(true))
        {
            Skill(slot.gameObject);
            var e=slot.GetComponent<LayoutElement>();e.minWidth=e.preferredWidth=108;e.minHeight=e.preferredHeight=132;
        }
        var recipe=F(t,"RecipeTab");Trim(recipe,"LuigiRowPlaque",true);Place(recipe,new Vector2(1,0),-28,28,187,58);
        foreach(var layout in recipe.GetComponents<LayoutGroup>())layout.enabled=false;
        Center(F(recipe,"Glyph"),-58,0,46,38);Center(F(recipe,"Caption"),25,0,105,38);
        Text(F(recipe,"Glyph"),26,"Number");Text(F(recipe,"Caption"),24);
        foreach(var p in new[]{"TopLeft","TopCenter","Prompt"})Trim(F(t,p),"LuigiRowPlaque",true);
        var so=new SerializedObject(root.GetComponent<UIMatchHudScreen>());
        foreach(var n in new[]{"rentMetColor","rentDefaultColor"}){var field=so.FindProperty(n);if(field!=null)field.colorValue=Color.white;}
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
