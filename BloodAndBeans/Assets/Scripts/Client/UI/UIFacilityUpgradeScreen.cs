using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 전환 페이즈의 설비 업그레이드 화면 (기획서 8장). 레이아웃은 `UI_목업.pptx` 7번이다.
///
/// **머리·카페 배치도 트리는 프리팹에 있다.** 이 클래스는 그것들을 만들지 않고 이어 둔
/// 참조에 값만 넣는다. 다만 **카드는 `UpgradeCatalog.All`을 보고 찍어 낸다** — 업그레이드
/// 종 수가 기획서 8장의 표에서 오므로, 프리팹에 몇 장을 깔아 두든 데이터와 어긋난다.
/// 격자 배치는 `cardRoot`의 `GridLayoutGroup`이 맡고, 넘치는 줄은 스크롤로 내려간다.
///
/// 무엇을 설치할지 결정하고 재료를 차감하는 것은 서버다 — 여기는 눌렸다는 사실만 넘긴다.
///
/// 기획서 13장이 설비 업그레이드를 첫 검증 빌드에서 제외했으므로 지금은 어느 흐름에서도
/// 열지 않는다. 화면과 프리팹만 준비돼 있다.
public sealed class UIFacilityUpgradeScreen : UIScreen
{
    [Header("머리")]
    [SerializeField] TMP_Text partsCount;
    [SerializeField] TMP_Text countdown;

    [Header("카드")]
    [SerializeField] UIFacilityUpgradeCard cardPrefab;
    [SerializeField] RectTransform cardRoot;

    [Header("카페 배치도")]
    [SerializeField] TMP_Text twinMachineNote;
    [SerializeField] TMP_Text dishwasherNote;
    [SerializeField] Button applyButton;

    /// 찍어 낸 카드. 인덱스는 `UpgradeCatalog.All`과 같다.
    readonly List<UIFacilityUpgradeCard> cards = new();

    /// 카드가 눌렸을 때 부를 곳. `Bind`마다 바뀔 수 있어 필드로 든다 — 카드에 건 콜백은
    /// 만들 때 한 번만 걸린다.
    Action<UpgradeId> onInstall;

    /// 전환 페이즈에서 이 화면을 열 때 부른다. `installed`는 `UpgradeCatalog.All`과 같은
    /// 순서의 설치 여부이고, `install`은 눌린 카드의 id를 받는다.
    public void Bind(IReadOnlyList<bool> installed, int parts,
                     Action<UpgradeId> install, Action apply)
    {
        onInstall = install;

        if (partsCount != null) partsCount.text = $"×{parts}";
        UIButtons.Wire(applyButton, apply);

        BuildCards();

        var all = UpgradeCatalog.All;
        for (var i = 0; i < cards.Count && i < all.Length; i++)
        {
            if (cards[i] == null) continue;
            cards[i].Show(all[i], IsInstalled(installed, i), parts);
        }

        if (twinMachineNote != null)
            twinMachineNote.text = IsInstalled(installed, (int)UpgradeId.TwinMachine)
                ? "2구 적용" : "";
        if (dishwasherNote != null)
            dishwasherNote.text = IsInstalled(installed, (int)UpgradeId.Dishwasher)
                ? "식기세척기 적용" : "식기세척기 미적용";
    }

    /// 카탈로그 수만큼 카드를 찍는다. 화면은 재사용되므로 한 번만 만든다.
    ///
    /// `Awake`가 아니라 여기서 만드는 이유는 화면 루트의 <see cref="UIFontScale"/>가 자기
    /// Awake에서 자식 글자를 모으기 때문이다. 그 전에 카드가 있으면 카드가 스스로 먹인
    /// 배율 위에 화면 배율이 한 번 더 곱해진다.
    void BuildCards()
    {
        if (cards.Count > 0) return;
        if (cardPrefab == null || cardRoot == null)
        {
            CDebug.LogError($"[{nameof(UIFacilityUpgradeScreen)}] 카드 프리팹 또는 부모가 비어 있다.");
            return;
        }

        var all = UpgradeCatalog.All;
        for (var i = 0; i < all.Length; i++)
        {
            var id = all[i].Id;
            var card = Instantiate(cardPrefab, cardRoot);
            card.name = $"Card_{id}";
            card.Bind(() => onInstall?.Invoke(id));
            cards.Add(card);
        }
    }

    static bool IsInstalled(IReadOnlyList<bool> installed, int index) =>
        installed != null && index < installed.Count && installed[index];

    /// 남은 전환 시간 (기획서 4장의 10초).
    public void SetRemaining(float seconds)
    {
        if (countdown != null)
            countdown.text = Mathf.CeilToInt(Mathf.Max(seconds, 0f)).ToString();
    }
}
