/// 로비를 무엇으로 도는가.
///
/// 빌드는 언제나 <see cref="Steam"/>이다. 고를 수 있는 것은 에디터뿐이고, 고르는 자리는
/// 개발 콘솔의 「플랫폼」이다 — 프리팹 값으로 두면 MPPM 가상 플레이어마다 따로 고쳐야 한다.
public enum NetPlatform
{
    /// 스팀 매치메이킹. 실제 배포 경로다.
    Steam,

    /// 스팀 SDK를 아예 켜지 않고 에디터 창끼리만 붙는다. 창 하나가 계정 하나다.
    Editor,
}

/// 개발 콘솔이 고른 플랫폼.
///
/// EditorPrefs에 두는 이유는 두 가지다. 도메인 리로드와 재생을 넘어 남고, MPPM 가상
/// 플레이어도 같은 값을 읽는다 — EditorPrefs는 프로젝트가 아니라 사용자·에디터 버전
/// 단위라 창을 몇 개 띄우든 한 값이다.
public static class NetPlatformSetting
{
    const string Key = "BloodAndBeans.NetPlatform";

    /// 에디터 기본값은 <see cref="NetPlatform.Editor"/>다. 스팀을 기본으로 두면 스팀이
    /// 꺼져 있는 PC에서 방 흐름 전체가 막혀, 켜기만 하면 되는 로컬 테스트가 기본이 아니게 된다.
    public static NetPlatform Current =>
#if UNITY_EDITOR
        (NetPlatform)UnityEditor.EditorPrefs.GetInt(Key, (int)NetPlatform.Editor);
#else
        NetPlatform.Steam;
#endif

#if UNITY_EDITOR
    public static void Set(NetPlatform value) => UnityEditor.EditorPrefs.SetInt(Key, (int)value);
#endif
}
