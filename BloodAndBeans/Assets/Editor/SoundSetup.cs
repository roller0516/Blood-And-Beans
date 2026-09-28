using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

/// 셋업 스크립트가 만드는 AudioSource의 출력 그룹. 믹서 경로를 한 곳에 둔다.
static class SoundSetup
{
    const string MixerPath = "Assets/Art/Audio/BBMixer.mixer";
    const string SfxGroupName = "SFX";

    public static AudioMixerGroup SfxGroup() =>
        AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath).FindMatchingGroups(SfxGroupName)[0];

    const string AudioFolder = "Assets/Art/Audio/";
    const string ManagerPath = "Assets/Resources/GameManager.prefab";
    static readonly (SfxCue cue, string file)[] Clips =
    {
        (SfxCue.IngredientLiquid, "SFX_S01a_Ingredient_Liquid"),
        (SfxCue.IngredientHard, "SFX_S01b_Ingredient_Hard"),
        (SfxCue.IngredientSoft, "SFX_S01c_Ingredient_Soft"),
        (SfxCue.GaugeDing, "SFX_S04_GaugeDing"), (SfxCue.GaugeTick, "SFX_S05_GaugeTick"),
        (SfxCue.Perfect, "SFX_S06_Perfect"), (SfxCue.Good, "SFX_S07_Good"),
        (SfxCue.Miss, "SFX_S08_Miss"), (SfxCue.Sale, "SFX_S10_Sale"),
        (SfxCue.Coin, "SFX_S11_Coin"), (SfxCue.Misdelivery, "SFX_S12_Misdelivery_TEMP"),
        (SfxCue.Spoiled, "SFX_S14_Spoiled"), (SfxCue.HandOff, "SFX_S16_HandOff"),
        (SfxCue.GemExpire, "SFX_S26_GemExpire"), (SfxCue.Bump, "SFX_S27_Bump"),
        (SfxCue.BloodBean, "SFX_S33_BloodBean"), (SfxCue.Gem, "SFX_S34_Gem"),
        (SfxCue.Dash, "SFX_S37_Dash"), (SfxCue.BloodBeanPour, "SFX_S58_BloodBeanPour"),
        (SfxCue.NotAllowed, "SFX_S62_NotAllowed"),
    };

    /// 제작 원본을 임포트하고 기존 GameManager의 효과음 표만 갱신한다.
    [MenuItem("Blood & Beans/임시 SFX 연결")]
    public static void ApplySfx()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("플레이 종료 후 적용한다.");
        var source = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,
            "../..", "00__Docs/03__Sound/SFX"));
        foreach (var entry in Clips)
        {
            var path = AudioFolder + entry.file + ".wav";
            System.IO.File.Copy(System.IO.Path.Combine(source, entry.file + ".wav"), path, true);
            AssetDatabase.ImportAsset(path);
        }
        var root = PrefabUtility.LoadPrefabContents(ManagerPath);
        try
        {
            var manager = root.GetComponentInChildren<SoundManager>(true);
            if (manager == null) throw new System.InvalidOperationException("GameManager에 SoundManager가 없다.");
            var data = new SerializedObject(manager);
            var cues = data.FindProperty("cues");
            cues.arraySize = Clips.Length;
            for (var i = 0; i < Clips.Length; i++)
            {
                var entry = cues.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("cue").intValue = (int)Clips[i].cue;
                entry.FindPropertyRelative("clip").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + Clips[i].file + ".wav");
                entry.FindPropertyRelative("volume").floatValue = 0.5f;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, ManagerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
