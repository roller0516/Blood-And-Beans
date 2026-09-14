using Unity.Cinemachine;
using UnityEngine;

/// 캐릭터 선택창의 무대. 방 인원만큼 모델을 세우고 머리 위 앵커를 잡아 둔다.
///
/// `UI` 접두사를 붙이지 않는 이유는 이 클래스가 uGUI를 소유하지 않기 때문이다 — 3D
/// 오브젝트만 다루고, 네임플레이트를 그리는 것은 화면 쪽 <see cref="UICharacterNameplate"/>다.
///
/// **캐릭터만 그리는 카메라를 따로 둔다.** 화면의 UI는 Overlay 캔버스라 어느 카메라보다
/// 위에 그려지므로, 캐릭터는 그냥 이 카메라가 그리면 자동으로 UI 뒤에 선다 — 카메라 스택도
/// 캔버스 평면 계산도 필요 없다.
///
/// 전용 레이어를 쓰는 이유는 캐릭터에만 조명을 물리고, 모델 크기가 제각각 들어와도
/// 배경 구도를 건드리지 않고 프레이밍을 고치기 위해서다. 월드와 멀리 떨어뜨려 세운다.
public sealed class CharacterStage : MonoBehaviour
{
    /// 무대에 설 수 있는 최대 인원. `MaxTeams × PlayersPerTeam`의 상한이다.
    ///
    /// ponytail: 기획서에 방 정원 상한 표가 없다. 4팀 × 2인으로 잡은 값이고, 정원이
    /// 확정되면 `MatchSeating`이 주는 값으로 갈아 끼운다.
    public const int MaxSeats = 8;

    /// 내 캐릭터가 서는 자리. **어느 클라이언트에서나 같다.**
    ///
    /// 명단 순서대로 앉히면 같은 사람이 내 화면에서는 0번, 짝꿍 화면에서는 1번이 된다.
    /// 그러면 "조명 아래가 나"라는 것이 성립하지 않는다 — 스포트도 카메라도 이 자리에
    /// 고정이라, 자리를 사람마다 다르게 주면 남의 화면에서 내가 어둠 속에 선다.
    public const int SelfSeat = 0;

    [SerializeField] CharacterVisualConfig visuals;

    [Tooltip("모델이 서는 자리. 무대 프리팹에 미리 깔아 둔다 — 상한이 고정이라 런타임에 만들 이유가 없다.")]
    [SerializeField] Transform[] seats = new Transform[0];

    [Tooltip("캐릭터를 그리는 카메라. 네임플레이트 좌표 변환도 이 카메라를 쓴다. CinemachineBrain이 붙어 있다.")]
    [SerializeField] Camera stageCamera;

    [Tooltip("내 캐릭터를 따라가는 가상 카메라. 브레인이 이것을 따른다.")]
    [SerializeField] CinemachineCamera stageVCam;

    [Tooltip("자리 바닥에서 이름표까지의 높이 (월드 단위). 모든 자리가 같은 높이를 쓴다.")]
    [SerializeField] float nameplateHeight = 2.1f;

    [Tooltip("자리 사이 간격 (월드 단위). 8명이 카메라 화각에 들어오는 값이어야 한다.")]
    [SerializeField] float seatSpacing = 1.35f;

    [Tooltip("내 자리를 비추는 스포트. 이 x가 곧 내 자리이고 줄 전체의 기준점이다.")]
    [SerializeField] Transform stageSpot;

    [Header("스크롤 (시안 D안)")]
    [Tooltip("켜면 카메라가 내 자리를 놓고 좌우로 움직인다. 두 시안을 비교하려고 둔 옵션이다.")]
    [SerializeField] bool scrollMode;

    [Tooltip("스크롤일 때의 자리 간격. 전원을 같은 크기로 세우므로 1안보다 넓다.")]
    [SerializeField] float scrollSeatSpacing = 2.4f;

    [Tooltip("스크롤 속도 (초당 월드 단위).")]
    [SerializeField] float scrollSpeed = 6f;

    /// 프리팹에 저장된 자리 위치. <see cref="Layout"/>은 x만 옮기고 y·z는 이것을 지킨다 —
    /// 세트 안에서 어디에 서는지는 씬을 만든 사람이 정한 것이다.
    Vector3[] seatHome;

    /// 자리마다 지금 서 있는 모델과 그것이 무엇이었는지. 같은 캐릭터면 다시 세우지 않는다 —
    /// 로비 콜백 하나당 한 번씩 다시 그리므로 매번 부수면 한 프레임에 두 번 스폰이 돈다.
    readonly GameObject[] spawned = new GameObject[MaxSeats];
    readonly int[] spawnedCharacter = new int[MaxSeats];

    public Camera StageCamera => stageCamera;

    /// 무대가 실제로 세울 수 있는 자리 수. 프리팹이 깔아 둔 만큼이다.
    public int SeatCount => seats != null ? Mathf.Min(seats.Length, MaxSeats) : 0;

    void Awake()
    {
        for (var i = 0; i < spawnedCharacter.Length; i++)
            spawnedCharacter[i] = CharacterCatalog.NoPick;

        seatHome = new Vector3[SeatCount];
        for (var i = 0; i < SeatCount; i++) seatHome[i] = seats[i].localPosition;

        if (visuals == null)
            CDebug.LogError($"{name}: CharacterVisualConfig가 비었다. 무대에 아무도 서지 않는다.", this);

        // 배경은 테마 색으로 지운다. 캔버스에 배경 판을 깔면 그 판이 캐릭터를 가린다 —
        // 캔버스는 한 평면이라 캐릭터를 UI 사이에 끼울 수 없다.
        if (stageCamera != null) stageCamera.backgroundColor = UITheme.Ink;
    }

    /// 화면이 열리고 닫힐 때. 카메라가 켜져 있는 동안 이 화면이 화면 전체를 그린다.
    public void SetActive(bool value)
    {
        if (stageCamera != null) stageCamera.enabled = value;
        if (gameObject.activeSelf != value) gameObject.SetActive(value);
    }

    /// 가상 카메라가 이 자리를 따라간다. 방에 들어온 사람이 늘어도 **내 캐릭터가 화면
    /// 기준점에 머문다** — 줄 전체의 가운데로 카메라가 끌려가지 않는다.
    ///
    /// 자리 트랜스폼을 주는 이유는 모델보다 오래 살기 때문이다. 캐릭터를 바꾸면 모델은
    /// 부서졌다 다시 생기는데, 그때마다 추적 대상이 끊기면 카메라가 튄다.
    public void FocusSeat(int seat)
    {
        if (stageVCam == null || seat < 0 || seat >= SeatCount) return;

        // 스크롤일 때는 추적을 끊는다. 붙여 두면 내 자리가 카메라를 도로 끌어당겨
        // 아무리 밀어도 제자리로 돌아온다 — 그러면 스크롤이 없는 것과 같다.
        if (scrollMode)
        {
            if (stageVCam.Follow != null) stageVCam.Follow = null;

            // 추적을 끊으면 카메라는 프리팹에 저장된 자리에 선다. 처음 한 번만 내 자리로
            // 옮겨 거기서부터 밀게 한다 — 로비가 갱신될 때마다 옮기면 스크롤하던 화면이
            // 계속 내 자리로 튕겨 돌아온다.
            if (!scrollAnchored)
            {
                scrollAnchored = true;
                var p = stageVCam.transform.localPosition;
                stageVCam.transform.localPosition =
                    new Vector3(seats[seat].localPosition.x, p.y, p.z);
            }
            return;
        }

        if (stageVCam.Follow == seats[seat]) return;
        stageVCam.Follow = seats[seat];
    }

    /// 지금 D안(스크롤)으로 서 있는가.
    public bool ScrollMode => scrollMode;

    /// 스크롤로 넘어오면서 카메라를 내 자리에 한 번 맞췄는가. <see cref="FocusSeat"/> 참고.
    bool scrollAnchored;

    /// 두 시안을 오가며 비교하기 위한 전환. 간격이 달라지므로 줄을 다시 편다.
    public void SetScrollMode(bool value)
    {
        if (scrollMode == value) return;
        scrollMode = value;

        // 다시 스크롤로 들어올 때 또 한 번 내 자리에서 시작한다.
        scrollAnchored = false;

        Layout();
        FocusSeat(SelfSeat);

        // 1안으로 돌아갈 때 카메라를 원점으로 되돌린다. 밀어 둔 자리에 그대로 두면
        // 추적이 다시 붙는 순간 화면이 크게 튄다.
        if (!scrollMode && stageVCam != null)
        {
            var p = stageVCam.transform.localPosition;
            stageVCam.transform.localPosition = new Vector3(SelfAnchorX, p.y, p.z);
        }
    }

    /// 카메라를 좌우로 민다. <paramref name="axis"/>는 -1~1, <paramref name="deltaTime"/>는 프레임 간격이다.
    ///
    /// 서 있는 사람의 양 끝을 넘지 못하게 자른다 — 아무도 없는 무대 밖으로 밀려나면
    /// 화면에 빈 세트만 남고 돌아올 방법이 눈에 보이지 않는다.
    public void Pan(float axis, float deltaTime)
    {
        if (!scrollMode || stageVCam == null || axis == 0f) return;

        // 자리 여덟 칸을 훑는다. 상한이 고정이고 탐색·조회가 없어 프레임마다 돌아도 된다.
        var min = float.MaxValue;
        var max = float.MinValue;
        for (var i = 0; i < SeatCount; i++)
        {
            if (spawned[i] == null) continue;
            var x = seats[i].localPosition.x;
            if (x < min) min = x;
            if (x > max) max = x;
        }
        if (min > max) return;

        var pos = stageVCam.transform.localPosition;
        pos.x = Mathf.Clamp(pos.x + axis * scrollSpeed * deltaTime, min, max);
        stageVCam.transform.localPosition = pos;
    }

    /// 줄 전체의 기준점. 조명이 박힌 x이며, 없으면 내 자리의 원래 x다.
    ///
    /// `seatHome`은 Awake에서 채워지므로 그 전에는 조명만 답이 된다.
    float SelfAnchorX
    {
        get
        {
            if (stageSpot != null) return stageSpot.localPosition.x;
            if (seatHome == null || seatHome.Length == 0) return 0f;
            return seatHome[Mathf.Clamp(SelfSeat, 0, seatHome.Length - 1)].x;
        }
    }

    /// 자리를 <see cref="SelfSeat"/> 기준으로 **좌우 양쪽으로** 편다.
    ///
    /// **인원 수를 보지 않는다.** 인원에 맞춰 다시 모으면 사람이 들고 날 때마다 내 자리가
    /// 옮겨 다니고, 그러면 고정된 스포트 아래에서 벗어난다. 대신 좌우로 번갈아 채워서
    /// 몇 명이 오든 내가 한가운데에 남는다.
    public void Layout()
    {
        if (seatHome == null) return;

        // 기준은 조명이다. 스포트가 박힌 x에 내 자리가 서고 나머지가 옆으로 밀린다 —
        // 조명을 옮기면 줄도 따라오므로 맞출 곳이 한 군데뿐이다.
        var anchorX = SelfAnchorX;

        // D안은 전원을 같은 크기로 세우고 화면 밖으로 넘기는 것이 전제라 간격이 넓다.
        var spacing = scrollMode ? scrollSeatSpacing : seatSpacing;

        for (var i = 0; i < SeatCount; i++)
            seats[i].localPosition = new Vector3(
                anchorX + SideStepOf(i) * spacing, seatHome[i].y, seatHome[i].z);
    }

    /// 내 자리에서 몇 칸 떨어져 서는가. 한쪽으로만 늘어놓으면 사람이 늘수록 내가 줄의
    /// 끝으로 밀린다 — 좌우로 번갈아 붙여서 내가 언제나 한가운데다.
    ///
    /// 0 → 0, 1 → +1, 2 → -1, 3 → +2, 4 → -2 …
    static int SideStepOf(int seat)
    {
        var n = seat - SelfSeat;
        if (n == 0) return 0;

        var step = (Mathf.Abs(n) + 1) / 2;
        return n % 2 != 0 ? step : -step;
    }

    /// 자리 하나를 이 캐릭터로 채운다. 같은 캐릭터가 이미 서 있으면 아무것도 하지 않는다.
    ///
    /// `character`가 `CharacterCatalog.NoPick`이면 자리를 비운다 — 아직 안 고른 사람이다.
    public void SetSeat(int seat, int character)
    {
        if (seat < 0 || seat >= SeatCount) return;
        if (spawnedCharacter[seat] == character) return;

        if (spawned[seat] != null) Destroy(spawned[seat]);
        spawned[seat] = null;
        spawnedCharacter[seat] = character;

        if (!CharacterCatalog.IsValid(character) || visuals == null) return;

        spawned[seat] = visuals.SpawnModel(CharacterCatalog.All[character].Id, seats[seat], gameObject.layer);
    }

    public void SetTeam(int seat, int team)
    {
        if (seat < 0 || seat >= SeatCount || spawned[seat] == null) return;
        var appearance = spawned[seat].GetComponent<CharacterModel>();
        if (appearance != null) appearance.Tint(TeamColors.Of(team), 1f);
    }

    /// 자리 수를 넘는 칸을 비운다. 사람이 나가면 그 자리 모델도 사라져야 한다.
    public void ClearFrom(int seat)
    {
        for (var i = Mathf.Max(seat, 0); i < SeatCount; i++) SetSeat(i, CharacterCatalog.NoPick);
    }

    /// 그 자리 이름표의 화면 좌표. 무대가 정지 화면이라 명단이 바뀔 때만 부른다.
    ///
    /// 모델 높이를 재지 않고 자리마다 같은 높이를 쓴다. `SkinnedMeshRenderer.bounds`는
    /// 스폰 직후 뼈가 잡히기 전이라 엉뚱한 값을 주고, 종류마다 이름표 높이가 들쭉날쭉한
    /// 것보다 한 줄로 맞는 편이 읽기 좋다.
    ///
    /// **자리를 보고 답한다. 모델이 서 있는지는 묻지 않는다.** 아직 캐릭터를 안 고른
    /// 사람은 자리가 비는데, 그때 실패로 답하면 화면이 그 이름표를 옮기지 못해 캔버스
    /// 원점에 그대로 남는다 — 가운데 캐릭터 위에 남의 이름이 겹쳐 보이던 것이 이것이다.
    public bool TryHeadScreenPoint(int seat, out Vector2 screenPoint)
    {
        screenPoint = default;
        if (seat < 0 || seat >= SeatCount || stageCamera == null) return false;

        screenPoint = stageCamera.WorldToScreenPoint(
            seats[seat].position + Vector3.up * nameplateHeight);
        return true;
    }

}
