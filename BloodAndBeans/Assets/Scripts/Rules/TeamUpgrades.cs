/// 한 팀이 이 판에서 설치한 설비 업그레이드 (기획서 8장).
///
/// 효과는 그 판 동안 영구다. 그래서 이 객체의 수명은 팀 원장(`TeamLedger`)과 같고, 판이
/// 끝나면 원장과 함께 사라진다 — 다음 판으로 설비를 물려주지 않는다.
///
/// **여기에는 수치가 없다.** "설치됐는가"만 답한다. 세척 간격이나 자동 서빙 주기 같은
/// 값은 기획서 8장에 표가 없어서 설비 쪽 `[SerializeField]`가 갖는다 (AGENTS.md 「값의
/// 자리는 셋뿐이다」 — 에디터에서 만져 정할 타이밍은 직렬화 필드다).
public class TeamUpgrades
{
    readonly bool[] installed = new bool[UpgradeCatalog.All.Length];

    public int Count => installed.Length;

    public bool Has(UpgradeId id)
    {
        var at = (int)id;
        return at >= 0 && at < installed.Length && installed[at];
    }

    /// 화면이 `UpgradeCatalog.All`과 같은 순서로 읽는다.
    public bool At(int index) => index >= 0 && index < installed.Length && installed[index];

    /// 설치에 필요한 업그레이드 재료 개수. 이미 설치돼 있거나 없는 id면 0이 아니라
    /// -1이다 — 0을 돌려주면 "공짜로 설치 가능"과 구별할 수 없다.
    public int CostOf(UpgradeId id)
    {
        var at = (int)id;
        if (at < 0 || at >= installed.Length || installed[at]) return -1;
        return UpgradeCatalog.Get(id).Cost;
    }

    /// 재료가 충분한가. 차감은 팀 재고(`TeamStock`)가 하므로 여기서는 판정만 한다 —
    /// 재고는 복제되는 네트워크 상태고 이 객체는 순수 규칙이다.
    public bool CanInstall(UpgradeId id, int availableParts)
    {
        var cost = CostOf(id);
        return cost >= 0 && availableParts >= cost;
    }

    /// 설치를 확정한다. 재료 차감이 *성공한 뒤에* 부른다.
    public bool MarkInstalled(UpgradeId id)
    {
        var at = (int)id;
        if (at < 0 || at >= installed.Length || installed[at]) return false;
        installed[at] = true;
        return true;
    }

    /// 복제용 비트마스크. 업그레이드는 9종이라 int 하나에 다 들어간다 — 설치 상태 하나
    /// 때문에 `NetworkList`를 세우고 그 변경 이벤트를 관리할 이유가 없다.
    public int ToMask()
    {
        var mask = 0;
        for (var i = 0; i < installed.Length; i++) if (installed[i]) mask |= 1 << i;
        return mask;
    }

    public static bool HasInMask(int mask, UpgradeId id) => (mask & (1 << (int)id)) != 0;

    public static bool AtInMask(int mask, int index) => (mask & (1 << index)) != 0;
}
