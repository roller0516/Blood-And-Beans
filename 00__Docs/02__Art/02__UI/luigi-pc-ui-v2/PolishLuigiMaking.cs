using System;
using UnityEngine;
using UnityEditor;
using TMPro;

/// Existing production prefab presentation only; no gauge rules or target selection changes.
public static class PolishLuigiMaking
{
    const string Root="Assets/Art/UI/Prefabs/";
    static readonly Vector2 C=new(.5f,.5f), BC=new(.5f,0);
    public static string Main()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first");
        Edit(Root+"Parts/UIMakingCard.prefab",Card);
        Edit(Root+"Parts/UICompletionGauge.prefab",Gauge);
        Edit(Root+"Screen/UIMatchHudScreen.prefab",Hud);
        Edit(Root+"Popup/UIMatchResultPopup.prefab",Result);
        return "Existing making dock/card, gauge and result ratio bars polished; gameplay bindings retained.";
    }
    static void Edit(string p,Action<GameObject> action){var g=PrefabUtility.LoadPrefabContents(p);try{action(g);PrefabUtility.SaveAsPrefabAsset(g,p);}finally{PrefabUtility.UnloadPrefabContents(g);}}
    static T Ref<T>(UnityEngine.Object o,string n)where T:UnityEngine.Object=>new SerializedObject(o).FindProperty(n).objectReferenceValue as T;
    static void Place(Transform t,Vector2 a,float x,float y,float w,float h,Vector2? pivot=null){var r=(RectTransform)t;r.anchorMin=r.anchorMax=a;r.pivot=pivot??C;r.anchoredPosition=new(x,y);r.sizeDelta=new(w,h);}
    static void Round(UnityEngine.UI.Image image,float radius,float shade)
    {
        image.sprite=null;image.type=UnityEngine.UI.Image.Type.Simple;
        image.material=Resources.Load<UIThemeConfig>(UIThemeConfig.AssetName).RoundedMaterial;
        var effect=image.GetComponent<UIRoundImage>()??image.gameObject.AddComponent<UIRoundImage>();effect.enabled=true;
        var so=new SerializedObject(effect);so.FindProperty("radiusOverride").floatValue=radius;so.FindProperty("bottomBrightness").floatValue=shade;so.ApplyModifiedPropertiesWithoutUndo();
    }
    static UnityEngine.UI.Image Image(Transform p,string n)
    {
        var t=p.Find(n);if(t==null){t=new GameObject(n,typeof(RectTransform),typeof(UnityEngine.UI.Image)).transform;t.SetParent(p,false);}
        var image=t.GetComponent<UnityEngine.UI.Image>();image.raycastTarget=false;return image;
    }
    static void Card(GameObject g)
    {
        var card=g.GetComponent<UIMakingCard>();Round(Ref<UnityEngine.UI.Image>(card,"frame"),17,.72f);
        var status=Ref<TMP_Text>(card,"statusText");status.fontSize=23;status.color=new(.12f,.065f,.025f);
        Place(Ref<TMP_Text>(card,"check").transform,new(1,.5f),-1,0,20,28,new(1,.5f));
        Place(Ref<TMP_Text>(card,"nameText").transform,C,-9,0,156,32);
    }
    static void Gauge(GameObject g)
    {
        var gauge=g.GetComponent<UICompletionGauge>();var bar=Ref<RectTransform>(gauge,"bar");
        Place(bar,C,0,-4,472,33);Round(bar.GetComponent<UnityEngine.UI.Image>(),16,.48f);
        Round(Ref<RectTransform>(gauge,"good").GetComponent<UnityEngine.UI.Image>(),0,.52f);
        Round(Ref<RectTransform>(gauge,"perfect").GetComponent<UnityEngine.UI.Image>(),0,.52f);
        var needle=Ref<RectTransform>(gauge,"needle");needle.sizeDelta=new(8,49);Round(needle.GetComponent<UnityEngine.UI.Image>(),4,.94f);
        var track=Image(bar.parent,"TimingTrack");Place(track.transform,C,0,-4,486,47);track.color=new(.025f,.035f,.018f);Round(track,22,.8f);
        track.transform.SetAsFirstSibling();track.transform.SetSiblingIndex(Math.Max(0,bar.GetSiblingIndex()-1));
        var shine=Image(bar,"TimingShine");var r=(RectTransform)shine.transform;r.anchorMin=new(0,1);r.anchorMax=Vector2.one;r.offsetMin=new(12,-6);r.offsetMax=new(-12,-4);shine.color=new(1,1,.8f,.28f);
        var title=g.transform.Find("TimingTitle");if(title!=null){Place(title,new(.5f,1),0,-4,300,32,new(.5f,1));title.GetComponent<TMP_Text>().fontSize=25;}
    }
    static void Hud(GameObject g)
    {
        var day=g.GetComponentInChildren<UIMatchHudDay>(true);Place(Ref<RectTransform>(day,"makingCardDock"),C,400,-10,228,344);
        var gauge=g.GetComponentInChildren<UICompletionGauge>(true);Place(gauge.transform,BC,400,190,560,170,BC);
    }
    static void Result(GameObject g)
    {
        for(int i=0;i<4;i++){var bar=g.transform.Find("Stage/FinalRanking/Rank"+i+"/Bar");Round(bar.GetComponent<UnityEngine.UI.Image>(),10,1);Round(bar.Find("Fill").GetComponent<UnityEngine.UI.Image>(),10,.68f);}
    }
}
