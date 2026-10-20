using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// 현재 열린 실제 UI의 기능/배치를 확인한다. 시안 일치 판정이나 네트워크 테스트가 아니다.
public static class VerifyLuigiReportedUI
{
    static T Ref<T>(UnityEngine.Object o,string f)where T:UnityEngine.Object=>new SerializedObject(o).FindProperty(f).objectReferenceValue as T;
    public static string Settings(){var ui=UnityEngine.Object.FindAnyObjectByType<UISettingsPopup>();if(ui==null)throw new Exception("Open settings first");var row=ui.GetComponentsInChildren<UIVolumeSlider>(false)[0];var input=row.GetComponentInChildren<TMP_InputField>();var slider=row.GetComponentInChildren<Slider>();var original=slider.value;slider.value=.37f;var follows=input.text=="37";input.onEndEdit.Invoke("125");var clamps=Mathf.Approximately(slider.value,1)&&input.text=="100";input.onEndEdit.Invoke("42");var typed=Mathf.Approximately(slider.value,.42f);slider.value=original;
        var toggle=Ref<Toggle>(ui,"fullscreen");var wasOn=toggle.isOn;var screenBefore=Screen.fullScreen;toggle.isOn=false;var off=Ref<Image>(ui,"fullscreenFace").color;toggle.isOn=true;var on=Ref<Image>(ui,"fullscreenFace").color;toggle.isOn=wasOn;
        var look=Ref<GameObject>(ui,"lookRow");var wasVisible=look.activeSelf;look.SetActive(true);Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(look.transform.parent as RectTransform);var last=look.transform.parent.GetChild(look.transform.parent.childCount-1) as RectTransform;var corners=new Vector3[4];last.GetWorldCorners(corners);var bottom=corners[0].y;var apply=Ref<Button>(ui,"applyButton");((RectTransform)apply.transform).GetWorldCorners(corners);var top=corners[1].y;var gap=bottom-top;look.SetActive(wasVisible);Canvas.ForceUpdateCanvases();
        var data=new{sliderUpdatesNumber=follows,numberUpdatesSlider=typed,numberClampsAt100=clamps,toggleChangesFace=off!=on,fullscreenAppliedBeforeApply=Screen.fullScreen!=screenBefore,fiveRowsClearApplyButton=gap>=8,fiveRowGap=gap};if(!follows||!clamps||!typed||off==on||gap<8)throw new Exception(Newtonsoft.Json.JsonConvert.SerializeObject(data));return Save("settings",data);}
    public static string Prompt(){var hud=UIManager.Instance.CurrentScreen as UIMatchHudScreen;Canvas.ForceUpdateCanvases();var text=Ref<TMP_Text>(hud,"promptText");text.ForceMeshUpdate();var key=Ref<RectTransform>(hud,"promptKey");var panel=Ref<RectTransform>(hud,"promptBox");var data=new{text=text.text,width=panel.rect.width,textWidth=text.rectTransform.rect.width,preferred=text.preferredWidth,overflow=text.isTextOverflowing,keyRight=key.anchoredPosition.x+key.rect.xMax,textLeft=text.rectTransform.anchoredPosition.x+text.rectTransform.rect.xMin};if(text.isTextOverflowing||data.textLeft-data.keyRight<10||text.rectTransform.rect.width+.1f<text.preferredWidth)throw new Exception(Newtonsoft.Json.JsonConvert.SerializeObject(data));return Save("long-prompt",data);}
    public static string Connections(){var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Character/Prefabs/Player.prefab");var plate=player.GetComponentInChildren<UIPlayerNameplate>(true);var dot=Ref<Graphic>(plate,"background");var panel=Ref<Graphic>(plate,"plate");if(dot.name!="Dot"||panel.name!="LuigiSurface"||Ref<PlayerTeam>(plate,"team")==null)throw new Exception("Nested player nameplate wiring mismatch");return Save("player-prefab",new{teamDot=dot.name,plate=panel.name,arrow=Ref<GameObject>(plate,"selfArrow").name,teamConnected=true});}
    static string Save(string state,object result){var path=Path.GetFullPath("../00__Docs/02__Art/02__UI/luigi-pc-ui-v2/light-ui-preview/native-captures/reported-check-"+state+".json");var json=Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented);File.WriteAllText(path,json);return json;}
}
