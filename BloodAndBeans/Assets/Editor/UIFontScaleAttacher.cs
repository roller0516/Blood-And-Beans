using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// 화면 프리팹 루트에 <see cref="UIFontScale"/>를 붙인다.
///
/// 글자 배율은 테마 애셋이 정하지만(<see cref="UIThemeConfig.FontScale"/>), 그 값을
/// 실제로 먹이는 것은 화면마다 하나씩 있는 이 컴포넌트다. 화면이 늘 때마다 손으로
/// 붙이는 것을 잊으면 그 화면만 글자가 작게 남으므로, 폴더를 훑어 한 번에 맞춘다.
///
/// 여러 번 돌려도 안전하다 — 이미 붙어 있으면 건너뛴다.
public static class UIFontScaleAttacher
{
    const string MenuPath = "Tools/Blood & Beans/화면 프리팹에 글자 배율 붙이기";

    /// 화면 프리팹이 모여 있는 곳. 여기 있는 프리팹은 전부 화면이나 팝업이다.
    const string ScreenPrefabFolder = "Assets/Prefabs/UI";

    [MenuItem(MenuPath)]
    public static void AttachAll()
    {
        var changed = new List<string>();
        var skipped = new List<string>();

        // 폴더에 실제로 있는 것만 다룬다. 목록을 코드에 적어 두면 화면이 늘 때마다 어긋난다.
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { ScreenPrefabFolder });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);

            // 프리팹 애셋의 인스턴스를 열어 고친다. 애셋에 직접 AddComponent하면
            // 저장되지 않거나 임포터와 어긋난다.
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<UIFontScale>() != null)
                {
                    skipped.Add(path);
                    continue;
                }

                root.AddComponent<UIFontScale>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                changed.Add(path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        CDebug.Log($"[UIFontScaleAttacher] 붙임 {changed.Count} · 이미 있음 {skipped.Count}");
        foreach (var path in changed) CDebug.Log($"  + {path}");
        foreach (var path in skipped) CDebug.Log($"  = {path}");
    }
}
