using UnityEngine;

/// 데이터 표 애셋들을 들고 있다가 부팅 때 규칙 표에 한 번 밀어 넣는다.
/// 원본은 `Resources/DataManager` 애셋 하나다 (`ResourceManager`와 같은 관례).
///
/// **읽기 창구가 아니다.** 값을 읽는 곳은 `Rent.Due()` · `Gems.CookTimeScale`처럼
/// 예전부터 있던 규칙 API이고, 그 뒤의 진실은 `BB.Rules`의 `Balance.Current` 하나다.
/// 여기에 `DataManager.Instance.Rent...` 같은 읽기 API를 열면 퍼사드가 둘이 되고
/// 서비스 로케이터가 된다 — 그래서 public 표면은 `Apply()`와 편집용 `Tables`뿐이다.
[CreateAssetMenu(menuName = "Blood & Beans/데이터 매니저", fileName = AssetName)]
public sealed class DataManager : ScriptableObject
{
    public const string AssetName = nameof(DataManager);

    /// 카테고리 하나당 애셋 하나. 임포터 창의 왼쪽 목록이 이 순서로 뜬다.
    [SerializeField] DataTableAsset[] tables = System.Array.Empty<DataTableAsset>();

    public DataTableAsset[] Tables => tables;

    static DataManager instance;

    public static DataManager Instance
    {
        get
        {
            if (instance != null) return instance;
            instance = Resources.Load<DataManager>(AssetName);
            return instance;
        }
    }

    /// 각 애셋이 엑셀에서 받아 둔 행을 규칙이 읽을 배열로 펴게 한다.
    ///
    /// 값은 애셋이 소유한다. 여기서 어디로 옮겨 담지 않는다 — 옮겨 담던 시절에는 같은
    /// 수치가 엑셀과 코드 폴백 두 곳에 살았고, 엑셀만 고친 사람이 옛 값을 봤다.
    ///
    /// ponytail: 규칙은 아직 `Balance.Current`(`BalanceData`)를 읽는다. 읽기 76곳을
    /// 애셋 직접 읽기로 바꾸는 작업이 남아 있고, 그때까지 엑셀 수정이 규칙에 반영되지
    /// 않는다. 그래서 부팅 때 경고를 남긴다.
    public void Apply()
    {
        foreach (var table in tables)
        {
            if (table == null) continue;
            table.Rebuild();
        }

        CDebug.LogWarning($"{AssetName}: 규칙이 아직 {nameof(BalanceData)}를 읽는다. " +
                          $"엑셀({ExcelPath}) 수정이 규칙에 반영되지 않는다 — 읽기 전환이 끝나면 이 경고를 지운다.");
        ReportProblems();
    }

    /// 표가 어긋난 채로 부팅되면 "쿨타임이 0이라 스킬이 무한 연타"처럼 엉뚱한 증상으로만
    /// 드러난다. 부팅 때 한 번 찍어 원인이 엑셀 편집이라는 것을 바로 보이게 한다.
    ///
    /// 판정은 `BB.Rules`의 순수 함수가 하고 여기서는 로그만 찍는다 — `BB.Rules`는
    /// `UnityEngine`을 못 쓰고(`noEngineReferences`), 같은 함수를 EditMode 테스트가 쓴다.
    static void ReportProblems()
    {
        foreach (var problem in BalanceValidation.Problems(Balance.Current))
            CDebug.LogError($"{AssetName}: 데이터 표가 어긋났다 — {problem} 원본은 {ExcelPath}다.");
    }

    /// 값의 원본. 로그를 보는 사람이 어디를 열어야 하는지 알 수 있게 적는다.
    const string ExcelPath = "Assets/Excel/BloodAndBeans.xlsx";

    /// 씬보다 먼저 돈다. 규칙을 읽는 어떤 오브젝트보다 앞이라 첫 프레임부터 같은 값을 본다.
    ///
    /// 애셋이 없어도 계속 간다 — `Balance.Reset()`이 기획서 확정치 폴백을 세워 두므로
    /// 데이터를 아직 안 만든 상태에서도 게임이 그대로 돈다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplyOnBoot()
    {
        // 도메인 리로드를 끈 채 재생하면 이전 판의 데이터가 남는다 (`FogOfWar.ResetShared`와 같은 이유).
        instance = null;
        Balance.Reset();

        var manager = Instance;
        if (manager == null)
        {
            CDebug.LogWarning($"Resources/{AssetName} 애셋이 없다. 기획서 확정치 폴백으로 돈다.");
            return;
        }
        manager.Apply();
    }

#if UNITY_EDITOR
    /// 에디터 도구(숲 배치·완성 게이지 등)도 재생 때와 같은 수치를 봐야 한다.
    /// 이게 없으면 에디터에서 깐 배치와 실제 판이 다른 표로 계산된다.
    [UnityEditor.InitializeOnLoadMethod]
    static void ApplyInEditor() => ApplyOnBoot();
#endif
}
