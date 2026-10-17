using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
public static class ValidateUIProductionReview
{
    public static string Main()
    {
        const string kit="Assets/Art/UI/PrototypeKit";
        var rows=new List<string>();
        foreach(var name in new[]{"UIRecipeClipboardReview","UIMakingClipboardReview","UISettlementClipboardReview"})
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(kit+"/"+name+".prefab");
            if(root==null||PrefabUtility.GetPrefabAssetType(root)!=PrefabAssetType.Variant)throw new Exception("Not a base prefab variant: "+name);
            var original=PrefabUtility.GetCorrespondingObjectFromSource(root);
            if(AssetDatabase.GetAssetPath(original)!=kit+"/UIClipboardBase.prefab")throw new Exception("Incorrect shared base: "+name);
            foreach(var b in root.GetComponentsInChildren<Button>(true))
            {
                var source=PrefabUtility.GetCorrespondingObjectFromSource(b.gameObject);
                if(AssetDatabase.GetAssetPath(source)!=kit+"/UIActionButton.prefab")throw new Exception("Button is not nested shared instance");
            }
            foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))if(text.font==null)throw new Exception("Missing font");
            foreach(var image in root.GetComponentsInChildren<Image>(true))if(image.sprite==null)throw new Exception("Missing sprite");
            if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root)>0)throw new Exception("Missing script");
            rows.Add(name+": Variant of UIClipboardBase, nested shared button, populated fonts/sprites");
        }
        var bAsset=AssetDatabase.LoadAssetAtPath<GameObject>(kit+"/UIActionButton.prefab").GetComponent<Button>();
        if(bAsset.transition!=Selectable.Transition.SpriteSwap||bAsset.spriteState.disabledSprite==null||bAsset.spriteState.highlightedSprite==null)throw new Exception("Incomplete button states");
        var dependencies=AssetDatabase.GetDependencies(kit+"/UIRecipeClipboardReview.prefab",true);
        if(dependencies.Contains("Assets/Art/Fonts/Pretendard-Regular SDF.asset"))throw new Exception("Review must not modify original dynamic font");
        var data="{\"passed\":true,\"variantCount\":3,\"sharedButtonStateSprites\":true,\"independentFontFallback\":true,\"gameIntegration\":false,\"checks\":["+string.Join(",",rows.Select(x=>"\""+x+"\""))+"]}";
        File.WriteAllText(Path.GetFullPath("../00__Docs/02__Art/02__UI/luigi-pc-ui-v2/ui-production-unity-validation.json"),data);
        return data;
    }
}
