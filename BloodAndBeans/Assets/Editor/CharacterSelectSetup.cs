using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// 선택창 프리팹 편집과 모델 초상 굽기. 런타임에는 저장된 프리팹과 이미지만 사용한다.
public static class CharacterSelectSetup
{
    const string ScreenPath = "Assets/Art/UI/Prefabs/Screen/UICharacterSelectScreen.prefab";
    const string StagePath = "Assets/Art/Environment/Prefabs/CharacterStage.prefab";
    const string CardPath = "Assets/Art/UI/Prefabs/Parts/UICharacterCard.prefab";
    const string SpritesPath = "Assets/Art/UI/Sprites/CharacterSelect/";
    const string MaterialsPath = "Assets/Art/Environment/Materials/";
    static readonly Color Ink = new(.075f, .105f, .11f);
    static readonly Color Panel = new(.12f, .17f, .17f);
    static readonly Color Cream = new(.96f, .90f, .77f);
    static readonly Color Gold = new(.84f, .65f, .35f);
    static readonly Color Muted = new(.62f, .72f, .68f);

    [MenuItem("Blood & Beans/캐릭터/임시 외형 연결", priority = 42)]
    public static void PrepareModels()
    {
        var config = Resources.Load<CharacterVisualConfig>(CharacterVisualConfig.AssetName);
        var data = new SerializedObject(config);
        var entries = data.FindProperty("entries");
        for (var i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            var prefab = (GameObject)entry.FindPropertyRelative("model").objectReferenceValue;
            if (prefab == null || !prefab.name.StartsWith("Ghost_")) continue;
            var path = AssetDatabase.GetAssetPath(prefab);
            Edit(path, root =>
            {
                var appearance = root.GetComponent<CharacterModel>() ?? root.AddComponent<CharacterModel>();
                var so = new SerializedObject(appearance);
                var tint = so.FindProperty("teamTintRoots");
                tint.arraySize = 1;
                tint.GetArrayElementAtIndex(0).objectReferenceValue = root.transform.Find("MonsterCrest");
                so.ApplyModifiedPropertiesWithoutUndo();
            });
            // 임시 유령의 원본 배율을 유지한다. 스킨 메시의 초기 bounds는 실제 렌더 크기와 다르다.
            entry.FindPropertyRelative("scale").floatValue = 1f;
            entry.FindPropertyRelative("localPosition").vector3Value = Vector3.zero;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Blood & Beans/캐릭터/선택창 꾸미기", priority = 43)]
    public static void Decorate()
    {
        Edit(CardPath, root =>
        {
            var t = root.transform;
            root.GetComponent<UnityEngine.UI.Image>().color = Panel;
            foreach(var round in root.GetComponentsInChildren<UIRoundImage>(true))
            {
                var roundData = new SerializedObject(round);
                roundData.FindProperty("radiusOverride").floatValue = 16f;
                roundData.ApplyModifiedPropertiesWithoutUndo();
            }
            var portrait = t.Find("Portrait").GetComponent<UnityEngine.UI.Image>();
            Stretch(portrait.rectTransform, 8, 8, 8, 38);
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            var label = t.Find("Name").GetComponent<TMP_Text>();
            label.fontSize = 22f / UITheme.Config.FontScale;
            label.color = Cream;
            label.alignment = TextAlignmentOptions.Center;
            Edge(label.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(.5f, 0), new Vector2(0, 8), new Vector2(-12, 30));
            var mark = t.Find("SelectedBadge/SelectedMark")?.GetComponent<TMP_Text>() ?? NewText(t, "SelectedMark", "선택", 16f / UITheme.Config.FontScale, Ink);
            Edge(mark.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8, -8), new Vector2(48, 25));
            mark.alignment = TextAlignmentOptions.Center;
            var badge = NewBox(t, "SelectedBadge", Gold);
            badge.SetSiblingIndex(mark.transform.GetSiblingIndex());
            Edge(badge, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8, -8), new Vector2(48, 25));
            mark.transform.SetParent(badge, false);
            badge.SetAsLastSibling();
            Stretch(mark.rectTransform);
            var so = new SerializedObject(root.GetComponent<UICharacterCard>());
            so.FindProperty("selectedMark").objectReferenceValue = badge.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            badge.gameObject.SetActive(false);
            var claim = t.Find("Claim/Chip");
            Edge((RectTransform)claim, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), new Vector2(0, 32), new Vector2(0, 28));
            claim.GetComponent<UnityEngine.UI.Image>().color = Ink;
            claim.GetComponentInChildren<TMP_Text>().fontSize = 16f / UITheme.Config.FontScale;
        });
        Edit(ScreenPath, root =>
        {
            var t = root.transform.Find("Stage");
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                { text.color = Cream; text.enableAutoSizing = false; }
            var caption = t.Find("Caption").GetComponent<TMP_Text>();
            caption.text = "오늘의 크루를 골라주세요";
            caption.fontSize = 38;
            Edge(caption.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(44, -73), new Vector2(750, 56));
            var brand = NewText(t, "Brand", "BLOOD & BEAN   /   CREW ROOM", 18, Gold);
            Edge(brand.rectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(46, -36), new Vector2(700, 28));
            var ready = (RectTransform)t.Find("GameObject");
            Edge(ready, Vector2.one, Vector2.one, Vector2.one, new Vector2(-44, -42), new Vector2(280, 62));
            var readyImage = ready.GetComponent<UnityEngine.UI.Image>() ?? ready.gameObject.AddComponent<UnityEngine.UI.Image>();
            readyImage.color = Panel;
            readyImage.raycastTarget = false;
            var readyLayout = ready.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            readyLayout.padding = new RectOffset(20,20,8,8);
            readyLayout.spacing = 12;
            foreach (var txt in ready.GetComponentsInChildren<TMP_Text>()) { txt.fontSize = 24; txt.alignment = TextAlignmentOptions.Center; }
            var panelWrap = (RectTransform)t.Find("PanelWrap");
            Edge(panelWrap, Vector2.up, Vector2.up, Vector2.up, new Vector2(44,-168), new Vector2(428,622));
            var detail = panelWrap.Find("Detail");
            detail.GetComponent<UnityEngine.UI.Image>().color = Panel;
            detail.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 390;
            var tab = panelWrap.Find("PanelTab");
            tab.GetComponent<UnityEngine.UI.LayoutElement>().preferredWidth = 38;
            tab.GetComponent<UnityEngine.UI.Image>().color = Ink;
            var selectedCaption = detail.Find("SelectedCaption").GetComponent<TMP_Text>();
            selectedCaption.text = "MY CREW";
            selectedCaption.color = Gold;
            selectedCaption.fontSize = 16;
            TopText(selectedCaption, 22, 20, 28);
            var selectedName = detail.Find("SelectedName").GetComponent<TMP_Text>();
            selectedName.fontSize = 38;
            TopText(selectedName,22,52,56);
            foreach(var rule in detail.Cast<Transform>().Where(c=>c.name=="Rule")) rule.gameObject.SetActive(false);
            var day = (RectTransform)detail.Find("DayGroup");
            Edge(day,Vector2.up,Vector2.one,new Vector2(.5f,1),new Vector2(0,-138),new Vector2(-44,150));
            AbilityGroup(day, "낮  /  방해 스킬", Gold);
            var night = detail.Find("NightGroup") as RectTransform;
            if(night == null)
            {
                night = new GameObject("NightGroup",typeof(RectTransform),typeof(UnityEngine.UI.VerticalLayoutGroup)).GetComponent<RectTransform>();
                night.SetParent(detail,false);
                foreach(var name in new[]{"NightCaption","NightName","NightEffect"}) detail.Find(name).SetParent(night,false);
            }
            Edge(night,Vector2.up,Vector2.one,new Vector2(.5f,1),new Vector2(0,-322),new Vector2(-44,140));
            AbilityGroup(night, "밤  /  탐색 스킬", new Color(.54f,.73f,.81f));
            var teamTitle = NewText(detail,"TeamCaption","함께할 팀",18,Muted);
            TopText(teamTitle,22,492,28);
            var teams = (RectTransform)detail.Find("Nameplate");
            Edge(teams,Vector2.up,Vector2.one,new Vector2(.5f,1),new Vector2(0,-532),new Vector2(-44,58));
            teams.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().spacing = 8;
            foreach(var txt in teams.GetComponentsInChildren<TMP_Text>()) {txt.fontSize=20;Stretch(txt.rectTransform,3,3,3,3);}
            var headingBar = NewBox(t,"HeadingBar",Ink);
            Edge(headingBar,Vector2.up,Vector2.one,new Vector2(.5f,1),new Vector2(0,-144),new Vector2(0,126));
            headingBar.SetAsFirstSibling();
            var cards = (RectTransform)t.Find("Cards");
            Edge(cards,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(214,116),new Vector2(1360,180));
            var grid = cards.GetComponent<UnityEngine.UI.GridLayoutGroup>();
            grid.cellSize=new Vector2(154,180);
            grid.spacing=new Vector2(18,0);
            grid.constraint=UnityEngine.UI.GridLayoutGroup.Constraint.FixedRowCount;
            grid.constraintCount=1;
            var strip = NewBox(t,"CardTray",Ink);
            Edge(strip,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(214,100),new Vector2(1396,212));
            strip.SetAsFirstSibling();
            var title = NewText(t,"StageHeading","커피는 함께, 밤은 대담하게.",26,Cream);
            Edge(title.rectTransform,Vector2.up,Vector2.up,Vector2.up,new Vector2(516,-180),new Vector2(1000,40));
            var hint = NewText(t,"StageSubheading","캐릭터와 팀을 고른 뒤 준비해 주세요.",20,Muted);
            Edge(hint.rectTransform,Vector2.up,Vector2.up,Vector2.up,new Vector2(518,-226),new Vector2(1000,34));
            var confirm = (RectTransform)t.Find("Confirm");
            Edge(confirm,Vector2.right,Vector2.right,Vector2.right,new Vector2(-44,28),new Vector2(286,62));
            confirm.GetComponent<UnityEngine.UI.Image>().color=Gold;
            var buttonColors=confirm.GetComponent<UnityEngine.UI.Button>().colors;
            buttonColors.normalColor=Color.white;buttonColors.highlightedColor=new Color(1,1,.88f);buttonColors.disabledColor=new Color(.45f,.45f,.45f);
            confirm.GetComponent<UnityEngine.UI.Button>().colors=buttonColors;
            var confirmText=confirm.GetComponentInChildren<TMP_Text>();
            confirmText.fontSize=26;confirmText.color=Ink;confirmText.alignment=TextAlignmentOptions.Center;Stretch(confirmText.rectTransform);
            Edge((RectTransform)t.Find("PanelHint"),Vector2.right,Vector2.right,Vector2.right,new Vector2(-362,42),new Vector2(180,31));
            t.Find("ScrollHint").gameObject.SetActive(false);
            foreach(var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                if(image.GetComponent<UnityEngine.UI.Button>()==null) image.raycastTarget=false;
        });
        DecorateStage();
        AssetDatabase.SaveAssets();
    }

    static void AbilityGroup(RectTransform group, string heading, Color accent)
    {
        var layout=group.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.spacing=8; layout.childControlWidth=true;layout.childControlHeight=true;
        layout.childForceExpandHeight=false;layout.childForceExpandWidth=true;
        var texts=group.GetComponentsInChildren<TMP_Text>();
        for(var i=0;i<texts.Length;i++)
        {
            texts[i].fontSize=i==0?17:i==1?28:21;
            texts[i].color=i==0?accent:Cream;
            var element=texts[i].GetComponent<UnityEngine.UI.LayoutElement>()??texts[i].gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            element.preferredHeight=i==0?26:i==1?38:74;
            element.flexibleHeight=0;
        }
        texts[0].text=heading;
    }

    static void DecorateStage()
    {
        Edit(StagePath,root=>
        {
            var t=root.transform;
            var cafe=t.Find("Cafe_Lobby");
            cafe.localPosition=new Vector3(0,0,13);
            // 기존 배경 모델은 유지하되, 크루 앞을 가리던 가구만 숨긴다.
            foreach(var name in new[]{"Counter","Shelf","ShelfMakerSide"}) cafe.Find(name)?.gameObject.SetActive(false);
            cafe.Find("Floor").GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(MaterialsPath+"CafeCream.mat");
            var so=new SerializedObject(root.GetComponent<CharacterStage>());
            so.FindProperty("seatSpacing").floatValue=1.35f;
            so.FindProperty("nameplateHeight").floatValue=2.6f;
            so.ApplyModifiedPropertiesWithoutUndo();
            t.Find("StageSpot").localPosition=new Vector3(0,5,10.4f);
            var cam=t.Find("StageVCam").GetComponent<CinemachineCamera>();
            var composer=cam.GetComponent<CinemachinePositionComposer>();
            composer.CameraDistance=13;
            composer.TargetOffset=new Vector3(0,1.1f,0);
            composer.Damping=Vector3.zero;
            var composition=composer.Composition;
            composition.ScreenPosition=new Vector2(.14f,.02f);
            composer.Composition=composition;
            cam.transform.localRotation=Quaternion.Euler(7,0,0);
            var camera=t.Find("StageCamera").GetComponent<Camera>();
            camera.clearFlags=CameraClearFlags.SolidColor;
            DisablePostProcessing(camera);
            foreach(var light in root.GetComponentsInChildren<Light>())light.cullingMask=1<<root.layer;
            t.Find("KeyLight").GetComponent<Light>().intensity=1.1f;
            t.Find("FillLight").GetComponent<Light>().intensity=.6f;
            var decor=t.Find("CrewDecor");
            if(decor==null){decor=new GameObject("CrewDecor").transform;decor.SetParent(t,false);}
            var walnut=AssetDatabase.LoadAssetAtPath<Material>(MaterialsPath+"CafeWalnut.mat");
            var sage=AssetDatabase.LoadAssetAtPath<Material>(MaterialsPath+"CafeSage.mat");
            var brass=AssetDatabase.LoadAssetAtPath<Material>(MaterialsPath+"CafeBrass.mat");
            Prop(decor,"BackWall",PrimitiveType.Cube,new Vector3(0,2.5f,17.8f),new Vector3(24,5,.25f),sage);
            Prop(decor,"Wainscot",PrimitiveType.Cube,new Vector3(0,.75f,17.5f),new Vector3(24,1.5f,.2f),walnut);
            Prop(decor,"WallTrim",PrimitiveType.Cube,new Vector3(0,1.53f,17.3f),new Vector3(24,.06f,.08f),brass);
            Prop(decor,"CrewRug",PrimitiveType.Cube,new Vector3(.7f,.02f,10.4f),new Vector3(12,.04f,3.1f),sage);
            for(var i=0;i<3;i++)
            {
                var x=-5f+i*5;
                Prop(decor,"WindowFrame"+i,PrimitiveType.Cube,new Vector3(x,3.4f,17.3f),new Vector3(3.5f,2.3f,.1f),walnut);
                Prop(decor,"WindowPane"+i,PrimitiveType.Cube,new Vector3(x,3.4f,17.2f),new Vector3(3.25f,2.05f,.05f),brass);
                Prop(decor,"WindowMullion"+i,PrimitiveType.Cube,new Vector3(x,3.4f,17.1f),new Vector3(.07f,2.05f,.05f),walnut);
            }
            foreach(var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=root.layer;
        });
    }

    [MenuItem("Blood & Beans/캐릭터/모델에서 초상 다시 만들기", priority = 44)]
    public static void Portraits()
    {
        var config=Resources.Load<CharacterVisualConfig>(CharacterVisualConfig.AssetName);
        var so=new SerializedObject(config);
        var entries=so.FindProperty("entries");
        for(var i=0;i<entries.arraySize;i++)
        {
            var entry=entries.GetArrayElementAtIndex(i);
            var id=(CharacterId)entry.FindPropertyRelative("id").intValue;
            var scene=EditorSceneManager.NewPreviewScene();
            RenderTexture rt=null;
            try
            {
                var holder=new GameObject("Portrait");SceneManager.MoveGameObjectToScene(holder,scene);
                var model=config.SpawnModel(id,holder.transform,12);
                if(model==null)continue;
                model.GetComponent<CharacterModel>()?.Tint(TeamColors.Of(i%4),1);
                var cam=CameraFor(scene,"PortraitCamera");
                cam.transform.position=new Vector3(0,1.25f,5);
                cam.transform.LookAt(new Vector3(0,1.1f,0));
                cam.orthographic=true;cam.orthographicSize=1.45f;cam.backgroundColor=Panel;
                LightFor(scene);
                rt=new RenderTexture(384,384,24);cam.targetTexture=rt;cam.Render();
                var path=SpritesPath+"Crew_"+id+".png";
                SaveTexture(rt,path);
                AssetDatabase.ImportAsset(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;
                importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }
            finally{if(rt!=null){rt.Release();Object.DestroyImmediate(rt);}EditorSceneManager.ClosePreviewScene(scene);}
        }
        so.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssets();
    }

    public static void Preview(string path)
    {
        var scene=EditorSceneManager.NewPreviewScene();
        var rt=new RenderTexture(1920,1080,24);
        try
        {
            var stage=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(StagePath));
            SceneManager.MoveGameObjectToScene(stage,scene);
            var stageScript=stage.GetComponent<CharacterStage>();
            typeof(CharacterStage).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(stageScript,null);
            stageScript.Layout();
            var screen=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath));
            SceneManager.MoveGameObjectToScene(screen,scene);
            var view=screen.GetComponent<UICharacterSelectScreen>();
            typeof(UICharacterSelectScreen).GetField("stage",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(view,stageScript);
            var canvas=screen.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.WorldSpace;
            screen.GetComponent<UnityEngine.UI.CanvasScaler>().enabled=false;
            var rect=screen.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(1920,1080);rect.position=Vector3.zero;rect.localScale=Vector3.one;rect.pivot=new Vector2(.5f,.5f);
            var uiCamera=CameraFor(scene,"UIPreviewCamera");
            uiCamera.transform.position=new Vector3(0,0,-1000);uiCamera.orthographic=true;uiCamera.orthographicSize=540;
            uiCamera.farClipPlane=2000;uiCamera.cullingMask=1<<5;uiCamera.clearFlags=CameraClearFlags.Depth;
            canvas.worldCamera=uiCamera;
            var claims=new[]{new UICharacterSelectScreen.Claim(2,"팀원 선택",TeamColors.Of(0),true)};
            view.Bind(claims,0,0,"YOU","밤  /  탐색 스킬","",_=>{},_=>{},()=>{},()=>{});
            foreach(var card in screen.GetComponentsInChildren<UICharacterCard>(true))
                card.GetComponent<UIFontScale>()?.Apply(UITheme.Config.FontScale);
            var camera=stageScript.StageCamera;
            camera.scene=scene;
            DisablePostProcessing(camera);
            var brain=camera.GetComponent<CinemachineBrain>();brain.enabled=false;
            var position=stage.transform.position+new Vector3(-2.4f,2.7f,-2.5f);
            camera.transform.position=position;camera.transform.rotation=Quaternion.Euler(7,0,0);
            camera.fieldOfView=40;camera.targetTexture=rt;
            var roster=new[]{
                new SteamLobby.RoomMember(1,"YOU",0,true,true,true,0),
                new SteamLobby.RoomMember(2,"BEAN",0,false,false,true,2),
                new SteamLobby.RoomMember(3,"MOCHA",1,false,false,true,4),
                new SteamLobby.RoomMember(4,"LATTE",1,false,false,false,7)};
            view.SetRoster(roster);view.SetLobby(true,false,false,3,4);
            foreach(var tr in screen.GetComponentsInChildren<Transform>(true))tr.gameObject.layer=5;
            Canvas.ForceUpdateCanvases();
            foreach(var group in screen.GetComponentsInChildren<UnityEngine.UI.LayoutGroup>(true))
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)group.transform);
            Canvas.ForceUpdateCanvases();
            // UI는 같은 카메라 바로 앞에 놓아 URP의 두 번째 Base 카메라가 무대를 지우지 않게 한다.
            var uiScale = 2f * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f) / 1080f;
            rect.position = camera.transform.position + camera.transform.forward;
            rect.rotation = camera.transform.rotation;
            rect.localScale = Vector3.one * uiScale;
            camera.cullingMask |= 1 << 5;
            var plates=screen.GetComponentsInChildren<UICharacterNameplate>(true);
            for(var i=0;i<roster.Length;i++)
            {
                var seat=stage.transform.Find("Seat"+i);
                var point=camera.WorldToViewportPoint(seat.position+Vector3.up*2.6f);
                plates[i].PlaceAt(new Vector2((point.x-.5f)*1920,(point.y-.5f)*1080));
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();

            SaveTexture(rt,path);
        }
        finally{rt.Release();Object.DestroyImmediate(rt);EditorSceneManager.ClosePreviewScene(scene);}
    }

    static void DisablePostProcessing(Camera camera)
    {
        var data = camera.GetComponents<Component>().First(c => c.GetType().Name == "UniversalAdditionalCameraData");
        var so = new SerializedObject(data);
        so.FindProperty("m_RenderPostProcessing").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Camera CameraFor(Scene scene,string name)
    {
        var go=new GameObject(name,typeof(Camera));SceneManager.MoveGameObjectToScene(go,scene);
        var cam=go.GetComponent<Camera>();cam.scene=scene;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Ink;return cam;
    }
    static void LightFor(Scene scene)
    {
        var go=new GameObject("PortraitLight",typeof(Light));SceneManager.MoveGameObjectToScene(go,scene);
        var light=go.GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;
        go.transform.rotation=Quaternion.Euler(35,155,0);
    }
    static void SaveTexture(RenderTexture rt,string path)
    {
        var previous=RenderTexture.active;var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
        try{RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}
        finally{RenderTexture.active=previous;Object.DestroyImmediate(tex);}
    }
    static void Edit(string path,Action<GameObject> edit)
    {
        var root=PrefabUtility.LoadPrefabContents(path);
        try{edit(root);PrefabUtility.SaveAsPrefabAsset(root,path);}
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    static void Prop(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
    {
        var t=parent.Find(name);
        if(t==null){var go=GameObject.CreatePrimitive(type);Object.DestroyImmediate(go.GetComponent<Collider>());t=go.transform;t.name=name;t.SetParent(parent,false);}
        t.localPosition=position;t.localScale=scale;t.GetComponent<Renderer>().sharedMaterial=material;
    }
    static RectTransform NewBox(Transform parent,string name,Color color)
    {
        var t=parent.Find(name) as RectTransform;
        if(t==null){t=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();t.SetParent(parent,false);}
        var image=t.GetComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;return t;
    }
    static TMP_Text NewText(Transform parent,string name,string value,float size,Color color)
    {
        var t=parent.Find(name);
        if(t==null){t=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)).transform;t.SetParent(parent,false);}
        var text=t.GetComponent<TMP_Text>();text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/Pretendard-Regular SDF.asset");
        text.text=value;text.enableAutoSizing=false;text.fontSize=size;text.color=color;text.raycastTarget=false;return text;
    }
    static void TopText(TMP_Text text,float inset,float y,float height)
        => Edge(text.rectTransform,Vector2.up,Vector2.one,new Vector2(0,1),new Vector2(inset,-y),new Vector2(-inset*2,height));
    static void Stretch(RectTransform t,float left=0,float top=0,float right=0,float bottom=0)
    {t.anchorMin=Vector2.zero;t.anchorMax=Vector2.one;t.offsetMin=new Vector2(left,bottom);t.offsetMax=new Vector2(-right,-top);}
    static void Edge(RectTransform t,Vector2 min,Vector2 max,Vector2 pivot,Vector2 pos,Vector2 size)
    {t.anchorMin=min;t.anchorMax=max;t.pivot=pivot;t.anchoredPosition=pos;t.sizeDelta=size;}
}

/// 모델 교체 안내와 누락 검사를 설정 애셋 바로 옆에서 제공한다.
[CustomEditor(typeof(CharacterVisualConfig))]
public sealed class CharacterVisualConfigEditor : Editor
{
    public override UnityEngine.UIElements.VisualElement CreateInspectorGUI()
    {
        var root = new UnityEngine.UIElements.VisualElement();
        root.Add(new UnityEngine.UIElements.HelpBox(
            "Model에 외형 전용 프리팹을 넣으세요. 루트에 CharacterModel을 붙이고 Team Tint Roots에 앞치마·장식만 연결합니다. " +
            "Local Position은 발 위치, Local Euler Angles는 +Z 정면 보정, Scale은 공통 크기입니다. " +
            "Animator는 외형 프리팹에 두고 Apply Root Motion은 끕니다. 이동·충돌·네트워크는 Player에 유지합니다. " +
            "Icon은 직접 지정하거나 아래 버튼으로 다시 만들 수 있습니다.", UnityEngine.UIElements.HelpBoxMessageType.Info));
        UnityEditor.UIElements.InspectorElement.FillDefaultInspector(root, serializedObject, this);
        root.Add(new UnityEngine.UIElements.Button(() => Validate((CharacterVisualConfig)target)) { text = "모델 연결 확인" });
        root.Add(new UnityEngine.UIElements.Button(CharacterSelectSetup.Portraits) { text = "모델에서 초상 다시 만들기" });
        return root;
    }
    public static void Validate(CharacterVisualConfig config)
    {
        foreach (var character in CharacterCatalog.All)
        {
            var model = config.ModelFor(character.Id);
            if (model == null || model.GetComponent<CharacterModel>() == null)
                throw new InvalidOperationException(character.Name + ": Model과 루트 CharacterModel을 연결하세요.");
            if (model.GetComponentInChildren<Collider>(true) != null || model.GetComponentInChildren<Rigidbody>(true) != null)
                throw new InvalidOperationException(character.Name + ": 충돌체와 Rigidbody는 외형이 아니라 Player에 두세요.");
            foreach (var animator in model.GetComponentsInChildren<Animator>(true))
                if (animator.applyRootMotion) throw new InvalidOperationException(character.Name + ": Apply Root Motion을 끄세요.");
            if (ResourceManager.Instance.CrewSprite(character.Id) == null)
                throw new InvalidOperationException(character.Name + ": 초상이 CharacterSelect 아틀라스에 없습니다.");
        }
        CDebug.Log("캐릭터 모델·초상 연결 확인 완료");
    }
}
