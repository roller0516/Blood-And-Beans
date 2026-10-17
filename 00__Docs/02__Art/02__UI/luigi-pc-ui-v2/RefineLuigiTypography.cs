using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

/// 실제 시안의 두툼한 크림 글자 / 어두운 외곽 / 아래쪽 입체 깊이를 공용 TMP에 적용한다.
public static class RefineLuigiTypography
{
    const string FontRoot = "Assets/Art/Fonts/";
    const string PrefabRoot = "Assets/Art/UI/Prefabs/";
    public static string Main()
    {
        var shader=Shader.Find("TextMeshPro/Distance Field");
        if(shader==null)throw new InvalidOperationException("Installed TMP bevel shader missing");
        foreach(var role in new[]{"Title","Body","Number"})
        foreach(var tone in new[]{"Light","Ink"})
        {
            var mat=AssetDatabase.LoadAssetAtPath<Material>(FontRoot+"Luigi"+role+tone+".mat");
            if(mat==null)throw new InvalidOperationException("Missing shared typography material");
            bool light=tone=="Light", body=role=="Body";
            mat.shader=shader;
            mat.SetColor("_FaceColor",Color.white);
            mat.SetFloat("_FaceDilate",body?.025f:.07f);
            mat.SetFloat("_OutlineWidth",light?(body?.08f:.18f):.025f);
            mat.SetColor("_OutlineColor",light?new Color(.12f,.065f,.025f):new Color(.29f,.16f,.06f));
            mat.SetFloat("_OutlineSoftness",0);
            mat.SetColor("_UnderlayColor",light?new Color(.28f,.12f,.025f,1):new Color(.64f,.42f,.15f,.65f));
            mat.SetFloat("_UnderlayOffsetX",light?.12f:.05f);
            mat.SetFloat("_UnderlayOffsetY",light?(body?-.28f:-.65f):-.2f);
            mat.SetFloat("_UnderlayDilate",light?.12f:0);
            mat.SetFloat("_UnderlaySoftness",.025f);
            mat.EnableKeyword("OUTLINE_ON");mat.EnableKeyword("UNDERLAY_ON");
            if(light&&!body)
            {
                mat.EnableKeyword("BEVEL_ON");mat.SetFloat("_Bevel",.55f);
                mat.SetFloat("_BevelWidth",.3f);mat.SetFloat("_BevelOffset",.02f);
                mat.SetFloat("_LightAngle",3.8f);mat.SetFloat("_SpecularPower",.65f);
                mat.SetColor("_SpecularColor",new Color(1,.96f,.8f));
                mat.SetFloat("_Reflectivity",0);mat.SetFloat("_Ambient",.65f);
            }
            else mat.DisableKeyword("BEVEL_ON");
            EditorUtility.SetDirty(mat);
        }
        int count=0;
        foreach(var path in Directory.GetFiles(PrefabRoot,"*.prefab",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')))
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if(text.font==null||!text.font.name.StartsWith("Luigi",StringComparison.Ordinal))continue;
                    var ink=text.fontSharedMaterial!=null&&text.fontSharedMaterial.name.Contains("Ink");
                    var role=text.font.name.Contains("Number")?"Number":text.font.name.Contains("Title")?"Title":"Body";
                    // 긴 안내는 본문을 유지하고, 짧은 HUD / 탭 / 버튼은 시안의 둥근 표제 서체로 통일한다.
                    if(role=="Body"&&text.text.Length<=24&&!text.name.Contains("Description")&&!text.name.Contains("Desc")&&!text.name.Contains("Input"))role="Title";
                    if(text.name=="LuigiGameLogo")role="Number";
                    text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontRoot+"Luigi"+role+" SDF.asset");
                    text.fontSharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(FontRoot+"Luigi"+role+(ink?"Ink":"Light")+".mat");
                    text.color=Color.white;text.enableVertexGradient=true;
                    var top=ink?new Color(.26f,.15f,.065f):new Color(1,.985f,.89f);
                    var bottom=ink?new Color(.13f,.07f,.03f):new Color(1,.84f,.48f);
                    text.colorGradient=new VertexGradient(top,top,bottom,bottom);
                    text.extraPadding=true;EditorUtility.SetDirty(text);count++;
                    // 카드 이름의 색은 선택 상태를 소유한 원래 컴포넌트가 바꾼다.
                    var card=root.GetComponent<UICharacterCard>();
                    if(card!=null&&new SerializedObject(card).FindProperty("label").objectReferenceValue==text)
                    {text.enableVertexGradient=false;text.color=new Color(.19f,.13f,.08f);}
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();
        return "Shared bevel/outline/underlay and cream gradient applied to "+count+" TMP labels; body retained for long copy.";
    }
}
