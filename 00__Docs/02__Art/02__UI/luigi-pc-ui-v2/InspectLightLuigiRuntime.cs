using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using Newtonsoft.Json.Linq;

/// 실제 Play Mode에 표시한 UI의 해상도와 글자 경계를 기록한다.
public static class InspectLightLuigiRuntime
{
    const string Output="../00__Docs/02__Art/02__UI/luigi-pc-ui-v2/light-ui-preview/runtime-state-checks.json";
    public static string Main(string label)
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play mode required");
        Canvas.ForceUpdateCanvases();
        var all=UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude)
            .Where(t=>t.enabled&&t.canvas!=null&&!string.IsNullOrWhiteSpace(t.text)&&t.GetComponentsInParent<CanvasGroup>().All(c=>c.alpha>.01f)).ToArray();
        foreach(var t in all)t.ForceMeshUpdate();
        var overflow=all.Where(t=>t.isTextOverflowing).Select(t=>new{path=PathOf(t.transform),text=t.text,font=t.font.name,size=t.fontSize,width=t.rectTransform.rect.width,height=t.rectTransform.rect.height,preferredWidth=t.preferredWidth,preferredHeight=t.preferredHeight,mode=t.overflowMode.ToString()}).ToArray();
        var result=new{label,width=Screen.width,height=Screen.height,activeTextCount=all.Length,overflow};
        var list=File.Exists(Output)?JArray.Parse(File.ReadAllText(Output)):new JArray();list.Add(JObject.FromObject(result));File.WriteAllText(Output,list.ToString());
        return Newtonsoft.Json.JsonConvert.SerializeObject(result);
    }
    static string PathOf(Transform t)=>t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
}
