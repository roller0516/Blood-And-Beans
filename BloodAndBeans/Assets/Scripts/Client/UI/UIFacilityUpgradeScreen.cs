using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 전환 페이즈의 설비 업그레이드 화면 (기획서 8장). 레이아웃은 `UI_목업.pptx` 7번이다.
///
/// **트리는 프리팹에 있다.** 이 클래스는 아무것도 만들지 않고 이어 둔 참조에 값만 넣는다.
/// 카드 9장은 `UpgradeCatalog`와 같은 순서로 프리팹에 깔려 있다.
///
/// 무엇을 설치할지 결정하고 재료를 차감하는 것은 서버다 — 여기는 눌렸다는 사실만 넘긴다.
///
/// 기획서 13장이 설비 업그레이드를 첫 검증 빌드에서 제외했으므로 지금은 어느 흐름에서도
/// 열지 않는다. 화면과 프리팹만 준비돼 있다.
public sealed class UIFacilityUpgradeScreen : UIScreen
{
    /// 카드 한 장이 가질 수 있는 상태. 목업 7번의 `INSTALLED` / `INSTALL` / `NO PARTS`다.
    public enum CardState { Installable, Installed, NotEnoughParts }

    /// 카드 한 장의 부품 묶음. 배열 순서는 `UpgradeCatalog.All`과 같아야 한다.
    [Serializable] public class CardSlot
    {
        public GameObject Root;
        public TMP_Text Name;
        public TMP_Text Effect;
        public TMP_Text Cost;
        public Button Install;
        public GameObject InstallRoot;
        public TMP_Text Status;
    }

    [Header("머리")]
    [SerializeField] TMP_Text partsCount;
    [SerializeField] TMP_Text countdown;

    [Header("카드")]
    [SerializeField] CardSlot[] cards = Array.Empty<CardSlot>();

    [Header("카페 배치도")]
    [SerializeField] TMP_Text twinMachineNote;
    [SerializeField] TMP_Text dishwasherNote;
    [SerializeField] Button applyButton;

    /// 전환 페이즈에서 이 화면을 열 때 부른다. `installed`는 `UpgradeCatalog.All`과 같은
    /// 순서의 설치 여부이고, `install`은 눌린 카드의 id를 받는다.
    public void Bind(IReadOnlyList<bool> installed, int parts,
                     Action<UpgradeId> install, Action apply)
    {
        if (partsCount != null) partsCount.text = $"×{parts}";
        UIButtons.Wire(applyButton, apply);

        var all = UpgradeCatalog.All;
        for (var i = 0; i < cards.Length && i < all.Length; i++)
        {
            var slot = cards[i];
            if (slot == null) continue;

            // 프리팹에 문구가 박혀 있어도 카탈로그를 원본으로 삼는다. 기획서 8장 표가
            // 바뀌면 프리팹을 다시 만지지 않아도 화면이 따라온다.
            if (slot.Name != null) slot.Name.text = all[i].Name;
            if (slot.Effect != null) slot.Effect.text = all[i].Effect;
            if (slot.Cost != null) slot.Cost.text = $"×{all[i].Cost}";

            var done = installed != null && i < installed.Count && installed[i];
            var affordable = parts >= all[i].Cost;
            var state = done ? CardState.Installed
                      : affordable ? CardState.Installable
                      : CardState.NotEnoughParts;

            if (slot.InstallRoot != null)
                slot.InstallRoot.SetActive(state == CardState.Installable);
            if (slot.Status != null)
                slot.Status.gameObject.SetActive(state != CardState.Installable);

            switch (state)
            {
                case CardState.Installed:
                    if (slot.Status != null)
                    {
                        slot.Status.text = "INSTALLED";
                        slot.Status.color = UITheme.Green;
                    }
                    break;

                case CardState.NotEnoughParts:
                    if (slot.Status != null)
                    {
                        slot.Status.text = "NO PARTS";
                        slot.Status.color = UITheme.Cream;
                    }
                    break;

                default:
                    if (slot.Install == null) break;

                    // 람다가 루프 변수를 잡지 않도록 한 번 복사한다.
                    var id = all[i].Id;
                    var button = slot.Install;

                    // 다시 눌릴 수 있게 되돌린다. 앞선 클릭으로 꺼 둔 채로 남으면 서버가
                    // 거절했을 때 그 카드를 영영 설치할 수 없다.
                    button.interactable = true;

                    UIButtons.Wire(button, () =>
                    {
                        // 서버가 결과를 돌려주며 다시 `Bind`할 때까지 같은 카드를 잠근다.
                        // 업그레이드 재료는 3등급 박스에서만 나와(기획서 8장) 귀하므로,
                        // 연타로 두 번 빠지면 그 판의 설비 격차가 그대로 굳는다.
                        button.interactable = false;
                        install?.Invoke(id);
                    });
                    break;
            }
        }

        if (twinMachineNote != null)
            twinMachineNote.text = IsInstalled(installed, UpgradeId.TwinMachine) ? "2구 적용" : "";
        if (dishwasherNote != null)
            dishwasherNote.text = IsInstalled(installed, UpgradeId.Dishwasher)
                ? "식기세척기 적용" : "식기세척기 미적용";
    }

    static bool IsInstalled(IReadOnlyList<bool> installed, UpgradeId id)
    {
        var i = (int)id;
        return installed != null && i < installed.Count && installed[i];
    }

    /// 남은 전환 시간 (기획서 4장의 10초).
    public void SetRemaining(float seconds)
    {
        if (countdown != null)
            countdown.text = Mathf.CeilToInt(Mathf.Max(seconds, 0f)).ToString();
    }
}
