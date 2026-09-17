using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// 캐릭터별로 어떤 모델 프리팹이 들어갔는지 보고, 그 자리에서 내 캐릭터에 입혀 본다.
///
/// 표는 `CharacterVisualConfig` 하나를 읽는다 — 선택창과 인게임이 같은 표를 보므로 여기가 곧 실제 배치다.
/// 입혀 보기는 `CharacterCheatGroup`과 같은 서버 치트를 쓰므로 호스트에서만 열린다.
public class CharacterModelGroup : DevConsoleGroup
{
    public override string Tab => "캐릭터";
    public override string Title => "캐릭터 모델";

    Label note;
    VisualElement list;
    readonly List<Button> wearButtons = new();

    /// 입혀 보기를 누른 뒤 새 모델이 스폰되면 선택한다. Animator 창에서 바로 상태를 보게 하려는 것이다.
    int pendingSelect = CharacterCatalog.NoPick;

    protected override void Build(VisualElement group)
    {
        var hint = new Label("프리팹 이름을 누르면 프로젝트에서 찾는다. 입혀 보기는 호스트 재생 중에만 된다.");
        hint.AddToClassList("hint");
        group.Add(hint);

        note = Row(group, "상태", "-");
        list = new VisualElement();
        group.Add(list);
        Btn(ButtonRow(group), "다시 검사", Rebuild);
        Rebuild();
    }

    /// 에셋 검사는 무겁지 않지만 10Hz로 돌릴 이유도 없다. 열 때와 버튼을 누를 때만 한다.
    void Rebuild()
    {
        list.Clear();
        wearButtons.Clear();

        var config = LoadConfig();
        if (config == null)
        {
            Row(list, "오류", "CharacterVisualConfig 애셋이 없다").AddToClassList("warn");
            return;
        }

        var placeholder = new SerializedObject(config).FindProperty("placeholderModel").objectReferenceValue;
        var all = CharacterCatalog.All;
        for (var i = 0; i < all.Length; i++)
        {
            var index = i;
            var prefab = config.ModelFor(all[i].Id);
            var isPlaceholder = prefab != null && prefab == placeholder;

            var status = Row(list, $"{all[i].Name} ({all[i].Id})", Check(prefab, isPlaceholder, out var ok));
            status.EnableInClassList("warn", !ok);

            var buttons = ButtonRow(list);
            Btn(buttons, prefab != null ? prefab.name : "(없음)", () => Ping(prefab)).SetEnabled(prefab != null);
            wearButtons.Add(Btn(buttons, "입혀 보기", () => Wear(index), "btn--primary"));
        }
    }

    public override void Refresh(in DevConsoleState state)
    {
        var canWear = state.IsServer && PlayerCharacter.Local() != null;
        note.text = !state.Playing ? "재생 전 · 목록만 확인"
            : !state.IsServer ? "입혀 보기는 호스트에서만"
            : canWear ? "입혀 보기 가능" : "내 플레이어 스폰 대기";
        foreach (var button in wearButtons) button.SetEnabled(canWear);

        if (pendingSelect == CharacterCatalog.NoPick) return;
        if (!state.Playing) { pendingSelect = CharacterCatalog.NoPick; return; }

        var local = PlayerCharacter.Local();
        var look = local != null ? local.GetComponent<PlayerLook>() : null;
        if (look == null || look.Model == null || local.Index != pendingSelect) return;

        var animator = look.Model.GetComponentInChildren<Animator>();
        Selection.activeGameObject = animator != null ? animator.gameObject : look.Model.gameObject;
        pendingSelect = CharacterCatalog.NoPick;
    }

    void Wear(int index)
    {
        var local = PlayerCharacter.Local();
        if (local == null) return;
        local.SetCharacterCheatServer(index);
        pendingSelect = index;
    }

    /// 애니메이션이 돌려면 CharacterModel·Animator 연결·컨트롤러가 모두 있어야 한다. 빠진 첫 항목을 적는다.
    static string Check(GameObject prefab, bool isPlaceholder, out bool ok)
    {
        ok = false;
        if (prefab == null) return "모델 없음";
        if (isPlaceholder) return "표에 없음 · 대역이 선다";

        var model = prefab.GetComponent<CharacterModel>();
        if (model == null) return "루트에 CharacterModel 없음";

        var animator = new SerializedObject(model).FindProperty("animator").objectReferenceValue as Animator;
        if (animator == null) return "CharacterModel의 Animator 칸이 비었다";
        if (animator.runtimeAnimatorController == null) return "Animator에 컨트롤러 없음";

        ok = true;
        return $"정상 · {animator.runtimeAnimatorController.name}";
    }

    static void Ping(Object asset)
    {
        EditorGUIUtility.PingObject(asset);
        Selection.activeObject = asset;
    }

    static CharacterVisualConfig LoadConfig()
    {
        var guids = AssetDatabase.FindAssets($"t:{nameof(CharacterVisualConfig)}");
        return guids.Length > 0
            ? AssetDatabase.LoadAssetAtPath<CharacterVisualConfig>(AssetDatabase.GUIDToAssetPath(guids[0]))
            : null;
    }
}
