using System.Collections.Generic;
using UnityEngine;

/// 표로 묶이지 않는 단일 수치 전부. 시트 한 장에 `key · value · note` 세 열이다.
///
/// **`key`는 이 애셋의 public 필드 이름 그대로다.** 예전에는 `BalanceData`의 필드 이름을
/// 가리켰고 값은 행 목록으로만 들고 있었는데, `BalanceData`를 없애면서 값과 스키마가
/// 여기 한 곳으로 합쳐졌다. 엑셀의 `key` 열은 그대로라 시트는 손댈 필요가 없다.
///
/// **코드에는 기본값을 두지 않는다.** 수치를 고칠 곳은 엑셀 하나뿐이어야 한다 — 코드에
/// 폴백을 적어 두면 같은 숫자가 두 곳에 살고, 엑셀만 고친 사람이 옛 값을 보게 된다.
/// 임포트하지 않은 키는 0으로 남고 `BalanceValidation`이 그것을 잡는다.
[CreateAssetMenu(menuName = "Blood & Beans/데이터 표/공통 수치", fileName = nameof(CommonDataTable))]
public sealed class CommonDataTable : DataTableAsset
{
    public const string SheetScalars = "scalars";

    // --- penalty (기획서 3.3) ---

    /// 감소를 다 먹여도 남는 최솟값.
    /// ponytail: 기획서 3.3에 하한이 없다. 같은 축에 붙는 감소가 늘면 그때 기획서로 올린다.
    public float PenaltyMinScale;

    // --- dayphases (기획서 4장) ---
    // 7일 x (밤 120 + 낮 120 + 전환 10) = 1,750초, 약 29분 10초.

    public float NightSeconds;
    public float DaySeconds;
    public float TransitionSeconds;
    public int TotalDays;

    /// 겉보기와 견제가 갈리는 선 (기획서 6.6). 두 규칙이 같은 수치를 쓰는 것이 요점이다.
    public float OverloadRatio;

    /// 화면 흔들림이 붙는 선 (기획서 6.7).
    public float ShakeRatio;

    /// 대시를 못 쓰게 되는 선 (기획서 6.6). 겉보기·낙하보다 10%p 낮다 — 견제 수단을 먼저
    /// 잃고 그다음에 표적이 되는 것이 의도된 순서다.
    public float DashBlockRatio;

    // --- night (기획서 6장) ---

    public float BagCapacity;             // 6.7 가방 100% 기준선
    public float BoxOpenSeconds;         // 6.5.2 박스 개봉 홀드
    public float BagRetrieveSeconds;     // 6.7 묻은 가방 회수
    public float BagBurnSeconds;           // 6.7 소각
    public float DashSpillShare;         // 6.6 적재 80% 이상에게 대시하면 떨어지는 비율

    // --- day (기획서 5.2 · 5.3 · 5.5 · 5.7.2 · 8.1) ---

    public float CoffeeSeconds;
    public float OvenSeconds;
    public float WashSeconds;
    public int Machines;
    public int Sinks;

    /// 5.5: 낮이 시작되면 이 간격으로 대기 슬롯 수만큼 들어오고, 그 뒤로는 자리가 비는 즉시.
    public float FirstEntrySeconds;

    /// 5.2: Perfect는 그 손님의 최대 인내심을 이 비율만큼 회복시킨다.
    public float PerfectPatienceRecovery;

    /// 5.7.2: 인내심 링의 촉박·위급 경계.
    public float PatienceWarning;
    public float PatienceUrgent;

    // --- gems (기획서 8.1 · 8.2) ---

    /// 8.1: 6종 모두 3턴(낮 3회).
    public int GemTurns;

    // --- forecast (기획서 5.5 · 5.6.1) ---

    /// 5.6.1: 1·2일차 2종, 3일차부터 3종.
    public int PopularCountEarly;
    public int PopularCountLate;
    public int PopularCountLateFromDay;

    /// 5.5 규칙 3: 이 확률로 오늘 밤 풀에서, 나머지는 팀 보유 재료에서 뽑는다.
    public double ForecastPoolShare;

    /// 5.5 규칙 4: 기본 메뉴(핫 아메리카노)는 주문의 1/이 값을 넘지 않는다.
    public int AmericanoCapDivisor;

    // --- saleprice (기획서 5.2 · 5.6.2 · 1.4 · 7.2) ---

    /// 인기 재료 하나당 보너스. 곱하지 않고 합산해서 한 번만 적용한다 (5.6.2).
    public float PopularBonus;

    /// 1.4 · 7.2: 블러드 빈을 쓰면 커피 판매가 x3.
    public float BloodBeanMultiplier;

    // --- lootslots (기획서 6.5.2 · 6.5.5) ---

    /// 상자 하나가 담는 최대 *종류* 수. 개수 상한이 아니다.
    public int LootMaxTypes;

    // --- forest (기획서 6.3 · 6.3.1) ---

    /// 구역 경계. 중심까지의 거리를 숲 반지름으로 나눈 값이다.
    /// ponytail: 기획서 6.3에 구역 반경이 없다. 레벨 디자인이 치수를 정하면 맵 데이터로 옮긴다.
    public float CoreRatio;
    public float MidRatio;

    /// 매 밤 총 개수 범위 (6.3.1). 일차와 무관하다.
    /// 6.3.1의 팀 수 비례(3팀 x1.5 · 4팀 x2)는 사용자 결정으로 적용하지 않는다.
    public int MinBoxes;
    public int MaxBoxes;

    // --- skills (기획서 9.1.2 · 9.1.3 · 9.2) ---

    /// 9.1.3 표의 지속.
    public float GlideSeconds;
    public float ShortcutSeconds;

    /// 9.1.2 삼키기: 세척 70% 진행이라 개수대 점유 3초가 0.9초가 된다.
    public float SwallowProgress;

    // ponytail: 9.1.2는 "크게 오른다"뿐이고 폭은 9.1의 +25~40%다. 상한으로 박았다.
    public float GlideSpeed;

    // ponytail: 9.1.2 불붙이기에 수치가 없다. 9.1 폭 상한 40% · 과열 3초로 박았다.
    public float IgniteCut;
    public float OverheatSeconds;

    /// 밤 액티브의 판정 반경과 지속 (9.2).
    /// ponytail: 기본 시야 12m(6.1)와 메아리 7m(9.2)가 충돌해 기존 20m를 유지한다.
    /// 기획 반경이 정리되면 확정 수치로 바꾼다.
    public float EchoRadius;
    public float AppraiseRadius;
    public float TrackRadius;
    public float TrackRevealSeconds;

    // --- 임포트 ---

    protected override string DefaultCategory => "공통 수치";
    protected override string[] DefaultSheetNames => new[] { SheetScalars };

    /// 시트에 있는 키를 읽어 같은 이름의 필드에 넣는다. 빠진 키는 0으로 남는다.
    public override void ReadSheet(int index, SheetTable sheet)
    {
        for (var i = 0; i < sheet.Count; i++)
        {
            var r = sheet[i];
            var key = r.Text("key");
            if (key.Length == 0) continue;

            if (!Assign(this, key, r.Double("value")))
                sheet.Errors.Add($"[{sheet.Name}] {r.Line}행 'key' 칸: '{key}'은(는) 단일 수치 필드가 아니다.");
        }
    }

    /// 펼 것이 없다. 스칼라는 `ReadSheet`가 필드에 곧장 넣고 그대로 직렬화되므로,
    /// 다른 표처럼 행을 배열로 펴는 단계가 필요 없다.
    public override void Rebuild() { }

    /// 이름이 맞으면 값을 넣고 true. `float`·`int`·`double` 셋만 받는다.
    public static bool Assign(CommonDataTable target, string key, double value)
    {
        var field = typeof(CommonDataTable).GetField(key);
        if (field == null) return false;

        if (field.FieldType == typeof(float)) field.SetValue(target, (float)value);
        else if (field.FieldType == typeof(double)) field.SetValue(target, value);
        else if (field.FieldType == typeof(int))
            field.SetValue(target, (int)System.Math.Round(value, System.MidpointRounding.AwayFromZero));
        else return false;

        return true;
    }

    public static bool IsScalarField(string key)
    {
        var field = typeof(CommonDataTable).GetField(key);
        return field != null &&
            (field.FieldType == typeof(float) || field.FieldType == typeof(int) || field.FieldType == typeof(double));
    }

    /// 내보내기가 쓰는 목록. 선언 순서 그대로라 시트도 같은 순서로 나온다.
    public static IEnumerable<(string Key, double Value)> ScalarFields(CommonDataTable data)
    {
        foreach (var f in typeof(CommonDataTable).GetFields())
        {
            if (f.FieldType == typeof(float)) yield return (f.Name, (float)f.GetValue(data));
            else if (f.FieldType == typeof(int)) yield return (f.Name, (int)f.GetValue(data));
            else if (f.FieldType == typeof(double)) yield return (f.Name, (double)f.GetValue(data));
        }
    }
}
