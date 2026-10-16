using UnityEditor;
using UnityEngine.Audio;

/// 셋업 스크립트가 만드는 AudioSource의 출력 그룹. 믹서 경로를 한 곳에 둔다.
static class SoundSetup
{
    const string MixerPath = "Assets/Art/Audio/BBMixer.mixer";
    const string SfxGroupName = "SFX";

    public static AudioMixerGroup SfxGroup() =>
        AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath).FindMatchingGroups(SfxGroupName)[0];
}
