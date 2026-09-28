using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// 엑셀에서 넣은 데이터가 애셋에 온전히 들어왔는가.
///
/// 예전에는 `BalanceData`의 코드 폴백과 엑셀 값을 맞대어 봤다. 수치를 고칠 곳이 엑셀
/// 하나가 되면서 그 폴백이 사라졌고, 비교 대상도 함께 사라졌다.
///
/// **남은 위험은 반대쪽이다.** 이제 값의 출처가 엑셀뿐이라, 임포트를 빠뜨리거나 시트
/// 하나를 흘리면 그 표가 통째로 비어 게임이 0으로 돈다. 그것을 여기서 잡는다.
public class DataTableTests
{
    static DataManager LoadManager()
    {
        var manager = Resources.Load<DataManager>(DataManager.AssetName);
        if (manager == null) Assert.Ignore($"Resources/{DataManager.AssetName} 애셋이 없다. 데이터 표를 아직 만들지 않았다.");
        return manager;
    }

    [Test]
    public void 매니저가_카테고리_애셋을_전부_들고_있다()
    {
        var manager = LoadManager();

        Assert.Greater(manager.Tables.Length, 0, "데이터 표 애셋이 하나도 이어져 있지 않다");
        foreach (var table in manager.Tables)
            Assert.IsNotNull(table, "빈 칸이 있다. 임포터 창의 「설정 만들기」로 채운다");
    }

    [Test]
    public void 카테고리마다_읽는_시트가_겹치지_않는다()
    {
        var manager = LoadManager();
        var seen = new Dictionary<string, string>();

        foreach (var table in manager.Tables)
        {
            if (table == null) continue;
            foreach (var sheet in table.SheetNames)
            {
                Assert.IsFalse(seen.ContainsKey(sheet),
                    $"시트 '{sheet}'을(를) '{table.Category}'와 '{(seen.TryGetValue(sheet, out var other) ? other : "?")}'가 함께 읽는다");
                seen[sheet] = table.Category;
            }
        }
    }

    [Test]
    public void 임포트한_표가_비어_있지_않다()
    {
        var manager = LoadManager();
        var empty = new List<string>();

        foreach (var table in manager.Tables)
        {
            if (table == null) continue;
            table.Rebuild();

            foreach (var field in table.GetType().GetFields())
            {
                if (field.GetValue(table) is not System.Array array) continue;
                if (array.Length == 0) empty.Add($"{table.Category} · {field.Name}");
            }
        }

        Assert.IsEmpty(empty,
            "엑셀에서 임포트하지 않은 표가 있다. 임포터 창에서 다시 읽는다: " +
            string.Join(" / ", empty));
    }

    [Test]
    public void 공통_수치가_0으로_남아_있지_않다()
    {
        var manager = LoadManager();
        var zero = new List<string>();

        foreach (var table in manager.Tables)
        {
            if (table is not CommonDataTable common) continue;

            // 0은 "엑셀에 그 키가 없었다"는 뜻이다. 실제로 0이어야 하는 수치는 이 표에 없다.
            foreach (var (key, value) in CommonDataTable.ScalarFields(common))
                if (value == 0d) zero.Add(key);
        }

        Assert.IsEmpty(zero,
            "엑셀 scalars 시트에 빠진 키가 있다: " + string.Join(", ", zero));
    }

    // --- 애셋 → Balance 스냅샷 연결 ---

    [Test]
    public void 애셋_값이_규칙_스냅샷으로_실린다()
    {
        var manager = LoadManager();
        manager.Apply();

        Assert.IsTrue(Balance.Loaded, "검증을 통과하지 못해 스냅샷이 실리지 않았다. 콘솔의 DataManager 오류를 본다");
        Assert.AreNotEqual(0, DataManager.ContentHash);

        foreach (var table in manager.Tables)
        {
            switch (table)
            {
                case EconomyDataTable economy:
                    Assert.AreSame(economy.RentByDay, Balance.Current.RentByDay, "임대료가 엑셀 값이 아니다");
                    Assert.AreSame(economy.MenuTable, Balance.Current.Menus, "메뉴 가격이 엑셀 값이 아니다");
                    break;
                case CharacterDataTable characters:
                    Assert.AreSame(characters.DaySkillCooldown, Balance.Current.DaySkillCooldown, "쿨타임이 엑셀 값이 아니다");
                    break;
                case CommonDataTable common:
                    Assert.AreEqual(common.DaySeconds, Balance.Current.DaySeconds, "공통 수치가 엑셀 값이 아니다");
                    break;
            }
        }
    }

    [Test]
    public void 표가_빠진_매니저는_기존_스냅샷을_바꾸지_않는다()
    {
        var before = Balance.Current;
        var wasLoaded = Balance.Loaded;
        var empty = ScriptableObject.CreateInstance<DataManager>();
        try
        {
            // 빠진 카테고리마다 오류를 찍는 것이 기대 동작이다.
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            empty.Apply();

            Assert.AreSame(before, Balance.Current, "어긋난 표가 스냅샷을 갈아 끼웠다");
            Assert.AreEqual(wasLoaded, Balance.Loaded);
        }
        finally
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            Object.DestroyImmediate(empty);
        }
    }
}
