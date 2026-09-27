using UnityEngine;

/// 카페 입구에 달린 팀 색 등 (기획서 5.7.6 · 낮 오브젝트 도해 O-P06).
///
/// 광장에 나가 있는 동안 손님 상태를 알 수단은 이 등과 소리 둘뿐이다 — 주문 목록도
/// 팀원 패널도 두지 않기로 했다. 등은 **전원에게** 보이고, 소리는 자기 팀에게만 들린다.
/// **몇 명이 위급한지는 말하지 않는다.** 깜빡임은 "뭔가 급하다"까지다.
///
/// 카페 외관(`CafeShell`)과 함께 피어마다 로컬로 선다 — 복제하지 않는다. 남의 카페
/// 손님은 복제되지 않으므로(3.4) 위급 여부는 공개 판(`Scoreboard`)에서 읽는다.
public class CafeEntranceLight : MonoBehaviour
{
    [SerializeField] Renderer lamp;

    /// 위급할 때 우는 알람. 카페 방향에서 들려야 하므로 등에 붙은 3D 소스다.
    [SerializeField] AudioSource alarm;

    [SerializeField, Min(0.1f)] float blinksPerSecond = 1.6f;

    /// 평소 밝기. 0이면 꺼져 보여서 카페 입구를 못 찾는다 (5.4.3-2).
    [SerializeField, Range(0f, 1f)] float calmBrightness = 0.45f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    int team = -1;
    Scoreboard board;
    MaterialPropertyBlock block;
    float applied = -1f;

    /// 카페 껍데기를 세우는 쪽이 팀을 찍어 준다 (`MatchDirector.SpawnCafeShells`).
    /// 껍데기는 복제되지 않으므로 팀을 스스로 알 방법이 없다.
    public void Bind(int value)
    {
        team = value;
        applied = -1f;
    }

    void Awake()
    {
        if (lamp != null) return;
        CDebug.LogError($"{name}: {nameof(lamp)}가 비어 있다. 이 카페 입구는 팀 색으로 "
                      + "빛나지 않고 위급도 알리지 못한다 (기획서 5.7.6).", this);
        enabled = false;
    }

    void Update()
    {
        if (team < 0) return;

        // 판은 매치 씬이 서면서 스폰된다. 찾은 뒤에는 다시 찾지 않는다.
        if (board == null)
        {
            board = MatchDirector.Instance != null ? MatchDirector.Instance.Board : null;
            if (board == null) return;
        }

        var urgent = board.UrgentOf(team);
        var brightness = urgent
            ? Mathf.Lerp(calmBrightness, 1f, Mathf.PingPong(Time.time * blinksPerSecond * 2f, 1f))
            : calmBrightness;

        if (!Mathf.Approximately(brightness, applied))
        {
            applied = brightness;
            block ??= new MaterialPropertyBlock();
            var color = TeamColors.Of(team) * brightness;
            color.a = 1f;                          // 곱하면 알파까지 줄어 반투명 셰이더에서 사라진다
            block.Clear();
            block.SetColor(BaseColorId, color);
            lamp.SetPropertyBlock(block);
        }

        if (alarm == null || alarm.clip == null) return;
        var mine = urgent && PlayerTeam.Local() == team;
        if (mine == alarm.isPlaying) return;
        if (mine) alarm.Play(); else alarm.Stop();
    }
}
