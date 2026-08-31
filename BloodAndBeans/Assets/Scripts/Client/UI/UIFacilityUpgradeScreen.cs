using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 전환 페이즈의 설비 업그레이드 화면 (기획서 8장). 레이아웃은 `UI_목업.pptx` 7번이다.
///
/// 업그레이드 목록과 문구는 `UpgradeCatalog`가 들고 있다. 이 화면은 어느 것이 이미
/// 설치됐고 재료가 얼마나 남았는지를 받아, 카드마다 셋 중 하나의 상태를 그린다.
/// 무엇을 설치할지 결정하고 재료를 차감하는 것은 서버다 — 여기는 눌렸다는 사실만 넘긴다.
public sealed class UIFacilityUpgradeScreen : UIScreen
{
    /// 카드 한 장이 가질 수 있는 상태. 목업 7번의 `INSTALLED` / `INSTALL` / `NO PARTS`다.
    public enum CardState { Installable, Installed, NotEnoughParts }

    const int Columns = 3;
    const float CardWidth = 408f, CardHeight = 302f;
    const float CardStepX = 424f, CardStepY = 318f;
    const float GridX = 48f, GridY = 119f;

    TMP_Text partsCount, countdown;
    Button applyButton;
    Transform grid;

    readonly List<GameObject> cards = new();

    /// 카드마다 만들어 두는 설치 버튼. `Bind`가 다시 불려도 카드를 통째로 다시 만들지
    /// 않고 상태만 갈아 끼우기 위해 들고 있는다.
    readonly List<Button> installButtons = new();
    readonly List<TMP_Text> statusLabels = new();
    readonly List<GameObject> buttonRoots = new();

    bool built;

    protected override void Awake()
    {
        base.Awake();
        Build();
    }

    void Build()
    {
        if (built) return;
        built = true;

        var stage = UITheme.Stage(transform, UITheme.Ink);

        UITheme.Caption(stage, "FACILITY UPGRADE", 48f, 61f, 277f);
        UITheme.Text(stage, "Note", "업그레이드 재료는 3등급 박스에서만 나온다", 10f,
                     UITheme.Cream, 320f, 58f, 303f, 22f);

        UITheme.Box(stage, "PartsSwatch", UITheme.Purple, 1752f, 43f, 26f, 26f);
        partsCount = UITheme.Text(stage, "PartsCount", "×0", 22f, UITheme.Cream,
                                  1790f, 39f, 42f, 38f);
        countdown = UITheme.Text(stage, "Countdown", "0", 30f, UITheme.Red,
                                 1840f, 36f, 40f, 44f, TextAlignmentOptions.TopRight);

        UITheme.Rule(stage, UITheme.Gold, 48f, 94f, 1824f);

        grid = stage;
        BuildCards();
        BuildLayoutPanel();
    }

    void BuildCards()
    {
        var all = UpgradeCatalog.All;
        for (var i = 0; i < all.Length; i++)
        {
            var x = GridX + i % Columns * CardStepX;
            var y = GridY + i / Columns * CardStepY;

            var card = UITheme.Box(grid, $"Card_{all[i].Id}", UITheme.Panel,
                                   x, y, CardWidth, CardHeight);
            cards.Add(card.gameObject);

            UITheme.Box(card, "Icon", UITheme.Placeholder, 19f, 17f, 46f, 46f);
            UITheme.Text(card, "Name", all[i].Name, 16f, UITheme.Cream, 77f, 25f, 220f, 34f);
            UITheme.Text(card, "Effect", all[i].Effect, 10f, UITheme.Cream,
                         19f, 73f, 381f, 169f);

            UITheme.Rule(card, UITheme.Cream, 19f, 248f, 370f);
            UITheme.Box(card, "PartsSwatch", UITheme.Purple, 19f, 262f, 20f, 20f);
            UITheme.Text(card, "Cost", $"×{all[i].Cost}", 13f, UITheme.Cream,
                         49f, 262f, 60f, 24f);

            // 설치 버튼과 상태 글자는 같은 자리를 나눠 쓴다. 목업에서 `INSTALL`만 금색
            // 버튼이고 `INSTALLED`·`NO PARTS`는 글자만 있다.
            var button = UITheme.Button(card, "Install", "INSTALL", true, 286f, 258f, 103f, 26f);
            var label = button.GetComponentInChildren<TMP_Text>();
            label.fontSize = 9f;
            label.alignment = TextAlignmentOptions.Center;
            installButtons.Add(button);
            buttonRoots.Add(button.gameObject);

            statusLabels.Add(UITheme.Text(card, "Status", string.Empty, 9f, UITheme.Green,
                                          250f, 264f, 139f, 18f, TextAlignmentOptions.TopRight));
        }
    }

    /// 오른쪽 카페 배치도. 기획서 5.4의 구조를 그대로 그린 그림이라 값이 바뀌지 않는다 —
    /// 설비가 붙었는지만 아래 두 글자가 알려 준다.
    TMP_Text twinMachineNote, dishwasherNote;

    void BuildLayoutPanel()
    {
        var panel = UITheme.Box(grid, "CafeLayout", UITheme.Panel, 1330f, 119f, 542f, 937f);
        UITheme.Caption(panel, "CAFE LAYOUT", 23f, 15f, 512f);
        UITheme.Rule(panel, UITheme.Gold, 1f, 43f, 540f);

        UITheme.Text(panel, "GuestQueue", "GUEST QUEUE", 8f, UITheme.Cream,
                     0f, 64f, 542f, 16f, TextAlignmentOptions.Top);
        UITheme.Text(panel, "Counter", "서빙 카운터", 10f, UITheme.Cream,
                     14f, 89f, 513f, 38f, TextAlignmentOptions.Top);

        // 제조존 (위). 조리대가 위아래를 가른다 (기획서 5.4).
        MakeStation(panel, "Machine1", "커피머신 1", 21f, 136f);
        twinMachineNote = UITheme.Text(panel, "TwinNote", string.Empty, 8f, UITheme.Green,
                                       76f, 176f, 100f, 17f);
        MakeStation(panel, "Machine2", "커피머신 2", 192f, 136f);
        MakeStation(panel, "Oven", "오븐", 362f, 136f);

        UITheme.Box(panel, "PrepIsland", UITheme.Gold, 21f, 220f, 500f, 40f);
        UITheme.Text(panel, "PrepIslandLabel", "조리대 — 통과 불가 · 아이템만 건넬 수 있음",
                     10f, UITheme.Cream, 14f, 231f, 513f, 42f, TextAlignmentOptions.Top);

        // 보급·세척존 (아래).
        UITheme.Box(panel, "Pantry", UITheme.Placeholder, 21f, 272f, 244f, 72f);
        UITheme.Text(panel, "PantryLabel", "재료 칸", 10f, UITheme.Ink,
                     18f, 292f, 250f, 74f, TextAlignmentOptions.Top);
        UITheme.Box(panel, "Sink", UITheme.Placeholder, 277f, 272f, 244f, 72f);
        UITheme.Text(panel, "SinkLabel", "싱크대", 10f, UITheme.Ink, 380f, 292f, 100f, 21f);
        dishwasherNote = UITheme.Text(panel, "DishwasherNote", string.Empty, 8f, UITheme.Ink,
                                      349f, 312f, 140f, 17f);

        UITheme.Text(panel, "Irreversible",
                     "업그레이드는 동선을 줄이는 방향으로만 작동한다. 적용 후 되돌릴 수 없다.",
                     10f, UITheme.Cream, 21f, 814f, 515f, 26f);

        applyButton = UITheme.Button(panel, "Apply", "적용하고 밤으로", true, 21f, 856f, 500f, 60f);
    }

    static void MakeStation(Transform parent, string name, string label, float x, float y)
    {
        UITheme.Box(parent, name, UITheme.Placeholder, x, y, 159f, 72f);
        UITheme.Text(parent, name + "Label", label, 10f, UITheme.Ink, x + 48f, y + 20f, 100f, 21f);
    }

    /// 전환 페이즈에서 이 화면을 열 때 부른다. `installed`는 `UpgradeCatalog.All`과 같은
    /// 순서의 설치 여부이고, `install`은 눌린 카드의 id를 받는다.
    public void Bind(IReadOnlyList<bool> installed, int parts,
                     Action<UpgradeId> install, Action apply)
    {
        Build();

        partsCount.text = $"×{parts}";
        UIButtons.Wire(applyButton, apply);

        var all = UpgradeCatalog.All;
        for (var i = 0; i < all.Length; i++)
        {
            var done = installed != null && i < installed.Count && installed[i];
            var affordable = parts >= all[i].Cost;
            var state = done ? CardState.Installed
                      : affordable ? CardState.Installable
                      : CardState.NotEnoughParts;

            buttonRoots[i].SetActive(state == CardState.Installable);
            statusLabels[i].gameObject.SetActive(state != CardState.Installable);

            switch (state)
            {
                case CardState.Installed:
                    statusLabels[i].text = "INSTALLED";
                    statusLabels[i].color = UITheme.Green;
                    break;
                case CardState.NotEnoughParts:
                    statusLabels[i].text = "NO PARTS";
                    statusLabels[i].color = UITheme.Cream;
                    break;
                default:
                    // 람다가 루프 변수를 잡지 않도록 한 번 복사한다. 잡으면 카드 9장이
                    // 전부 마지막 id를 넘긴다.
                    var id = all[i].Id;
                    UIButtons.Wire(installButtons[i], () => install?.Invoke(id));
                    break;
            }
        }

        var twin = IsInstalled(installed, UpgradeId.TwinMachine);
        twinMachineNote.text = twin ? "2구 적용" : string.Empty;
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
        if (countdown == null) return;
        countdown.text = Mathf.CeilToInt(Mathf.Max(seconds, 0f)).ToString();
    }
}
