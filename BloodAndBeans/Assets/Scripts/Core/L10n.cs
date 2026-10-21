using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// 사용자에게 보이는 문구를 Localization 표에서 꺼낸다. 키는 `TextKey`에 있고 값은
/// `Assets/Localization/Source/Localization.xlsx`가 원본이다.
///
/// 틀(`{0}` 자리표시자)을 키별로 한 번만 읽어 둔다 — HUD가 갱신 주기마다 부르므로
/// 매번 표를 뒤지면 할당이 늘어난다. 로케일이 바뀌면 비운다.
public static class L10n
{
    const string TableName = "Localization";

    static readonly Dictionary<string, string> templates = new();
    static readonly HashSet<string> warned = new();
    static bool subscribed;

    // 도메인 리로드 없이 재생해도 이전 재생의 캐시와 구독 상태를 끌고 가지 않는다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        templates.Clear();
        warned.Clear();
        subscribed = false;
    }

    public static string Get(string key) => Template(key);

    /// `string.Format`과 같은 규칙이다. 인자 수가 틀의 자리표시자와 어긋나면 화면을 깨지 않고
    /// 키를 대괄호로 싸서 돌려준다 — 시트의 오타가 UI 예외가 되지 않게 한다.
    public static string Format(string key, params object[] args)
    {
        var template = Template(key);
        try
        {
            return string.Format(template, args);
        }
        catch (System.FormatException)
        {
            CDebug.LogError($"[L10n] '{key}' 문구의 자리표시자가 인자와 맞지 않는다: {template}");
            return $"[{key}]";
        }
    }

    static string Template(string key)
    {
        if (templates.TryGetValue(key, out var text)) return text;

        if (!subscribed)
        {
            LocalizationSettings.SelectedLocaleChanged += _ => templates.Clear();
            subscribed = true;
        }

        var table = LocalizationSettings.StringDatabase.GetTable(TableName, CurrentLocale());
        var entry = table != null ? table.GetEntry(key) : null;
        if (entry == null || string.IsNullOrEmpty(entry.Value))
        {
            // 없는 키는 빈 문자열이 아니라 눈에 띄는 표시로 남긴다. 캐시하지 않는다 — 표가
            // 늦게 준비되거나 시트를 다시 임포트하면 다음 조회에서 바로 읽혀야 한다.
            if (warned.Add(key)) CDebug.LogWarning($"[L10n] '{TableName}' 표에 '{key}' 문구가 없다.");
            return $"[{key}]";
        }

        templates[key] = entry.Value;
        return entry.Value;
    }

    /// 선택된 로케일. 에디터 편집 모드에는 없다(Scene Controls로 고른 값만 쓴다) — 그때는
    /// 등록된 첫 로케일로 읽는다. 한국어 하나뿐인 지금은 그것이 곧 `ko`다.
    static Locale CurrentLocale()
    {
        var selected = LocalizationSettings.SelectedLocale;
        if (selected != null) return selected;
        var all = LocalizationSettings.AvailableLocales.Locales;
        return all.Count > 0 ? all[0] : null;
    }
}
