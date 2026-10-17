using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

/// 기준: gallery images/unity-luigi-v2-night.webp, 1672 x 941.
/// 시안의 원판/키캡, 상단 2단 시계, 좌측 매출/가방, 우하단 무게판 배치를 그대로 옮긴다.
public static class MatchApprovedNightHud
{
    const string Path="Assets/Art/UI/Prefabs/Screen/UIMatchHudScreen.prefab";
    const string Part="Assets/Art/UI/Prefabs/Parts/UISurfacePartGreen.prefab";
    static readonly Vector2 C=new Vector2(.5f,.5f), TL=new Vector2(0,1), TC=new Vector2(.5f,1), BR=new Vector2(1,0);
    static Sprite plate,circle;
    public static string Main()
    {
        plate=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/UI/Sprites/Common/LuigiHudTrim.png").OfType<Sprite>().First(s=>s.name=="LuigiRowPlaque");
        circle=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Sprites/Common/Circle.png");
        foreach(var role in new[]{"Title","Number"})
        {
            var path="Assets/Art/Fonts/Luigi"+role+"Concept.mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Fonts/Luigi"+role+"Light.mat"));AssetDatabase.CreateAsset(mat,path);}
            mat.DisableKeyword("BEVEL_ON");mat.SetFloat("_FaceDilate",.025f);mat.SetFloat("_OutlineWidth",.06f);
            mat.SetColor("_OutlineColor",new Color(.1f,.075f,.025f));mat.SetFloat("_UnderlayOffsetX",.03f);mat.SetFloat("_UnderlayOffsetY",-.25f);mat.SetFloat("_UnderlayDilate",.02f);
            EditorUtility.SetDirty(mat);
        }
        var root=PrefabUtility.LoadPrefabContents(Path);
        try
        {
            var t=root.transform;var left=F(t,"TopLeft");Place(left,TL,27,-15,209,97);Plate(left);
            var coin=New(left,"ApprovedCoin").gameObject.GetComponent<Image>()??New(left,"ApprovedCoin").gameObject.AddComponent<Image>();
            coin.sprite=F(t,"DayHeader/SalesBlock/Coin").GetComponent<Image>().sprite;coin.color=Color.white;coin.preserveAspect=true;coin.raycastTarget=false;Center(coin.transform,-54,6,58,58);
            var revenue=F(left,"Revenue");Center(revenue,29,12,111,57);Text(revenue,46,"Number");Auto(revenue,24,46);
            Label(left,"RevenueCaption","팀 매출",new Vector2(29,-34),new Vector2(86,25),17,new Color(.5f,.9f,.58f));
            var summary=New(left,"ApprovedBagSummary");Place(summary,TL,0,-110,240,52);Plate(summary);
            var summaryIcon=New(summary,"Icon").gameObject.GetComponent<Image>()??New(summary,"Icon").gameObject.AddComponent<Image>();
            summaryIcon.sprite=F(t,"BagIcon").GetComponent<Image>().sprite;summaryIcon.color=Color.white;summaryIcon.preserveAspect=true;summaryIcon.raycastTarget=false;Center(summaryIcon.transform,-96,0,31,34);
            Label(summary,"Caption","가방",new Vector2(-51,0),new Vector2(46,34),21);
            var summaryValue=Label(summary,"Weight","",new Vector2(43,0),new Vector2(143,36),24);
            Auto(summaryValue.transform,17,24);
            var center=F(t,"TopCenter");Place(center,TC,0,-12,292,56);Plate(center);
            var moon=New(center,"Moon");var moonImage=moon.GetComponent<Image>()??moon.gameObject.AddComponent<Image>();moonImage.sprite=circle;moonImage.color=new Color(1,.83f,.25f);moonImage.raycastTarget=false;Center(moon,-112,0,28,28);
            var cut=New(moon,"Cutout");var cutImage=cut.GetComponent<Image>()??cut.gameObject.AddComponent<Image>();cutImage.sprite=circle;cutImage.color=new Color(.018f,.055f,.035f);cutImage.raycastTarget=false;Center(cut,7,5,26,26);
            Center(F(center,"Day"),-47,0,72,34);Text(F(center,"Day"),24,"Title",new Color(1,.83f,.22f));
            Label(center,"Separator","·",new Vector2(-3,0),new Vector2(12,34),23);
            Center(F(center,"Phase"),64,0,132,34);Text(F(center,"Phase"),23);
            var timerPlate=New(center,"ApprovedTimerPlate");Center(timerPlate,0,-55,194,67);Plate(timerPlate);timerPlate.SetAsFirstSibling();
            Center(F(center,"Timer"),0,-55,182,60);Text(F(center,"Timer"),46,"Number");
            var dock=New(t,"ApprovedBagDock");Place(dock,BR,-28,40,264,68);Plate(dock);dock.SetAsFirstSibling();
            var bag=F(t,"BagIcon");Place(bag,BR,-231,48,52,52);
            var hud=root.GetComponent<UIMatchHudScreen>();var so=new SerializedObject(hud);
            var weight=(TMP_Text)so.FindProperty("bagWeightText").objectReferenceValue;weight.transform.SetParent(dock,false);Center(weight.transform,31,0,177,45);Text(weight.transform,27,"Number");
            so.FindProperty("nightBagDock").objectReferenceValue=dock;
            so.FindProperty("nightBagSummaryRoot").objectReferenceValue=summary;
            so.FindProperty("nightBagSummary").objectReferenceValue=summaryValue;
            so.FindProperty("showNightBagCapacityDetails").boolValue=false;so.FindProperty("compactNightClock").boolValue=true;so.FindProperty("nightRevenueFormat").stringValue="{0:N0}G";
            var headers=so.FindProperty("nightHeader");headers.arraySize=2;headers.GetArrayElementAtIndex(0).objectReferenceValue=left.gameObject;headers.GetArrayElementAtIndex(1).objectReferenceValue=center.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            foreach(var name in new[]{"Team","Ping","Details"})F(t,name).gameObject.SetActive(false);
            Place(F(t,"BottomLeft"),new Vector2(0,0),27,24,240,132);
            PrefabUtility.SaveAsPrefabAsset(root,Path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();return "Night layout matched to approved reference; invented blue-bead medallion removed from all skill slots.";
    }
    static Transform F(Transform t,string n){var r=t.Find(n);if(r==null)throw new Exception(t.name+" missing "+n);return r;}
    static RectTransform New(Transform t,string n){var r=t.Find(n);if(r!=null)return (RectTransform)r;var go=new GameObject(n,typeof(RectTransform));go.transform.SetParent(t,false);return (RectTransform)go.transform;}
    static void Place(Transform t,Vector2 a,float x,float y,float w,float h){var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=a;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);r.localScale=Vector3.one;}
    static void Center(Transform t,float x,float y,float w,float h)=>Place(t,C,x,y,w,h);
    static void Plate(Transform t)
    {
        var old=t.Find("LuigiSurface");if(old==null){var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Part));go.name="LuigiSurface";go.transform.SetParent(t,false);old=go.transform;}
        var rect=(RectTransform)old;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;old.SetAsFirstSibling();
        var image=old.GetComponent<Image>();image.sprite=plate;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=7;image.color=new Color(.68f,.72f,.58f);image.raycastTarget=false;image.fillCenter=true;
        var back=t.GetComponent<Image>();if(back!=null)back.color=Color.clear;
    }
    static void Text(Transform t,float size,string role="Title",Color? tint=null)
    {
        var text=t.GetComponent<TMP_Text>();text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/Luigi"+role+" SDF.asset");text.fontSharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Fonts/Luigi"+role+"Concept.mat");text.fontSize=size;text.enableAutoSizing=false;text.alignment=TextAlignmentOptions.Center;text.enableVertexGradient=false;text.color=tint??new Color(1,.965f,.82f);text.extraPadding=true;text.textWrappingMode=TextWrappingModes.NoWrap;
    }
    static TMP_Text Label(Transform parent,string name,string content,Vector2 position,Vector2 size,float fontSize,Color? tint=null)
    {var r=New(parent,name);var text=r.GetComponent<TMP_Text>()??r.gameObject.AddComponent<TextMeshProUGUI>();text.text=content;text.raycastTarget=false;Center(r,position.x,position.y,size.x,size.y);Text(r,fontSize,"Title",tint);return text;}
    static void Auto(Transform t,float min,float max){var text=t.GetComponent<TMP_Text>();text.enableAutoSizing=true;text.fontSizeMin=min;text.fontSizeMax=max;}
}
