using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.U2D;
using UnityEditor.U2D.Sprites;
using UnityEngine.U2D;
using UnityEngine.TextCore.LowLevel;
using TMPro;
using Object = UnityEngine.Object;

/// 승인한 시안을 실제 공통 Sprite·TMP·프리팹 리소스로 준비하는 재현 레시피.
public static class BuildLuigiProductionAssets
{
    const string Common="Assets/Art/UI/Sprites/Common/";
    const string Parts="Assets/Art/UI/Prefabs/Parts/";
    const string Fonts="Assets/Art/Fonts/";
    const string Review="Assets/Art/UI/PrototypeKit/";
    public static string Main()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Slice(Common+"LuigiPanel.png",new[]{"LuigiPanel"},new[]{new RectInt(0,0,1254,1254)},new Vector4(150,150,150,150));
        // ponytail: 2026-10-18 원본 시트의 상태별 영역. 외형 확정값은 Sprite 메타데이터에 저장한다.
        Slice(Common+"LuigiControls.png",new[]{"LuigiGreen","LuigiSelected","LuigiRed","LuigiCream","LuigiDisabled","LuigiHeader"},
            new[]{new RectInt(0,0,768,340),new RectInt(768,0,768,340),new RectInt(0,350,768,300),new RectInt(768,350,768,300),new RectInt(0,680,768,344),new RectInt(768,630,768,394)},new Vector4(110,85,110,85));
        var bag=Read(Common+"LuigiBagStates.png");int cell=bag.width/3;int height=bag.height;Object.DestroyImmediate(bag);
        Slice(Common+"LuigiBagStates.png",new[]{"LuigiBagClosed","LuigiBagOpen","LuigiBagBuried"},
            new[]{new RectInt(0,0,cell,height),new RectInt(cell,0,cell,height),new RectInt(cell*2,0,cell,height)},Vector4.zero);
        ImportBackdrop();
        var ascii=new string(Enumerable.Range(32,95).Select(x=>(char)x).ToArray())+"…▲▼◀▶✓×→·";
        var gameText=string.Join("",Directory.GetFiles("Assets/Scripts/Client/UI","*.cs").Select(File.ReadAllText));
        gameText+="최종결과하루정산오늘의크루함께밤대담하게커피디저트조합법참가자방목록준비완료자세히주문제조좋음완벽실패만드는중서빙가능조리진행탐색귀환성공가방분실무게남은시간접속대기열음량설정뒤로게임시작종료블러드빈우유얼음크림초콜렛아몬드베리원두빵";
        var characters=new string((ascii+gameText).Where(c=>c>=32 && (c<127 || c>=0xAC00&&c<=0xD7A3 || "…▲▼◀▶✓×→·".Contains(c))).Distinct().ToArray());
        var title=Font("Title",Review+"Cafe24Ssurround-v2.0.ttf",characters,50,5,2048,false);
        var body=Font("Body",Review+"DoHyeon-Regular.ttf",characters,50,5,2048,false);
        var number=Font("Number",Review+"LilitaOne-Regular.ttf",ascii,80,8,512,false);
        var titleFallback=Font("TitleFallback",Review+"Cafe24Ssurround-v2.0.ttf","",50,5,1024,true);
        var bodyFallback=Font("BodyFallback",Review+"DoHyeon-Regular.ttf","",50,5,1024,true);
        title.fallbackFontAssetTable=new List<TMP_FontAsset>{titleFallback};
        body.fallbackFontAssetTable=new List<TMP_FontAsset>{bodyFallback};
        number.fallbackFontAssetTable=new List<TMP_FontAsset>{title,body};
        foreach(var f in new[]{title,body,number})EditorUtility.SetDirty(f);
        MaterialPreset(title,"TitleLight",true);MaterialPreset(title,"TitleInk",false);
        MaterialPreset(body,"BodyLight",false);MaterialPreset(body,"BodyInk",false);
        MaterialPreset(number,"NumberLight",true);MaterialPreset(number,"NumberInk",false);
        BuildSurfaces();BuildRosterCard();BuildAtlas();
        AssetDatabase.SaveAssets();
        return "Prepared 10 sprites, static primary/dynamic fallback fonts, shared surface variants and roster card.";
    }

    static Texture2D Read(string path){var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);texture.LoadImage(File.ReadAllBytes(path));return texture;}
    static void Slice(string path,string[] names,RectInt[] cells,Vector4 border)
    {
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit=100;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
        importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=4096;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        var factories=new SpriteDataProviderFactories();factories.Init();
        var provider=factories.GetSpriteEditorDataProviderFromObject(importer);
        if(provider==null)throw new InvalidOperationException("Sprite provider missing: "+path);
        provider.InitSpriteEditorDataProvider();
        var capability=provider.GetDataProvider<ISpriteFrameEditCapability>();
        if(capability==null)throw new InvalidOperationException("Sprite capability missing: "+path);
        foreach(var required in new[]{EEditCapability.CreateAndDeleteSprite,EEditCapability.EditSpriteRect,EEditCapability.EditBorder,EEditCapability.EditPivot})
            if(!capability.GetEditCapability().HasCapability(required))throw new InvalidOperationException("Sprite operation unsupported: "+required);
        var texture=Read(path);var pixels=texture.GetPixels32();var previous=provider.GetSpriteRects();var result=new List<SpriteRect>();
        for(int i=0;i<names.Length;i++)
        {
            var cell=cells[i];int minX=texture.width,minY=texture.height,maxX=0,maxY=0;
            for(int y=Mathf.Max(0,cell.y);y<Mathf.Min(texture.height,cell.yMax);y++)
                for(int x=Mathf.Max(0,cell.x);x<Mathf.Min(texture.width,cell.xMax);x++)
                    if(pixels[(texture.height-1-y)*texture.width+x].a>=48){minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);}
            if(maxX<=minX||maxY<=minY)throw new InvalidOperationException("Empty sprite cell: "+names[i]);
            minX=Mathf.Max(cell.x,minX-2);minY=Mathf.Max(cell.y,minY-2);maxX=Mathf.Min(cell.xMax-1,maxX+2);maxY=Mathf.Min(cell.yMax-1,maxY+2);
            var old=previous.FirstOrDefault(x=>x.name==names[i]);
            var rect=new SpriteRect{name=names[i],spriteID=old!=null?old.spriteID:GUID.Generate(),alignment=SpriteAlignment.Center,pivot=new Vector2(.5f,.5f),
                rect=new Rect(minX,texture.height-maxY-1,maxX-minX+1,maxY-minY+1),border=border};
            result.Add(rect);
        }
        Object.DestroyImmediate(texture);
        provider.SetSpriteRects(result.ToArray());
        var idProvider=provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if(idProvider==null)throw new InvalidOperationException("Sprite file-id provider missing: "+path);
        idProvider.SetNameFileIdPairs(result.Select(x=>new SpriteNameFileIdPair(x.name,x.spriteID)));
        provider.Apply();importer.SaveAndReimport();
    }
    static void ImportBackdrop()
    {
        const string path="Assets/Art/UI/Sprites/Title/LuigiTitleBackdrop.png";
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.mipmapEnabled=false;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
    }
    static TMP_FontAsset Font(string name,string path,string chars,int sampling,int padding,int atlas,bool dynamic)
    {
        string target=Fonts+"Luigi"+name+" SDF.asset";
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(target);if(font!=null)return font;
        var source=AssetDatabase.LoadAssetAtPath<UnityEngine.Font>(path);if(source==null)throw new InvalidOperationException("Missing source font: "+path);
        font=TMP_FontAsset.CreateFontAsset(source,sampling,padding,GlyphRenderMode.SDFAA,atlas,atlas,AtlasPopulationMode.Dynamic,dynamic);
        font.name="Luigi "+name;if(!dynamic){font.TryAddCharacters(chars);font.atlasPopulationMode=AtlasPopulationMode.Static;}
        AssetDatabase.CreateAsset(font,target);
        foreach(var texture in font.atlasTextures){texture.name=name+" Atlas";AssetDatabase.AddObjectToAsset(texture,font);}
        font.material.name=name+" Base";AssetDatabase.AddObjectToAsset(font.material,font);
        var so=new SerializedObject(font);so.FindProperty("m_ClearDynamicDataOnBuild").boolValue=dynamic;so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(font);return font;
    }
    static void MaterialPreset(TMP_FontAsset font,string name,bool dimensional)
    {
        string path=Fonts+"Luigi"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(font.material);AssetDatabase.CreateAsset(material,path);}
        material.CopyPropertiesFromMaterial(font.material);
        material.SetColor("_FaceColor",Color.white);material.SetFloat("_OutlineWidth",dimensional?.12f:0f);
        material.SetColor("_OutlineColor",new Color(.12f,.075f,.035f));
        if(dimensional){material.EnableKeyword("OUTLINE_ON");material.EnableKeyword("UNDERLAY_ON");material.SetColor("_UnderlayColor",new Color(.35f,.18f,.045f));material.SetFloat("_UnderlayOffsetY",-.25f);material.SetFloat("_UnderlaySoftness",.025f);}
        EditorUtility.SetDirty(material);
    }
    public static Sprite Sprite(string name)
    {
        foreach(var source in new[]{"LuigiPanel.png","LuigiControls.png","LuigiBagStates.png"})
        {var sprite=AssetDatabase.LoadAllAssetsAtPath(Common+source).OfType<Sprite>().FirstOrDefault(x=>x.name==name);if(sprite!=null)return sprite;}
        throw new InvalidOperationException("Missing prepared sprite: "+name);
    }
    static void Wire(Object owner,string field,Object target){var so=new SerializedObject(owner);var p=so.FindProperty(field);if(p==null)throw new Exception("Missing field "+field);p.objectReferenceValue=target;so.ApplyModifiedPropertiesWithoutUndo();}
    static void BuildSurfaces()
    {
        string basePath=Parts+"UISurfacePart.prefab";
        var root=new GameObject("UISurfacePart",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.LayoutElement),typeof(UISurfacePart));
        ((RectTransform)root.transform).sizeDelta=new Vector2(320,80);
        var image=root.GetComponent<UnityEngine.UI.Image>();image.sprite=Sprite("LuigiGreen");image.type=UnityEngine.UI.Image.Type.Sliced;image.pixelsPerUnitMultiplier=3;image.raycastTarget=false;
        Wire(root.GetComponent<UISurfacePart>(),"image",image);PrefabUtility.SaveAsPrefabAsset(root,basePath);Object.DestroyImmediate(root);
        foreach(var pair in new[]{new[]{"Paper","LuigiPanel"},new[]{"Green","LuigiGreen"},new[]{"Selected","LuigiSelected"},new[]{"Red","LuigiRed"},new[]{"Cream","LuigiCream"},new[]{"Disabled","LuigiDisabled"},new[]{"Header","LuigiHeader"}})
        {
            root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(basePath));root.name="UISurfacePart"+pair[0];
            image=root.GetComponent<UnityEngine.UI.Image>();image.sprite=Sprite(pair[1]);
            if(pair[0]=="Paper"){((RectTransform)root.transform).sizeDelta=new Vector2(600,700);image.pixelsPerUnitMultiplier=3;}
            PrefabUtility.SaveAsPrefabAsset(root,Parts+root.name+".prefab");Object.DestroyImmediate(root);
        }
    }
    static RectTransform Rect(Transform parent,string name,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax)
    {
        var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);var rect=(RectTransform)g.transform;
        rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=offsetMin;rect.offsetMax=offsetMax;return rect;
    }
    static TMP_Text Label(Transform parent,string name,string value,Vector2 min,Vector2 max,Vector2 lower,Vector2 upper,float size)
    {
        var r=Rect(parent,name,min,max,lower,upper);var t=r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Fonts+"LuigiTitle SDF.asset");t.fontSharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Fonts+"LuigiTitleLight.mat");
        t.text=value;t.fontSize=size;t.color=new Color(.98f,.92f,.76f);t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;return t;
    }
    static void BuildRosterCard()
    {
        var root=new GameObject("UICrewRosterCard",typeof(RectTransform),typeof(UnityEngine.UI.LayoutElement),typeof(UICrewRosterCard));
        ((RectTransform)root.transform).sizeDelta=new Vector2(145,178);
        var background=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Parts+"UISurfacePartGreen.prefab"));
        background.transform.SetParent(root.transform,false);var bg=(RectTransform)background.transform;bg.anchorMin=Vector2.zero;bg.anchorMax=Vector2.one;bg.offsetMin=bg.offsetMax=Vector2.zero;
        background.GetComponent<UnityEngine.UI.Image>().pixelsPerUnitMultiplier=6;
        var portrait=Rect(root.transform,"Portrait",Vector2.zero,Vector2.one,new Vector2(14,37),new Vector2(-14,-14)).gameObject.AddComponent<UnityEngine.UI.Image>();portrait.preserveAspect=true;portrait.raycastTarget=false;
        var name=Label(root.transform,"PlayerName","",Vector2.zero,new Vector2(1,0),new Vector2(12,10),new Vector2(-12,37),20);
        var stripe=Rect(root.transform,"TeamStripe",new Vector2(0,0),new Vector2(1,0),new Vector2(18,4),new Vector2(-18,8)).gameObject.AddComponent<UnityEngine.UI.Image>();stripe.color=Color.green;stripe.raycastTarget=false;
        var ready=Label(root.transform,"Ready","✓",new Vector2(1,1),Vector2.one,new Vector2(-36,-40),new Vector2(-10,-14),26);ready.color=new Color(.6f,.98f,.42f);
        var self=Rect(root.transform,"SelfFrame",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero).gameObject.AddComponent<UnityEngine.UI.Image>();self.sprite=Sprite("LuigiSelected");self.type=UnityEngine.UI.Image.Type.Sliced;self.fillCenter=false;self.pixelsPerUnitMultiplier=5;self.raycastTarget=false;self.transform.SetAsLastSibling();
        var card=root.GetComponent<UICrewRosterCard>();Wire(card,"portrait",portrait);Wire(card,"teamStripe",stripe);Wire(card,"playerName",name);Wire(card,"readyMark",ready.gameObject);Wire(card,"selfMark",self.gameObject);
        PrefabUtility.SaveAsPrefabAsset(root,Parts+"UICrewRosterCard.prefab");Object.DestroyImmediate(root);
    }
    static void BuildAtlas()
    {
        const string path="Assets/Art/UI/Atlases/LuigiCommon.spriteatlasv2";
        if(!File.Exists(path))
        {
            var createdAtlas=new SpriteAtlasAsset();UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(new Object[]{createdAtlas},path,true);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        }
        var importer=AssetImporter.GetAtPath(path) as SpriteAtlasImporter;
        if(importer==null)throw new InvalidOperationException("Missing atlas importer");
        var atlasAsset=SpriteAtlasAsset.Load(path);
        var importedAtlas=AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);var old=importedAtlas.GetPackables();if(old.Length>0)atlasAsset.Remove(old);
        atlasAsset.Add(new Object[]{AssetDatabase.LoadAssetAtPath<Texture2D>(Common+"LuigiPanel.png"),AssetDatabase.LoadAssetAtPath<Texture2D>(Common+"LuigiControls.png"),AssetDatabase.LoadAssetAtPath<Texture2D>(Common+"LuigiBagStates.png")});
        SpriteAtlasAsset.Save(atlasAsset,path);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        importer=(SpriteAtlasImporter)AssetImporter.GetAtPath(path);var packing=importer.packingSettings;packing.enableRotation=false;packing.enableTightPacking=false;packing.padding=4;importer.packingSettings=packing;
        var settings=importer.textureSettings;settings.generateMipMaps=false;settings.readable=false;settings.filterMode=FilterMode.Bilinear;importer.textureSettings=settings;
        var platform=importer.GetPlatformSettings("Standalone");platform.overridden=true;platform.maxTextureSize=2048;platform.format=TextureImporterFormat.BC7;platform.textureCompression=TextureImporterCompression.CompressedHQ;importer.SetPlatformSettings(platform);
        importer.includeInBuild=true;importer.SaveAndReimport();
    }
}
