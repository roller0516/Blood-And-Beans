using DG.Tweening;
using UnityEngine;
using UnityEngine.Audio;

/// 볼륨 버스. 믹서의 노출 파라미터와 1:1이다.
public enum SoundBus { Master, Bgm, Sfx }

/// 배경음 자리. 곡은 SoundManager 프리팹이 갖는다.
public enum Bgm { Title, Lobby, Day, Night }

/// 배경음·2D 효과음 재생과 버스 볼륨의 주인. 볼륨의 원천은 믹서 하나다.
///
/// 3D 효과음은 여기로 오지 않는다. 카페 사건은 방향과 거리가 정보라서 (기획서 5.7.6)
/// 월드 오브젝트가 자기 AudioSource를 들고 출력만 SFX 그룹으로 보낸다.
///
/// GameManager 프리팹의 자식이다. 타이틀에서 매치로 넘어가도 배경음이 끊기지 않는다.
public sealed class SoundManager : PersistentMonoSingleton<SoundManager>
{
    [SerializeField] AudioMixer mixer;
    [SerializeField] AudioSource bgmSource;
    [SerializeField] AudioSource sfxSource;

    [Header("배경음")]
    [SerializeField] float bgmFadeSeconds = 0.5f;
    [SerializeField] AudioClip title;
    [SerializeField] AudioClip lobby;
    [SerializeField] AudioClip[] day;
    [SerializeField] AudioClip[] night;

    [System.Serializable]
    sealed class CueClip
    {
        public SfxCue cue;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.5f;
        [System.NonSerialized] public AudioSource[] voices;
        [System.NonSerialized] public int next;
    }
    [Header("임시 효과음")]
    [SerializeField] CueClip[] cues = System.Array.Empty<CueClip>();
    [SerializeField] float sfxMinDistance = 3f;
    [SerializeField] float sfxMaxDistance = 40f;
    [SerializeField] float saleCoinDelay = 0.3f;
    // 효과음 리스트 0.2: 같은 클립은 3개, 자기 팀은 +3dB.
    const int VoicesPerClip = 3;
    const float OwnTeamGain = 1.4125375f;

    /// 복제된 사건의 위치에서 재생한다. 같은 클립의 네 번째 소리는 가장 오래된 것을 끊는다.
    public void PlayCue(SfxCue cue, Vector3 position, int team = -1, bool spatial = true,
        float volume = 1f, float pitch = 1f)
    {
        if (cue == SfxCue.None) return;
        var coinVolume = 1f;
        var sale = cue == SfxCue.Sale || cue == SfxCue.BurntSale;
        if (cue == SfxCue.PartialSale) { cue = SfxCue.Sale; volume *= 0.3f; }
        if (cue == SfxCue.BurntSale) { cue = SfxCue.Sale; pitch *= 0.74f; coinVolume = 0.5f; }
        foreach (var entry in cues)
        {
            if (entry.cue != cue || entry.clip == null) continue;
            if (entry.voices == null)
            {
                entry.voices = new AudioSource[VoicesPerClip];
                for (var i = 0; i < entry.voices.Length; i++)
                {
                    // sfxSource를 Instantiate하면 같은 오브젝트의 SoundManager까지 복제되고,
                    // 그 복제본은 싱글턴 중복으로 스스로 파괴된다. 소스만 가진 빈 오브젝트를 만든다.
                    var voice = new GameObject(entry.clip.name).AddComponent<AudioSource>();
                    voice.transform.SetParent(transform, false);
                    voice.outputAudioMixerGroup = sfxSource.outputAudioMixerGroup;
                    voice.playOnAwake = false;
                    voice.loop = false;
                    voice.rolloffMode = AudioRolloffMode.Linear;
                    voice.minDistance = sfxMinDistance;
                    voice.maxDistance = sfxMaxDistance;
                    entry.voices[i] = voice;
                }
            }
            var source = entry.voices[entry.next];
            entry.next = (entry.next + 1) % entry.voices.Length;
            source.Stop();
            source.transform.position = position;
            source.spatialBlend = spatial ? 1f : 0f;
            source.volume = entry.volume * volume * (team >= 0 && team == PlayerTeam.Local() ? OwnTeamGain : 1f);
            source.pitch = pitch;
            source.clip = entry.clip;
            source.Play();
            if (sale)
                DOVirtual.DelayedCall(saleCoinDelay,
                    () => PlayCue(SfxCue.Coin, position, team, spatial, volume * coinVolume)).SetLink(gameObject);
            return;
        }
        CDebug.LogWarning($"{name}: {cue} 효과음 클립이 연결되지 않았다.", this);
    }

    /// 믹서 에셋의 노출 파라미터 이름과 저장 키. `SoundBus` 순서와 같다.
    static readonly string[] ExposedParams = { "MasterVolume", "BGMVolume", "SFXVolume" };
    static readonly string[] PrefsKeys = { "volume.master", "volume.bgm", "volume.sfx" };

    /// 선형 0은 로그로 -무한대라 믹서 바닥(-80dB)에서 자른다.
    const float MutedDb = -80f;
    const float DefaultVolume = 1f;

    /// 저장된 볼륨을 믹서에 넣는다.
    void Start()
    {
        for (var i = 0; i < ExposedParams.Length; i++) Apply((SoundBus)i, GetVolume((SoundBus)i));
    }

    /// 0~1 선형 값. 설정 팝업 슬라이더와 같은 단위다.
    public float GetVolume(SoundBus bus) => PlayerPrefs.GetFloat(PrefsKeys[(int)bus], DefaultVolume);

    public void SetVolume(SoundBus bus, float linear)
    {
        linear = Mathf.Clamp01(linear);
        PlayerPrefs.SetFloat(PrefsKeys[(int)bus], linear);
        Apply(bus, linear);
    }

    /// 슬라이더는 선형인데 귀는 로그로 듣는다. 그대로 넣으면 앞쪽 10%에서 소리가 거의 다 사라진다.
    void Apply(SoundBus bus, float linear) =>
        mixer.SetFloat(ExposedParams[(int)bus],
            linear > 0f ? Mathf.Max(Mathf.Log10(linear) * 20f, MutedDb) : MutedDb);

    /// 같은 곡이면 이어서 튼다. `round`는 몇 번째 낮·밤인지다.
    public void PlayBgm(Bgm kind, int round = 0)
    {
        var clip = Pick(kind, round);
        if (clip == null || bgmSource.clip == clip) return;

        bgmSource.DOKill();
        if (!bgmSource.isPlaying) { StartBgm(clip); return; }
        bgmSource.DOFade(0f, bgmFadeSeconds).OnComplete(() => StartBgm(clip));
    }

    void StartBgm(AudioClip clip)
    {
        bgmSource.clip = clip;
        bgmSource.volume = 0f;
        bgmSource.Play();
        bgmSource.DOFade(1f, bgmFadeSeconds);
    }

    AudioClip Pick(Bgm kind, int round) => kind switch
    {
        Bgm.Title => title,
        Bgm.Lobby => lobby,
        Bgm.Day => Cycle(day, round),
        Bgm.Night => Cycle(night, round),
        _ => null,
    };

    // ponytail: 기획서에 일차별 곡 배정이 없다. 곡(6)이 일수(7)보다 적어 돌려 쓴다. 배정이 정해지면 그 표로 옮긴다
    static AudioClip Cycle(AudioClip[] clips, int round) =>
        clips == null || clips.Length == 0 ? null : clips[round % clips.Length];

    /// 2D 효과음 (UI · 알림). 위치가 정보인 소리는 월드 오브젝트의 AudioSource로 낸다.
    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (clip != null) sfxSource.PlayOneShot(clip, volume);
    }
}
