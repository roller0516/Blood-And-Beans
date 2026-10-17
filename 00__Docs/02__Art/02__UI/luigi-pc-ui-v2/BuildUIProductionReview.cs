using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// ponytail: 제작 검토용 규격. 검토 후 확정된 외형·배치를 실제 부품의 직렬화 설정으로 옮긴다.
// 게임 화면·UIThemeConfig·TMP 기본 설정·Addressables 등록은 변경하지 않는다.
public static class BuildUIProductionReview
{
    const string Kit = "Assets/Art/UI/PrototypeKit";
    static string Docs => Path.GetFullPath("../00__Docs/02__Art/02__UI/luigi-pc-ui-v2");
    static readonly Color Green = new Color(.055f,.18f,.135f), Cream = new Color(.92f,.85f,.69f), Ink = new Color(.09f,.14f,.105f);
    static Material gold, green, paper, dark, creamButton, greenButton;
    static TMP_FontAsset titleFont, bodyFont, numberFont;
    static readonly Dictionary<string,Sprite> sprites = new Dictionary<string,Sprite>();
    static Scene scene;
    static int meshIndex;

    public static string Main()
    {
        if(!AssetDatabase.IsValidFolder(Kit)) AssetDatabase.CreateFolder("Assets/Art/UI","PrototypeKit");
        scene=EditorSceneManager.NewPreviewScene();
        try
        {
            gold=Mat("Brass",new Color(.72f,.47f,.18f),.12f,.26f);
            green=Mat("Green",Green,.04f,.27f);paper=Mat("Paper",Cream,.01f,.2f);
            dark=Mat("Dark",new Color(.025f,.055f,.04f),.12f,.24f);
            creamButton=Mat("CreamButton",new Color(.92f,.77f,.46f),.12f,.32f);
            greenButton=Mat("SelectedGreen",new Color(.14f,.40f,.22f),.08f,.3f);
            var light=New("KeyLight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;light.color=new Color(1f,.89f,.72f);light.transform.rotation=Quaternion.Euler(30,-28,0);
            var fill=New("FillLight").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.35f;fill.color=new Color(1f,.95f,.84f);fill.transform.rotation=Quaternion.Euler(-25,145,0);
            var fontChars="외니모델→투명글별장독교공통부품실제제작검토조립예시같은프리팹다른구성재사용클립보드판황동금속종이배경제목내용영역선택버튼아이콘슬롯상태기본강조비활성준비완료뒤로조합법커피디저트아메리카노카페라떼크림케이크제조조리가능서빙재료완성판정정산팀매출임대료목표합계가방무게남은시간입체원형정면사선표시스프라이트머티리얼공유실제렌더링동일에셋사용검토용화면변경전확인크기세로가로테두리두께유지0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz /:+-·%!?()";
            titleFont=FontAsset("Cafe24Ssurround-v2.0.ttf","Cafe24Ssurround-OFL.txt","Title",fontChars);
            bodyFont=FontAsset("DoHyeon-Regular.ttf","dohyeon-OFL.txt","Body",fontChars);
            numberFont=FontAsset("LilitaOne-Regular.ttf","lilitaone-OFL.txt","Number","0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz /:+-%!?()");
            string fallbackPath=Kit+"/UI_Fallback SDF.asset";
            File.Copy("Assets/Art/Fonts/Pretendard-OFL.txt",Kit+"/Pretendard-OFL.txt",true);
            if(!File.Exists(fallbackPath))AssetDatabase.CopyAsset("Assets/Art/Fonts/Pretendard-Regular SDF.asset",fallbackPath);
            var fallback=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fallbackPath);fallback.atlasPopulationMode=AtlasPopulationMode.Static;EditorUtility.SetDirty(fallback);
            titleFont.fallbackFontAssetTable=new List<TMP_FontAsset>{fallback};bodyFont.fallbackFontAssetTable=new List<TMP_FontAsset>{fallback};EditorUtility.SetDirty(titleFont);EditorUtility.SetDirty(bodyFont);
            Source("Board",6f,8f,()=>{Box("BrassBevel",6,8,.24f,.28f,.065f,gold);Box("GreenFace",5.84f,7.84f,.12f,.23f,-.16f,green);});
            Source("Paper",5.4f,7.4f,()=>{Box("PaperEdge",5.4f,7.4f,.08f,.18f,0,gold);Box("CreamSheet",5.33f,7.33f,.035f,.16f,-.06f,paper);});
            Source("Clip",2.9f,1.75f,()=>{Box("BackPlate",2.55f,.62f,.18f,.12f,.02f,gold);Box("FoldedLip",2.72f,.23f,.26f,.105f,-.13f,gold,new Vector2(0,-.18f));Torus("HangingLoop",.42f,.09f,new Vector3(0,.52f,.015f),gold);Stud(-1.03f,.08f,-.13f,.085f);Stud(1.03f,.08f,-.13f,.085f);});
            Source("Header",6f,1.4f,()=>{Box("BrassBevel",6,1.4f,.19f,.27f,0,gold);Box("GreenFace",5.86f,1.25f,.08f,.22f,-.13f,green);});
            Source("Button",4.5f,1.15f,()=>{Box("BrassBevel",4.5f,1.15f,.18f,.25f,0,gold);Box("Face",4.35f,1,.07f,.2f,-.13f,creamButton);});
            Source("ButtonSelected",4.5f,1.15f,()=>{Box("BrassBevel",4.5f,1.15f,.18f,.25f,0,gold);Box("Face",4.35f,1,.07f,.2f,-.13f,greenButton);});
            Source("ButtonDisabled",4.5f,1.15f,()=>{Box("BrassBevel",4.5f,1.15f,.18f,.25f,0,dark);Box("Face",4.35f,1,.07f,.2f,-.13f,green);});
            Source("Slot",2f,2f,()=>{Box("BrassRim",2,2,.12f,.23f,0,gold);Box("CreamFace",1.90f,1.90f,.055f,.19f,-.09f,paper);});
            Source("Hud",6f,1.65f,()=>{Box("BrassRim",6,1.65f,.18f,.28f,0,gold);Box("GreenInset",5.83f,1.48f,.06f,.23f,-.13f,green);Stud(-2.72f,0,-.15f,.075f);Stud(2.72f,0,-.15f,.075f);});
            Source("Gauge",7f,1.55f,()=>{Box("BrassRim",7,1.55f,.16f,.28f,0,gold);Box("GreenInset",6.82f,1.37f,.06f,.23f,-.11f,green);Box("Track",6.44f,.70f,.05f,.30f,-.17f,dark);});
            RenderClipboardModel();
            BuildUi();
            AssetDatabase.SaveAssets();
            File.WriteAllText(Path.Combine(Docs,"ui-production-review-manifest.json"),"{\"renderer\":\"Unity 6000.5.9f1 / project URP\",\"actualAssets\":true,\"gameScreensModified\":false,\"kit\":\""+Kit+"\",\"spriteCount\":"+sprites.Count+",\"sourceModels\":10,\"renderedViews\":[\"ui-production-assembled.png\",\"ui-production-parts.png\",\"ui-production-breakdown.png\"]}");
            return "Created actual model sources, rendered sprites, shared UI prefabs and three Unity review captures at "+Kit;
        }
        finally {EditorSceneManager.ClosePreviewScene(scene);}
    }

    static GameObject modelRoot;
    static GameObject New(string name,Transform parent=null)
    {var g=new GameObject(name);if(parent!=null)g.transform.SetParent(parent,false);else SceneManager.MoveGameObjectToScene(g,scene);return g;}
    static Material Mat(string name,Color color,float metal,float smooth)
    {
        string p=Kit+"/UI_"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,p);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);EditorUtility.SetDirty(m);return m;
    }
    static TMP_FontAsset FontAsset(string file,string license,string role,string characters)
    {
        var source=Path.Combine(Docs,"fonts",file);string p=Kit+"/"+file;
        if(!File.Exists(p))File.Copy(source,p);if(!File.Exists(Kit+"/"+license))File.Copy(Path.Combine(Docs,"fonts",license),Kit+"/"+license);
        AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceSynchronousImport);
        string target=Kit+"/UI_"+role+" SDF.asset";var asset=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(target);
        if(asset!=null)return asset;
        asset=TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(p),72,8,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,false);
        asset.name="UI "+role;asset.TryAddCharacters(characters);asset.atlasPopulationMode=AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(asset,target);
        foreach(var atlas in asset.atlasTextures){atlas.name=role+" Atlas";AssetDatabase.AddObjectToAsset(atlas,asset);}
        AssetDatabase.AddObjectToAsset(asset.material,asset);
        if(role!="Body") {asset.material.SetFloat("_OutlineWidth",.10f);asset.material.SetColor("_OutlineColor",new Color(.08f,.13f,.08f));asset.material.EnableKeyword("OUTLINE_ON");asset.material.EnableKeyword("UNDERLAY_ON");asset.material.SetColor("_UnderlayColor",new Color(.26f,.17f,.06f));asset.material.SetFloat("_UnderlayOffsetY",-.22f);asset.material.SetFloat("_UnderlaySoftness",.02f);}
        EditorUtility.SetDirty(asset);return asset;
    }
    static void Source(string id,float width,float height,Action build)
    {
        modelRoot=New("UI"+id+"Model");build();
        PrefabUtility.SaveAsPrefabAsset(modelRoot,Kit+"/UI"+id+"Model.prefab");
        int pxW=id=="Board"||id=="Paper"?600:900,pxH=Mathf.RoundToInt(pxW*height/width);
        string spritePath=Kit+"/UI_"+id+".png";
        Render(modelRoot,spritePath,pxW,pxH,Mathf.Max(height/2,width/2*pxH/pxW)*1.035f,Quaternion.identity,true);
        AssetDatabase.ImportAsset(spritePath,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(spritePath);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.spritePixelsPerUnit=100;
        if(id!="Clip"){float border=id=="Slot"?135:id=="Board"||id=="Paper"?65:Mathf.Min(100,pxH*.42f);importer.spriteBorder=new Vector4(border,border,border,border);}importer.SaveAndReimport();
        sprites[id]=AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        File.Copy(spritePath,Path.Combine(Docs,"ui-part-"+id.ToLowerInvariant()+".png"),true);
        Object.DestroyImmediate(modelRoot);modelRoot=null;
    }
    static void Box(string name,float w,float h,float depth,float radius,float z,Material material,Vector2 offset=default(Vector2))
    {
        var g=New(name,modelRoot.transform);g.transform.localPosition=new Vector3(offset.x,offset.y,z);
        var mesh=Rounded(w,h,depth,radius);mesh.name=name+" "+meshIndex++;
        string p=Kit+"/Mesh_"+modelRoot.name+"_"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(p);
        if(existing==null)AssetDatabase.CreateAsset(mesh,p);else{EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}
        g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=material;
    }
    static Mesh Rounded(float w,float h,float d,float r)
    {
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();int count=36;
        for(int ring=0;ring<4;ring++)
        {
            float inset=ring==0||ring==3?Mathf.Min(.06f,d*.28f):0;
            float z=ring==0?-d/2: ring==1?-d/2+Mathf.Min(.06f,d*.28f):ring==2?d/2-Mathf.Min(.06f,d*.28f):d/2;
            for(int corner=0;corner<4;corner++)for(int j=0;j<9;j++)
            {
                float angle=(180+corner*90+j*90f/8f)*Mathf.Deg2Rad;
                float cx=(corner==0||corner==3?-1:1)*(w/2-r),cy=(corner<2?-1:1)*(h/2-r);
                var v=new Vector3(cx+Mathf.Cos(angle)*(r-inset),cy+Mathf.Sin(angle)*(r-inset),z);vertices.Add(v);uv.Add(new Vector2(v.x/w+.5f,v.y/h+.5f));
            }
        }
        for(int ring=0;ring<3;ring++)for(int i=0;i<count;i++) {int a=ring*count+i,b=ring*count+(i+1)%count,c=a+count,e=b+count;triangles.AddRange(new[]{a,c,b,b,c,e});}
        int front=vertices.Count;vertices.Add(new Vector3(0,0,-d/2));uv.Add(new Vector2(.5f,.5f));int back=vertices.Count;vertices.Add(new Vector3(0,0,d/2));uv.Add(new Vector2(.5f,.5f));
        for(int i=0;i<count;i++){int next=(i+1)%count;triangles.AddRange(new[]{front,next,i,back,3*count+i,3*count+next});}
        var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
    static void Torus(string name,float radius,float tube,Vector3 pos,Material material)
    {
        var vs=new List<Vector3>();var tris=new List<int>();int a=40,b=10;
        for(int i=0;i<a;i++)for(int j=0;j<b;j++){float x=i*Mathf.PI*2/a,y=j*Mathf.PI*2/b;vs.Add(new Vector3(Mathf.Cos(x)*(radius+Mathf.Cos(y)*tube),Mathf.Sin(x)*(radius+Mathf.Cos(y)*tube),Mathf.Sin(y)*tube));}
        for(int i=0;i<a;i++)for(int j=0;j<b;j++){int v=i*b+j,n=((i+1)%a)*b+j,p=i*b+(j+1)%b,q=((i+1)%a)*b+(j+1)%b;tris.AddRange(new[]{v,n,p,p,n,q});}
        var mesh=new Mesh();mesh.SetVertices(vs);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();string path=Kit+"/Mesh_Loop.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}
        var g=New(name,modelRoot.transform);g.transform.localPosition=pos;g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=material;
    }
    static void Stud(float x,float y,float z,float radius)
    {var g=GameObject.CreatePrimitive(PrimitiveType.Sphere);g.name="Rivet";g.transform.SetParent(modelRoot.transform,false);g.transform.localPosition=new Vector3(x,y,z);g.transform.localScale=Vector3.one*radius*2;Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<MeshRenderer>().sharedMaterial=gold;}
    static void Render(GameObject root,string path,int width,int height,float ortho,Quaternion rotation,bool alpha)
    {
        var rendererData=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
        var features=rendererData.rendererFeatures.ToArray();var featureStates=features.Select(f=>f.isActive).ToArray();
        foreach(var f in features)f.SetActive(false);
        var cam=New("RenderCamera").AddComponent<Camera>();
        var cameraData=cam.GetUniversalAdditionalCameraData();cameraData.renderPostProcessing=false;cameraData.volumeLayerMask=0;cam.scene=scene;cam.orthographic=true;cam.orthographicSize=ortho;cam.transform.position=new Vector3(0,0,-20);cam.transform.rotation=rotation;cam.nearClipPlane=.1f;cam.farClipPlane=100;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=alpha?Color.clear:new Color(.045f,.065f,.055f);cam.allowHDR=false;cam.allowMSAA=true;
        var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);rt.antiAliasing=4;rt.Create();cam.targetTexture=rt;var old=RenderTexture.active;Texture2D tex=null;
        try
        {
            Canvas.ForceUpdateCanvases();
            tex=new Texture2D(width,height,TextureFormat.RGBA32,false);
            cam.backgroundColor=alpha?Color.black:cam.backgroundColor;cam.Render();RenderTexture.active=rt;
            tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();
            if(alpha)
            {
                // URP's final blit forces opaque alpha. Recover antialiased coverage from two actual renders.
                var black=tex.GetPixels();cam.backgroundColor=Color.white;cam.Render();RenderTexture.active=rt;
                tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();var white=tex.GetPixels();
                for(int i=0;i<black.Length;i++)
                {
                    // RT readback is sRGB. Solve coverage in linear light, then store straight-alpha sRGB.
                    var b=black[i].linear;var w=white[i].linear;
                    float a=1-Mathf.Clamp01(((w.r-b.r)+(w.g-b.g)+(w.b-b.b))/3);
                    var c=a>.001f?new Color(b.r/a,b.g/a,b.b/a,1).gamma:Color.clear;c.a=a;black[i]=c;
                }
                tex.SetPixels(black);tex.Apply();
            }
            File.WriteAllBytes(path,tex.EncodeToPNG());
        }
        finally{for(int i=0;i<features.Length;i++)features[i].SetActive(featureStates[i]);RenderTexture.active=old;if(tex!=null)Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(cam.gameObject);}
    }
    static void RenderClipboardModel()
    {
        var group=New("Clipboard3DAssembly");
        foreach(var id in new[]{"Board","Paper","Clip"})
        {
            var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Kit+"/UI"+id+"Model.prefab"),scene);
            g.transform.SetParent(group.transform,false);
            g.transform.localPosition=id=="Paper"?new Vector3(0,0,-.26f):id=="Clip"?new Vector3(0,3.65f,-.46f):Vector3.zero;
        }
        PrefabUtility.SaveAsPrefabAsset(group,Kit+"/UIClipboard3DAssembly.prefab");
        group.transform.rotation=Quaternion.Euler(7,-24,-3);
        Render(group,Path.Combine(Docs,"ui-production-board-3d.png"),900,1100,5.15f,Quaternion.identity,false);
        Object.DestroyImmediate(group);
    }
    static GameObject ClipboardInstance(string name,float width,float height)
    {
        var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Kit+"/UIClipboardBase.prefab"),scene);
        g.name=name;var r=(RectTransform)g.transform;r.sizeDelta=new Vector2(width,height);
        ((RectTransform)r.Find("Board")).sizeDelta=new Vector2(width,height);
        ((RectTransform)r.Find("Paper")).sizeDelta=new Vector2(width-56,height-42);
        ((RectTransform)r.Find("Clip")).anchoredPosition=new Vector2(0,height/2-9);
        ((RectTransform)r.Find("ContentRoot")).sizeDelta=new Vector2(width-64,height-76);
        return g;
    }
    static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
    {var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);var r=g.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.sizeDelta=new Vector2(w,h);r.anchoredPosition=new Vector2(x,y);return r;}
    static UnityEngine.UI.Image Image(Transform p,string name,string sprite,float x,float y,float w,float h)
    {var r=Rect(p,name,x,y,w,h);var i=r.gameObject.AddComponent<UnityEngine.UI.Image>();i.sprite=sprites[sprite];i.type=sprite=="Clip"?UnityEngine.UI.Image.Type.Simple:UnityEngine.UI.Image.Type.Sliced;i.raycastTarget=false;return i;}
    static TMP_Text Text(Transform p,string name,string value,float x,float y,float w,float h,float size,bool title=false,bool number=false,Color? color=null)
    {var r=Rect(p,name,x,y,w,h);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=number?numberFont:title?titleFont:bodyFont;t.fontSharedMaterial=t.font.material;t.text=value;t.fontSize=size;t.enableAutoSizing=false;t.alignment=TextAlignmentOptions.Center;t.color=color??Ink;t.raycastTarget=false;t.overflowMode=TextOverflowModes.Overflow;return t;}
    static GameObject Clipboard(string name,float width,float height)
    {
        var r=Rect(null,name,0,0,width,height);SceneManager.MoveGameObjectToScene(r.gameObject,scene);
        Image(r,"Board","Board",0,0,width,height);Image(r,"Paper","Paper",0,-2,width-56,height-42);Image(r,"Clip","Clip",0,height/2-9,118,72);
        Rect(r,"ContentRoot",0,-10,width-64,height-76);return r.gameObject;
    }
    static void Header(Transform root,string title,float width,float top)
    {Image(root,"HeaderPlaque","Header",0,top,width,64);Text(root,"Title",title,0,top,width-25,58,30,true,false,Cream);}
    static void Button(Transform p,string label,string state,float x,float y,float w=180)
    {
        var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Kit+"/UIActionButton.prefab"),scene);
        g.name="Button_"+label;g.transform.SetParent(p,false);var r=(RectTransform)g.transform;
        r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,52);
        var image=g.GetComponent<UnityEngine.UI.Image>();image.sprite=sprites[state];
        var button=g.GetComponent<UnityEngine.UI.Button>();button.interactable=state!="ButtonDisabled";
        var text=g.GetComponentInChildren<TMP_Text>();text.text=label;text.color=state=="ButtonSelected"?Cream:Ink;
        ((RectTransform)text.transform).sizeDelta=new Vector2(w-18,45);
    }
    static void BuildSharedParts()
    {
        var r=Rect(null,"UIActionButton",0,0,180,52);SceneManager.MoveGameObjectToScene(r.gameObject,scene);
        var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.sprite=sprites["Button"];image.type=UnityEngine.UI.Image.Type.Sliced;
        var button=r.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;button.transition=UnityEngine.UI.Selectable.Transition.SpriteSwap;
        button.spriteState=new SpriteState{highlightedSprite=sprites["ButtonSelected"],pressedSprite=sprites["ButtonSelected"],selectedSprite=sprites["ButtonSelected"],disabledSprite=sprites["ButtonDisabled"]};
        Text(r,"Label","준비 완료",0,0,162,45,24,true);SavePrefab(r.gameObject,"UIActionButton");Object.DestroyImmediate(r.gameObject);
        foreach(var id in new[]{"Header","Slot","Hud","Gauge"})
        {
            var root=Rect(null,"UI"+id+"Part",0,0,id=="Slot"?96:300,id=="Slot"?96:64);SceneManager.MoveGameObjectToScene(root.gameObject,scene);
            var part=root.gameObject.AddComponent<UnityEngine.UI.Image>();part.sprite=sprites[id];part.type=UnityEngine.UI.Image.Type.Sliced;part.raycastTarget=false;
            if(id=="Header")Text(root,"Title","제목",0,0,275,58,30,true,false,Cream);
            SavePrefab(root.gameObject,"UI"+id+"Part");Object.DestroyImmediate(root.gameObject);
        }
    }
    static GameObject CanvasRoot(string name)
    {var g=new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));SceneManager.MoveGameObjectToScene(g,scene);var canvas=g.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;var r=(RectTransform)g.transform;r.sizeDelta=new Vector2(1920,1080);r.localScale=Vector3.one*.01f;g.GetComponent<UnityEngine.UI.CanvasScaler>().enabled=false;return g;}
    static void CaptureCanvas(GameObject canvas,string file)
    {Render(canvas,Path.Combine(Docs,file),1920,1080,5.4f,Quaternion.identity,false);Object.DestroyImmediate(canvas);}
    static void SavePrefab(GameObject g,string name){PrefabUtility.SaveAsPrefabAsset(g,Kit+"/"+name+".prefab");}
    static void Icon(Transform parent,string source,float x,float y,float size)
    {var sp=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Sprites/Ingame/"+source+".png");if(sp==null)return;var r=Rect(parent,"ProductIcon",x,y,size,size);var i=r.gameObject.AddComponent<UnityEngine.UI.Image>();i.sprite=sp;i.preserveAspect=true;i.raycastTarget=false;}
    static void BuildUi()
    {
        BuildSharedParts();
        // 공통 외형 리소스를 공유하고 크기·내용만 달리한 실제 uGUI 프리팹을 저장한다.
        var baseBoard=Clipboard("UIClipboardBase",400,540);SavePrefab(baseBoard,"UIClipboardBase");Object.DestroyImmediate(baseBoard);
        var canvas=CanvasRoot("UIProductionAssembled");Text(canvas.transform,"Heading","공통 클립보드 · 실제 제작 검토",0,470,1500,70,44,true,false,Cream);
        Text(canvas.transform,"Subheading","같은 외형 에셋과 머티리얼을 공유하고 내용과 크기를 조정합니다",0,410,1700,45,25,false,false,new Color(.65f,.72f,.65f));
        var recipe=ClipboardInstance("UIRecipeClipboard",565,690);recipe.transform.SetParent(canvas.transform,false);((RectTransform)recipe.transform).anchoredPosition=new Vector2(-610,-20);Header(recipe.transform,"조합법",300,270);
        string[] menus={"아메리카노","카페라떼","크림 케이크"};string[] icons={"Menu_HotAmericano","Menu_CafeLatte","Menu_CreamCake"};
        for(int i=0;i<3;i++){float y=150-i*150;Image(recipe.transform,"RecipeRow","Slot",0,y,475,126);Icon(recipe.transform,icons[i],-164,y,92);Text(recipe.transform,"MenuName",menus[i],-4,y+17,220,40,25,true);Text(recipe.transform,"Ingredients","재료  1 / 1",-8,y-24,200,28,22);Text(recipe.transform,"Price",(5+i*6)+"G",180,y,70,45,31,false,true);}
        Button(recipe.transform,"뒤로","Button",0,-282,190);SavePrefab(recipe,"UIRecipeClipboardReview");
        var making=ClipboardInstance("UIMakingClipboard",382,530);making.transform.SetParent(canvas.transform,false);((RectTransform)making.transform).anchoredPosition=new Vector2(12,34);Header(making.transform,"제조",220,199);Text(making.transform,"State","조리 가능",0,122,280,52,32,true);Icon(making.transform,"Menu_HotAmericano",0,3,170);Text(making.transform,"Product","아메리카노",0,-108,290,42,28,true);Button(making.transform,"서빙 준비","ButtonSelected",0,-191,265);SavePrefab(making,"UIMakingClipboardReview");
        var settlement=ClipboardInstance("UISettlementClipboard",470,590);settlement.transform.SetParent(canvas.transform,false);((RectTransform)settlement.transform).anchoredPosition=new Vector2(608,3);Header(settlement.transform,"정산",260,230);Text(settlement.transform,"RevenueLabel","팀 매출",0,138,300,45,27,true);Text(settlement.transform,"Revenue","1,240G",0,62,360,90,60,false,true);Image(settlement.transform,"TotalSlot","Slot",0,-47,370,83);Text(settlement.transform,"Rent","임대료   120G",0,-47,330,60,27);Button(settlement.transform,"준비 완료","ButtonSelected",0,-196,300);SavePrefab(settlement,"UISettlementClipboardReview");
        Text(canvas.transform,"Footer","Unity 실제 렌더 · 검토용 프리팹 · 실제 적용 시 동일 에셋 사용",0,-459,1750,45,25,false,false,new Color(.65f,.72f,.65f));
        CaptureCanvas(canvas,"ui-production-assembled.png");
        var parts=CanvasRoot("UIPartsBoard");Text(parts.transform,"Heading","공통 부품 · 실제 에셋",0,470,1600,60,44,true,false,Cream);
        var entries=new[]{"Board","Paper","Clip","Header","Button","ButtonSelected","ButtonDisabled","Slot","Hud","Gauge"};
        var names=new[]{"판 · 테두리","종이 영역","금속 클립","제목 프레임","기본 버튼","선택 버튼","비활성 버튼","아이콘 슬롯","HUD 프레임","판정 프레임"};
        for(int i=0;i<entries.Length;i++){int col=i%5,row=i/5;float x=-740+col*370,y=190-row*370;float w=300,h=entries[i]=="Board"||entries[i]=="Paper"?250:entries[i]=="Slot"?200:entries[i]=="Clip"?150:90;if(entries[i]=="Board"||entries[i]=="Paper")w=190;Image(parts.transform,names[i],entries[i],x,y,w,h);Text(parts.transform,"AssetLabel",names[i],x,y-155,345,46,25,true,false,Cream);}
        Text(parts.transform,"Footer","원형 모델 → 투명 렌더 스프라이트 → 공통 UI 부품",0,-455,1700,50,27,false,false,new Color(.65f,.72f,.65f));CaptureCanvas(parts,"ui-production-parts.png");
        var breakdown=CanvasRoot("UIClipboardBreakdown");Text(breakdown.transform,"Heading","클립보드 · 실제 조립 구조",0,470,1700,60,44,true,false,Cream);
        string[] layers={"Board","Paper","Clip","Header"};float[] xs={-650,-225,225,650};
        for(int i=0;i<4;i++){float w=i==2?260:310,h=i==2?160:i==3?90:420;Image(breakdown.transform,"Layer",layers[i],xs[i],75,w,h);Text(breakdown.transform,"LayerLabel",new[]{"1  판 · 테두리","2  종이 · 내용 영역","3  금속 클립","4  제목 · 글자"}[i],xs[i],-220,380,55,27,true,false,Cream);}
        Text(breakdown.transform,"Structure","공유 외형  +  용도별 내용  +  상태 표시",0,-350,1650,70,39,true,false,Cream);Text(breakdown.transform,"Footer","모델과 스프라이트는 실제 저장됩니다 · 비교 화면은 Unity에서 같은 부품으로 렌더링합니다",0,-440,1800,55,23,false,false,new Color(.65f,.72f,.65f));CaptureCanvas(breakdown,"ui-production-breakdown.png");
    }
}
