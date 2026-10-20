using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// 실제 저장된 파츠에 시각 검토용 값만 넣는다. 스폰·판매·팀 소유권은 만들지 않는다.
public static class PreviewLuigiReportedParts
{
    const string Parts="Assets/Art/UI/Prefabs/Parts/";
    static T Ref<T>(UnityEngine.Object o,string f)where T:UnityEngine.Object=>new SerializedObject(o).FindProperty(f).objectReferenceValue as T;
    static void Position(RectTransform r,Transform parent,float x,float y,float scale=1){r.SetParent(parent,false);r.anchorMin=r.anchorMax=new(.5f,.5f);r.anchoredPosition=new(x,y);r.localScale=Vector3.one*scale;}
    public static string Main(string mode="orders")
    {
        var hud=UIManager.Instance.CurrentScreen as UIMatchHudScreen;
        if(hud==null)throw new InvalidOperationException("Show HUD first");
        var old=hud.WorldMarkers.Find("Reported part samples");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
        var root=new GameObject("Reported part samples",typeof(RectTransform));Position((RectTransform)root.transform,hud.WorldMarkers,0,0);
        if(mode=="orders")
        {
            var ids=new[]{MenuId.HotAmericano,MenuId.CafeLatte,MenuId.CafeMocha,MenuId.IcedLatte};
            for(int i=0;i<4;i++){
                var part=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Parts+"UICustomerOrder.prefab"),root.transform).GetComponent<UICustomerOrder>();part.enabled=false;
                var view=Ref<RectTransform>(part,"view");Position(view,root.transform,-335+i*158,285,.70f);Ref<CanvasGroup>(part,"group").alpha=1;
                Ref<UINamedItemIcon>(part,"product").Show(ResourceManager.Instance.MenuSprite(ids[i]),"",null,true,false);
                Ref<TMP_Text>(part,"expectedPrice").text=new[]{"22","30","114","31"}[i];Ref<UIBubbleFill>(part,"patience").Show(i*.2f,new(.45f,.8f,.4f));Ref<Image>(part,"judgementArrow").gameObject.SetActive(false);
            }
        }
        else if(mode=="nameplates"){
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Parts+"UIPlayerNameplate.prefab");
            for(int i=0;i<2;i++){
                var view=UnityEngine.Object.Instantiate(source.transform.Find("View"),root.transform) as RectTransform;Position(view,root.transform,-80+i*230,-80,1);
                view.GetComponent<Image>().color=Color.clear;view.Find("LuigiSurface").GetComponent<Image>().enabled=i!=0;view.Find("Dot").GetComponent<Image>().enabled=i!=0;view.Find("Dot").GetComponent<Image>().color=TeamColors.Of(1);view.Find("SelfArrow").gameObject.SetActive(i==0);
                var text=view.GetComponentInChildren<TMP_Text>();text.text=i==0?"YOU":"Mina";text.color=i==0?new(.74f,1,.49f):Color.white;
            }
            var ingredient=AssetDatabase.LoadAssetAtPath<GameObject>(Parts+"UIIngredientBadge.prefab");
            for(int i=0;i<2;i++){
                var view=UnityEngine.Object.Instantiate(ingredient.transform.Find("View"),root.transform) as RectTransform;Position(view,root.transform,-80+i*230,55,1);view.GetComponent<CanvasGroup>().alpha=1;
                view.GetComponentInChildren<UIDayItemIcon>().Render(ResourceManager.Instance.IngredientSprite(i==0?Ingredient.Bean:Ingredient.Milk),i==0?"상비":"12",true,i==1,false);
            }
        }
        else throw new ArgumentException(mode);
        Canvas.ForceUpdateCanvases();
        return mode+": saved part prefabs, controlled visual inputs; no network objects / authority changes";
    }
}
