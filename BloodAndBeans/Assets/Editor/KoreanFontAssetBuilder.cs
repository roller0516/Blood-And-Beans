using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// Pretendard(SIL OFL 1.1)로 TMP 폰트 애셋을 만들고 프로젝트 기본 폰트로 세운다.
///
/// 이 단계를 손으로 하지 않는 이유는 TMP 기본 폰트 애셋(LiberationSans SDF)에 **한글
/// 글리프가 없기** 때문이다. legacy `Text`는 `LegacyRuntime.ttf`가 시스템 폰트로 폴백해
/// 한글이 나왔지만, TMP는 폰트 애셋에 없는 글자를 그리지 못한다. 폰트 애셋을 만들어
/// 기본값으로 걸지 않으면 UI의 모든 한글이 사라진다.
///
/// 아틀라스는 **Dynamic**이다. 한글은 완성형만 11,172자라 정적으로 구우면 아틀라스가
/// 수십 MB가 되고, 그러고도 빠진 글자는 여전히 안 나온다. Dynamic은 실제로 쓰인 글자만
/// 실행 중에 굽는다 — 대신 원본 폰트 파일이 프로젝트에 남아 있어야 한다.
public static class KoreanFontAssetBuilder
{
    const string SourceFontPath = "Assets/Art/Fonts/Pretendard-Regular.otf";
    const string FontAssetPath = "Assets/Art/Fonts/Pretendard-Regular SDF.asset";

    /// TMP 필수 리소스. 패키지 해시가 바뀌어도 이 가상 경로는 그대로다.
    const string EssentialsPackagePath =
        "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";

    // 굽기 품질. Font Asset Creator의 기본값과 같은 조합이다.
    const int SamplingPointSize = 90;
    const int AtlasPadding = 9;
    const int AtlasSize = 1024;

    [MenuItem("Tools/Blood & Beans/한글 TMP 폰트 애셋 만들기")]
    public static void Build()
    {
        // TMP 필수 리소스가 없으면 TMP_Settings 자체가 없어서 기본 폰트를 걸 곳이 없다.
        // 임포트는 비동기라 이번 호출 안에서 이어서 할 수 없다. 받아 두고 다시 부르게 한다.
        if (TMP_Settings.instance == null)
        {
            if (!File.Exists(Path.GetFullPath(EssentialsPackagePath)))
            {
                Debug.LogError("TMP 필수 리소스 패키지를 찾지 못했다. Window > TextMeshPro > "
                             + "Import TMP Essential Resources를 직접 실행한 뒤 다시 시도한다.");
                return;
            }

            Debug.Log("TMP 필수 리소스를 임포트한다. 끝나면 이 메뉴를 한 번 더 실행한다.");
            AssetDatabase.ImportPackage(EssentialsPackagePath, false);
            return;
        }

        var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (source == null)
        {
            Debug.LogError($"원본 폰트가 없다: {SourceFontPath}");
            return;
        }

        var fontAsset = TMP_FontAsset.CreateFontAsset(
            source, SamplingPointSize, AtlasPadding, GlyphRenderMode.SDFAA,
            AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);

        if (fontAsset == null)
        {
            Debug.LogError($"{source.name}으로 TMP 폰트 애셋을 만들지 못했다.");
            return;
        }

        fontAsset.name = Path.GetFileNameWithoutExtension(FontAssetPath);
        AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

        // 아틀라스 텍스처와 머티리얼은 폰트 애셋의 하위 애셋으로 들어가야 한다. 따로 두면
        // 폰트 애셋만 옮겼을 때 참조가 끊긴다 (Font Asset Creator와 같은 구성).
        foreach (var atlas in fontAsset.atlasTextures)
        {
            atlas.name = fontAsset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, fontAsset);
        }
        if (fontAsset.material != null)
        {
            fontAsset.material.name = fontAsset.name + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        SetAsProjectDefault(fontAsset);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"{FontAssetPath}를 만들고 TMP 기본 폰트로 세웠다.", fontAsset);
    }

    /// TMP_Settings의 기본 폰트를 갈아 끼운다. 직렬화 필드라 SerializedObject로 써야
    /// 애셋이 dirty로 표시되고 저장된다 — 프로퍼티에 대입만 하면 다음 리로드에 되돌아간다.
    static void SetAsProjectDefault(TMP_FontAsset fontAsset)
    {
        var settings = new SerializedObject(TMP_Settings.instance);
        var property = settings.FindProperty("m_defaultFontAsset");
        if (property == null)
        {
            Debug.LogWarning("TMP_Settings에서 m_defaultFontAsset을 찾지 못했다. "
                           + "Project Settings에서 직접 지정해야 한다.");
            return;
        }

        property.objectReferenceValue = fontAsset;
        settings.ApplyModifiedProperties();
        EditorUtility.SetDirty(TMP_Settings.instance);
    }
}
