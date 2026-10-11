using System.Collections.Generic;
using System.Reflection;
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

    /// 실린 스냅샷의 원본 행 해시. 접속 승인이 호스트와 손님의 표가 같은지 비교한다.
    /// 0이면 아직 실린 표가 없다.
    public static int ContentHash { get; private set; }

    /// 애셋 행을 펴서 후보 스냅샷을 만들고, 검증을 통과할 때만 `Balance.Load`로 갈아 끼운다.
    /// 어긋난 재임포트는 오류만 남기고 기존 스냅샷을 그대로 둔다.
    public void Apply()
    {
        foreach (var table in tables)
            if (table != null) table.Rebuild();

        var problems = new List<string>();
        var candidate = Compose(problems);
        problems.AddRange(BalanceValidation.Problems(candidate));

        if (problems.Count > 0)
        {
            foreach (var problem in problems)
                CDebug.LogError($"{AssetName}: 데이터 표가 어긋나 싣지 않았다 — {problem} 원본은 {ExcelPath}다.");
            return;
        }

        Balance.Load(candidate);
        ContentHash = HashRows();
    }

    /// 표마다 자기 칸을 `BalanceData`에 옮기게 한다. 어느 칸이 어느 표에서 오는지는 표가 안다.
    ///
    /// 빠진 표는 문제로 남겨 폴백으로 성공하지 않게 한다. 표 타입을 이름으로 찾지 않고,
    /// 다 옮긴 뒤에도 **폴백 배열 그대로인 칸**을 잡는다 — 그 칸을 채울 표가 이어져 있지 않은 것이다.
    /// 수치 칸(값 타입)은 공통 수치 표가 스스로 검사한다.
    BalanceData Compose(List<string> problems)
    {
        var d = new BalanceData();

        var fallback = new List<(FieldInfo Field, object Value)>();
        foreach (var f in typeof(BalanceData).GetFields(BindingFlags.Public | BindingFlags.Instance))
            if (!f.FieldType.IsValueType) fallback.Add((f, f.GetValue(d)));

        var seen = new HashSet<System.Type>();
        foreach (var table in tables)
        {
            if (table == null) continue;
            if (!seen.Add(table.GetType())) { problems.Add($"{table.GetType().Name} 애셋이 둘 이상 이어져 있다."); continue; }
            table.WriteTo(d, problems);
        }

        // 빈 배열은 표는 있는데 그 시트를 임포트하지 않은 것이다. 길이 검사가 없는 표도 여기서 잡힌다.
        foreach (var (field, value) in fallback)
        {
            var now = field.GetValue(d);
            if (ReferenceEquals(now, value))
                problems.Add($"{nameof(BalanceData)}.{field.Name}을(를) 채울 표가 이어져 있지 않다.");
            else if (now is System.Array { Length: 0 })
                problems.Add($"{nameof(BalanceData)}.{field.Name}이(가) 비어 있다. 이 칸을 채우는 표를 임포트한다.");
        }
        return d;
    }

    /// 직렬화된 행(엑셀에서 온 값)을 FNV-1a로 접는다. `string.GetHashCode`는 플랫폼·실행마다 달라 쓰지 않는다.
    int HashRows()
    {
        unchecked
        {
            var hash = (int)2166136261;
            foreach (var table in tables)
            {
                if (table == null) continue;
                foreach (var c in JsonUtility.ToJson(table)) hash = (hash ^ c) * 16777619;
            }
            return hash == 0 ? 1 : hash;
        }
    }

    /// 값의 원본. 로그를 보는 사람이 어디를 열어야 하는지 알 수 있게 적는다.
    const string ExcelPath = "Assets/Excel/BloodAndBeans.xlsx";

    /// 씬보다 먼저 돈다. 규칙을 읽는 어떤 오브젝트보다 앞이라 첫 프레임부터 같은 값을 본다.
    ///
    /// 애셋이 없거나 표가 어긋나면 폴백으로 돌되 `Balance.Loaded`가 false라 매치는 시작하지 않는다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplyOnBoot()
    {
        // 도메인 리로드를 끈 채 재생하면 이전 판의 데이터가 남는다 (`FogOfWar.ResetShared`와 같은 이유).
        instance = null;
        ContentHash = 0;
        Balance.Reset();

        var manager = Instance;
        if (manager == null)
        {
            CDebug.LogError($"Resources/{AssetName} 애셋이 없다. 폴백으로 돌며 매치는 시작하지 않는다.");
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
