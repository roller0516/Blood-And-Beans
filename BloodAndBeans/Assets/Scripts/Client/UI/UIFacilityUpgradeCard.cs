using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 설비 업그레이드 화면의 카드 한 장 (기획서 8장). 트리는
/// `Assets/Prefabs/UI/Parts/UIFacilityUpgradeCard.prefab`에 있고,
/// <see cref="UIFacilityUpgradeScreen"/>이 `UpgradeCatalog.All` 수만큼 찍어 낸다.
///
/// 카드를 프리팹으로 뺀 이유는 업그레이드 종 수가 데이터에서 오기 때문이다. 화면 프리팹에
/// 9장을 깔아 두면 기획서 8장의 표가 바뀔 때마다 트리를 손으로 고쳐야 하고, 카드 하나를
/// 손보면 나머지 여덟 장도 같이 고쳐야 한다.
///
/// 상태 문구와 색은 카드가 스스로 정한다 — 화면은 "설치됐는가·살 수 있는가"만 넘긴다
/// (AGENTS.md 「에셋과 프로젝트 파일」).
///
/// 글자 배율은 이 카드가 스스로 먹인다 — 화면 루트의 <see cref="UIFontScale"/>는 자기
/// Awake에서 자식을 한 번만 모으므로, 그 뒤에 태어나는 카드는 거기 잡히지 않는다.
[RequireComponent(typeof(UIFontScale))]
public sealed class UIFacilityUpgradeCard : MonoBehaviour
{
    [Header("부품")]
    [SerializeField] TMP_Text nameLabel;
    [SerializeField] TMP_Text effectLabel;
    [SerializeField] TMP_Text costLabel;
    [SerializeField] Button installButton;
    [SerializeField] TMP_Text statusLabel;

    [Header("상태 문구")]
    [SerializeField] string installedText = "INSTALLED";
    [SerializeField] string notEnoughText = "NO PARTS";

    /// 눌렀을 때 화면이 받아 갈 곳. 카드는 자기가 몇 번째인지 모른다.
    Action clicked;

    /// 카드를 만들 때 한 번. 누름은 화면이 인덱스를 붙여 되돌린다.
    public void Bind(Action onInstall)
    {
        clicked = onInstall;
        UIButtons.Wire(installButton, Click);
    }

    /// 다시 그릴 때마다. 이름·효과·비용은 프리팹에 박힌 값이 아니라 카탈로그에서 온다 —
    /// 기획서 8장 표가 바뀌어도 프리팹을 다시 만지지 않는다.
    public void Show(in UpgradeDef def, bool installed, int parts)
    {
        if (nameLabel != null) nameLabel.text = def.Name;
        if (effectLabel != null) effectLabel.text = def.Effect;
        if (costLabel != null) costLabel.text = $"×{def.Cost}";

        var buyable = !installed && parts >= def.Cost;
        if (installButton != null)
        {
            installButton.gameObject.SetActive(buyable);

            // 다시 눌릴 수 있게 되돌린다. 앞선 클릭으로 꺼 둔 채로 남으면 서버가 거절했을
            // 때 그 카드를 영영 설치할 수 없다.
            installButton.interactable = true;
        }

        if (statusLabel == null) return;
        statusLabel.gameObject.SetActive(!buyable);
        if (buyable) return;

        statusLabel.text = installed ? installedText : notEnoughText;
        statusLabel.color = installed ? UITheme.Green : UITheme.Cream;
    }

    void Click()
    {
        // 서버가 결과를 돌려주며 다시 그릴 때까지 이 카드를 잠근다. 업그레이드 재료는
        // 3등급 박스에서만 나와(기획서 8장) 귀하므로, 연타로 두 번 빠지면 그 판의 설비
        // 격차가 그대로 굳는다.
        if (installButton != null) installButton.interactable = false;
        clicked?.Invoke();
    }
}
