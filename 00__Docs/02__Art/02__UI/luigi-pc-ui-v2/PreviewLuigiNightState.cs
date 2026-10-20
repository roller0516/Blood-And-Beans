using System;
using System.Linq;
using UnityEngine;
using UnityEditor;

/// 저장된 HUD의 표현 상태만 바꾼다. 선행: Play Mode에서 UIMatchHudScreen 로드.
/// 규칙·인벤토리·네트워크·PlayerPrefs에는 쓰지 않는다.
public static class PreviewLuigiNightState
{
    public static string Main(string state="normal")
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play Mode required");
        var ui=UIManager.Instance.CurrentScreen as UIMatchHudScreen;
        if(ui==null)throw new InvalidOperationException("Load existing UIMatchHudScreen first");
        bool buried=state=="buried",full=state=="full",cooldown=state=="cooldown";
        if(!new[]{"normal","buried","full","cooldown","long-prompt"}.Contains(state))throw new ArgumentException(state);
        ui.Render(new MatchHudModel{
            IsDay=false,Portrait=ResourceManager.Instance.CrewSprite(CharacterCatalog.All[0].Id),
            Day="1일차",PhaseName="야간 탐색",Timer="01:37",Team="팀 1",TeamColor=TeamColors.Of(0),
            Ping="",Details="",TeamRevenue=0,ShowBag=true,BagPercent=buried?"가방 없음":full?"가방 용량 230%":"가방 용량 45%",
            BagWeight=buried?"귀환 지점에서 빈 가방 재지급":full?"23.0 / 10 KG":"4.5 / 10 KG",
            BagRatio=buried?0:full?2.3f:.45f,BagBand=full?6:0,BagBuried=buried,
            ShowDash=true,DashKey="SPACE",DashRatio=cooldown?.75f:0,DashTime=cooldown?"6":"",
            Prompt=state=="long-prompt"?"[F] Tier 1 · 길게 눌러 열기":"[F] 가방 회수"});
        foreach(var slot in ui.GetComponentsInChildren<UISkillSlot>(true)){
            var isDash=slot.name.Contains("Dash");slot.SetLabel(isDash?"대시":CharacterCatalog.All[0].NightName);
            if(!isDash){slot.SetIcon(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Sprites/SkillIcon/Skill_Vampire_Night_BatSwarm.png"));slot.SetKey("Q");slot.SetCooldown(cooldown?.65f:0);slot.SetCount(cooldown?"8":"");}
        }
        ui.SetCastProgress(.65f);Canvas.ForceUpdateCanvases();
        var data=new SerializedObject(ui.Night);
        var icon=((RectTransform)data.FindProperty("bagIcon").objectReferenceValue).GetComponent<UnityEngine.UI.Image>();
        return Newtonsoft.Json.JsonConvert.SerializeObject(new{state,screen=new[]{Screen.width,Screen.height},bag=icon.sprite.name,
            percent=((TMPro.TMP_Text)data.FindProperty("bagPercentText").objectReferenceValue).text,source="Saved native prefabs; controlled UI inputs, prior Unity camera reference background"});
    }
}
